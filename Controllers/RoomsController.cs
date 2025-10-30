using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyRoom.Data;
using StudyRoom.Models;
using StudyRoom.Services;
using System.Security.Claims;

namespace StudyRoom.Controllers
{
    [Authorize]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IRoomPresenceService _presence;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public RoomsController(ApplicationDbContext db, IRoomPresenceService presence)
        {
            _db = db; _presence = presence;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rooms = await _db.Rooms.Include(r => r.Members)
                .OrderByDescending(r => r.CreatedAt).ToListAsync();
            return View(rooms);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Room model)
        {
            if (model.IsPrivate && string.IsNullOrWhiteSpace(model.JoinCode))
                ModelState.AddModelError(nameof(model.JoinCode), "Join Code is required for private rooms.");

            if (!ModelState.IsValid) return View(model);

            model.OwnerId = UserId;
            _db.Rooms.Add(model);
            _db.RoomMembers.Add(new RoomMember { Room = model, UserId = UserId, Role = "owner" });
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var room = await _db.Rooms
                .Include(r => r.Members)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return NotFound();

            var isMember = await _db.RoomMembers.AnyAsync(m => m.RoomId == id && m.UserId == UserId);
            ViewBag.IsMember = isMember;

            // Initial history: last 12 hours or at least last 20
            var since = DateTime.UtcNow.AddHours(-12);
            var history = await _db.Messages
                .Where(m => m.RoomId == id && m.CreatedAt >= since)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.UserId,
                    DisplayName = m.User != null ? (m.User.DisplayName ?? m.User.UserName) : m.UserId,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync();

            if (history.Count < 20)
            {
                history = await _db.Messages
                    .Where(m => m.RoomId == id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(20)
                    .Select(m => new
                    {
                        m.Id,
                        m.UserId,
                        DisplayName = m.User != null ? (m.User.DisplayName ?? m.User.UserName) : m.UserId,
                        m.Content,
                        m.CreatedAt
                    }).ToListAsync();
                history.Reverse();
            }

            ViewBag.History = history;
            return View(room);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(Guid id, string? code)
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return NotFound();

            var already = await _db.RoomMembers.AnyAsync(m => m.RoomId == id && m.UserId == UserId);
            if (already) return RedirectToAction(nameof(Details), new { id });

            if (room.IsPrivate)
            {
                if (string.IsNullOrWhiteSpace(code) || !string.Equals(code.Trim(), room.JoinCode?.Trim(), StringComparison.Ordinal))
                {
                    TempData["JoinError"] = "Invalid join code.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            _db.RoomMembers.Add(new RoomMember { RoomId = id, UserId = UserId, Role = "member" });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Explore(string? q, string? subject, string? sort)
        {
            var query = _db.Rooms.AsNoTracking()
                .Include(r => r.Members)
                .Where(r => !r.IsPrivate);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(r => r.Name.Contains(q) || (r.Description != null && r.Description.Contains(q)));

            if (!string.IsNullOrWhiteSpace(subject))
                query = query.Where(r => r.Subject == subject);

            var rooms = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            var onlineCounts = _presence.GetAllCounts();
            var list = rooms.Select(r => new RoomCardVM
            {
                Id = r.Id,
                Name = r.Name,
                Subject = r.Subject,
                Description = r.Description,
                MembersCount = r.Members.Count,
                OnlineCount = onlineCounts.TryGetValue(r.Id, out var c) ? c : 0,
                CreatedAt = r.CreatedAt
            }).ToList();

            if (string.Equals(sort, "active", StringComparison.OrdinalIgnoreCase))
                list = list.OrderByDescending(x => x.OnlineCount).ThenByDescending(x => x.MembersCount).ToList();
            else if (string.Equals(sort, "new", StringComparison.OrdinalIgnoreCase))
                list = list.OrderByDescending(x => x.CreatedAt).ToList();

            ViewBag.Subjects = await _db.Rooms.Where(r => !r.IsPrivate && r.Subject != null)
                .Select(r => r.Subject!).Distinct().OrderBy(s => s).ToListAsync();
            ViewBag.Query = q; ViewBag.Subject = subject; ViewBag.Sort = sort;

            return View(list);
        }

        // History endpoint unchanged (keep if you already added it)
        [HttpGet]
        public async Task<IActionResult> History(Guid id, string? before, int take = 20)
        {
            if (take < 1) take = 1;
            if (take > 50) take = 50;

            if (!DateTime.TryParse(before, out var beforeUtc))
                beforeUtc = DateTime.UtcNow;

            var items = await _db.Messages
                .Where(m => m.RoomId == id && m.CreatedAt < beforeUtc)
                .OrderByDescending(m => m.CreatedAt)
                .Take(take)
                .Select(m => new
                {
                    m.Id,
                    m.UserId,
                    DisplayName = m.User != null ? (m.User.DisplayName ?? m.User.UserName) : m.UserId,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync();

            items.Reverse();
            return Json(items);
        }
    }
}
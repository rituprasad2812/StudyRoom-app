using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudyRoom.Data;
using StudyRoom.Hubs;
using StudyRoom.Models;
using System.Security.Claims;

namespace StudyRoom.Controllers
{
    [Authorize]
    [Route("Rooms/{roomId:guid}/Polls")]
    public class PollsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<PollsHub> _hub;
        public PollsController(ApplicationDbContext db, IHubContext<PollsHub> hub)
        { _db = db; _hub = hub; }
        private string? CurrentUserId => User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        // Anyone can view polls (public or private rooms).
        [AllowAnonymous]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> List(Guid roomId)
        {
            var roomExists = await _db.Rooms.AsNoTracking().AnyAsync(r => r.Id == roomId);
            if (!roomExists) return NotFound("Room not found.");

            var polls = await _db.Polls.AsNoTracking()
                .Where(p => p.RoomId == roomId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new
                {
                    id = p.Id,
                    question = p.Question,
                    isClosed = p.IsClosed || p.ExpiresAt <= DateTime.UtcNow,
                    expiresAt = p.ExpiresAt,
                    createdBy = p.CreatedBy,
                    creatorName = _db.Users
                        .Where(u => u.Id == p.CreatedBy)
                        .Select(u => u.DisplayName ?? u.UserName)
                        .FirstOrDefault() ?? "Unknown",
                    options = p.Options
                        .OrderBy(o => o.Position)
                        .Select(o => new
                        {
                            id = o.Id,
                            text = o.Text,
                            position = o.Position,
                            count = _db.PollVotes.Count(v => v.OptionId == o.Id)
                        })
                        .ToList()
                })
                .ToListAsync();

            return Json(new { polls });
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create(Guid roomId, [FromForm] string question,
            [FromForm] string? option1, [FromForm] string? option2, [FromForm] string? option3,
            [FromForm] string? option4, [FromForm] string? option5)
        {
            var uid = CurrentUserId;
            if (uid == null) return Unauthorized("Login required.");
            var isMember = await _db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == uid);
            if (!isMember) return BadRequest("Join the room to create polls.");

            question = (question ?? "").Trim();
            var options = new[] { option1, option2, option3, option4, option5 }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();

            if (string.IsNullOrWhiteSpace(question)) return BadRequest("Question is required.");
            if (options.Count < 2) return BadRequest("At least 2 options are required.");
            if (options.Count > 5) return BadRequest("Max 5 options.");

            var poll = new Poll
            {
                RoomId = roomId,
                Question = question,
                CreatedBy = uid,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
                IsClosed = false
            };
            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();

            int pos = 0;
            foreach (var txt in options)
                _db.PollOptions.Add(new PollOption { PollId = poll.Id, Text = txt, Position = pos++ });
            await _db.SaveChangesAsync();

            var dto = await _db.Polls.AsNoTracking()
                .Where(p => p.Id == poll.Id)
                .Select(p => new
                {
                    id = p.Id,
                    question = p.Question,
                    isClosed = p.IsClosed,
                    expiresAt = p.ExpiresAt,
                    createdBy = p.CreatedBy,
                    creatorName = _db.Users
                        .Where(u => u.Id == p.CreatedBy)
                        .Select(u => u.DisplayName ?? u.UserName)
                        .FirstOrDefault() ?? "Unknown",
                    options = p.Options.OrderBy(o => o.Position)
                        .Select(o => new { id = o.Id, text = o.Text, position = o.Position, count = 0 })
                        .ToList()
                })
                .FirstAsync();

            await _hub.Clients.Group($"room:{roomId}").SendAsync("PollCreated", dto);
            return Json(dto);
        }

        [HttpPost("{pollId:guid}/vote")]
        public async Task<IActionResult> Vote(Guid roomId, Guid pollId, [FromForm] Guid optionId)
        {
            var uid = CurrentUserId;
            if (uid == null) return Unauthorized("Login required.");
            var isMember = await _db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == uid);
            if (!isMember) return BadRequest("Join the room to vote.");

            var poll = await _db.Polls.Include(p => p.Options)
                .FirstOrDefaultAsync(p => p.Id == pollId && p.RoomId == roomId);
            if (poll == null) return NotFound("Poll not found.");

            var closed = poll.IsClosed || poll.ExpiresAt <= DateTime.UtcNow;
            if (closed) return BadRequest("Poll is closed.");

            var already = await _db.PollVotes.AnyAsync(v => v.PollId == pollId && v.UserId == uid);
            if (already) return BadRequest("You already voted.");

            var opt = poll.Options.FirstOrDefault(o => o.Id == optionId);
            if (opt == null) return BadRequest("Invalid option.");

            _db.PollVotes.Add(new PollVote { PollId = pollId, OptionId = optionId, UserId = uid, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            var counts = await _db.PollOptions.AsNoTracking()
                .Where(o => o.PollId == pollId)
                .OrderBy(o => o.Position)
                .Select(o => new { optionId = o.Id, count = _db.PollVotes.Count(v => v.OptionId == o.Id) })
                .ToListAsync();

            await _hub.Clients.Group($"room:{roomId}").SendAsync("PollVoted", new { pollId, counts });
            return Json(new { ok = true });
        }

        [HttpPost("{pollId:guid}/close")]
        public async Task<IActionResult> Close(Guid roomId, Guid pollId)
        {
            var uid = CurrentUserId;
            if (uid == null) return Unauthorized("Login required.");

            var poll = await _db.Polls
                .Include(p => p.Options)
                .FirstOrDefaultAsync(p => p.Id == pollId && p.RoomId == roomId);
            if (poll == null) return NotFound("Poll not found.");
            if (poll.CreatedBy != uid) return Forbid();

            // Delete poll entirely (cascade deletes options and votes) so it vanishes
            _db.Polls.Remove(poll);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group($"room:{roomId}").SendAsync("PollDeleted", new { pollId });
            return Json(new { ok = true });
        }
    }
}

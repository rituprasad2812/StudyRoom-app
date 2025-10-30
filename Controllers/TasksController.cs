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
    [Route("Rooms/{roomId:guid}/Tasks")]
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<TasksHub> _hub;
        public TasksController(ApplicationDbContext db, IHubContext<TasksHub> hub)
        {
            _db = db; _hub = hub;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        private async Task<(bool isMember, bool isOwner)> GetPerms(Guid roomId)
        {
            var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId);
            if (room == null) return (false, false);
            var isMember = await _db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == UserId);
            var isOwner = room.OwnerId == UserId;
            return (isMember, isOwner);
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid roomId)
        {
            var (isMember, _) = await GetPerms(roomId);
            if (!isMember) return Forbid();

            var items = await _db.RoomTasks
                .Where(t => t.RoomId == roomId)
                .OrderBy(t => t.Status)
                .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
                .ThenBy(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    status = t.Status,
                    dueAt = t.DueAt, // UTC
                    createdBy = t.CreatedBy,
                    createdAt = t.CreatedAt
                })
                .ToListAsync();

            return Json(new { items });
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid roomId, [FromForm] string title, [FromForm] string? dueDate)
        {
            var (isMember, _) = await GetPerms(roomId);
            if (!isMember) return Forbid();

            title = (title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");

            DateTime? dueAtUtc = null;
            if (!string.IsNullOrWhiteSpace(dueDate))
            {
                if (DateTime.TryParse(dueDate, out var local))
                {
                    if (local.Kind == DateTimeKind.Unspecified)
                        local = DateTime.SpecifyKind(local, DateTimeKind.Local);
                    dueAtUtc = local.ToUniversalTime();
                }
            }

            var task = new RoomTask
            {
                RoomId = roomId,
                Title = title,
                Status = "todo",
                DueAt = dueAtUtc,
                CreatedBy = UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.RoomTasks.Add(task);
            await _db.SaveChangesAsync();

            var dto = new
            {
                id = task.Id,
                title = task.Title,
                status = task.Status,
                dueAt = task.DueAt,
                createdBy = task.CreatedBy,
                createdAt = task.CreatedAt
            };

            await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskCreated", dto);
            return Json(dto);
        }

        [HttpPost("{taskId:guid}/status")]
        public async Task<IActionResult> ChangeStatus(Guid roomId, Guid taskId, [FromForm] string status)
        {
            var (isMember, isOwner) = await GetPerms(roomId);
            if (!isMember) return Forbid();

            status = (status ?? "").ToLowerInvariant();
            var allowed = new HashSet<string> { "todo", "doing", "done" };
            if (!allowed.Contains(status)) return BadRequest("Invalid status.");

            var task = await _db.RoomTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.RoomId == roomId);
            if (task == null) return NotFound();

            if (!(isOwner || task.CreatedBy == UserId)) return Forbid();

            task.Status = status;
            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var dto = new
            {
                id = task.Id,
                title = task.Title,
                status = task.Status,
                dueAt = task.DueAt,
                createdBy = task.CreatedBy,
                createdAt = task.CreatedAt
            };

            await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskUpdated", dto);
            return Json(dto);
        }

        [HttpPost("{taskId:guid}/update")]
        public async Task<IActionResult> Update(Guid roomId, Guid taskId, [FromForm] string title, [FromForm] string? dueDate)
        {
            var (isMember, isOwner) = await GetPerms(roomId);
            if (!isMember) return Forbid();

            var task = await _db.RoomTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.RoomId == roomId);
            if (task == null) return NotFound();

            if (!(isOwner || task.CreatedBy == UserId)) return Forbid();

            title = (title ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(title))
                task.Title = title;

            if (dueDate != null)
            {
                if (dueDate == "")
                {
                    task.DueAt = null;
                }
                else if (DateTime.TryParse(dueDate, out var local))
                {
                    if (local.Kind == DateTimeKind.Unspecified)
                        local = DateTime.SpecifyKind(local, DateTimeKind.Local);
                    task.DueAt = local.ToUniversalTime();
                }
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var dto = new
            {
                id = task.Id,
                title = task.Title,
                status = task.Status,
                dueAt = task.DueAt,
                createdBy = task.CreatedBy,
                createdAt = task.CreatedAt
            };

            await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskUpdated", dto);
            return Json(dto);
        }

        [HttpPost("{taskId:guid}/delete")]
        public async Task<IActionResult> Delete(Guid roomId, Guid taskId)
        {
            var (isMember, isOwner) = await GetPerms(roomId);
            if (!isMember) return Forbid();

            var task = await _db.RoomTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.RoomId == roomId);
            if (task == null) return NotFound();

            if (!(isOwner || task.CreatedBy == UserId)) return Forbid();

            _db.RoomTasks.Remove(task);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskDeleted", new { id = taskId });
            return Json(new { ok = true });
        }
    }
}
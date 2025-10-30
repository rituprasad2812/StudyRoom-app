using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudyRoom.Data;
using StudyRoom.Models;
using StudyRoom.Services;

namespace StudyRoom.Hubs
{
    [Authorize]
    public class TimerHub : Hub
    {
        private readonly IRoomTimerManager _mgr;
        private readonly ApplicationDbContext _db;
        private readonly IBadgeService _badges;
        public TimerHub(IRoomTimerManager mgr, ApplicationDbContext db, IBadgeService badges)
        {
            _mgr = mgr; _db = db; _badges = badges;
        }

        public Task<string> Ping() => Task.FromResult("pong");

        // Join by raw string (no parse) so this never fails
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        }

        public Task<TimerState> Sync(string roomId)
        {
            // Timer state lives per room Guid in the manager; if parse fails, return idle
            return Guid.TryParse(roomId, out var rid)
                ? Task.FromResult(_mgr.Get(rid))
                : Task.FromResult(new TimerState(Guid.Empty, false, 0, 0, "idle"));
        }

        public async Task Start(string roomId, int seconds, string phase = "focus")
        {
            if (!Guid.TryParse(roomId, out var rid)) return;
            var s = _mgr.Start(rid, seconds, phase);
            await Clients.Group($"room:{roomId}").SendAsync("TimerUpdated", s);
        }

        public async Task Pause(string roomId)
        {
            if (!Guid.TryParse(roomId, out var rid)) return;
            var s = _mgr.Pause(rid);
            await Clients.Group($"room:{roomId}").SendAsync("TimerUpdated", s);
        }

        public async Task Resume(string roomId)
        {
            if (!Guid.TryParse(roomId, out var rid)) return;
            var s = _mgr.Resume(rid);
            await Clients.Group($"room:{roomId}").SendAsync("TimerUpdated", s);
        }

        public async Task LogFocusSession(string roomId, int seconds)
        {
            if (seconds <= 0) return;
            if (!Guid.TryParse(roomId, out var rid)) return;

            var userId = Context.UserIdentifier ?? Context.ConnectionId;
            var end = DateTime.UtcNow;
            var start = end.AddSeconds(-seconds);

            _db.StudySessions.Add(new StudySession
            {
                RoomId = rid,
                UserId = userId,
                Phase = "focus",
                StartedAt = start,
                EndedAt = end,
                DurationSeconds = seconds
            });
            await _db.SaveChangesAsync();

            var earned = await _badges.EvaluateOnSessionAsync(userId, start, seconds);
            if (earned.Count > 0)
            {
                await Clients.Caller.SendAsync("BadgesAwarded",
                    earned.Select(b => new { key = b.Key, name = b.Name, icon = b.Icon }));
            }
        }
    }
}
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudyRoom.Data;
using StudyRoom.Models;
using StudyRoom.Services;

namespace StudyRoom.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly IRoomPresenceService _presence;
        public ChatHub(ApplicationDbContext db, IRoomPresenceService presence)
        {
            _db = db; _presence = presence;
        }

        public Task<string> Ping() => Task.FromResult("pong");

        private string GetUserIdSafe()
        {
            var uid = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(uid)) return uid;
            uid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uid)) return uid;
            uid = Context.User?.Identity?.Name;
            return string.IsNullOrEmpty(uid) ? Context.ConnectionId : uid!;
        }

        // Track rooms joined by a connection for presence cleanup
        private static readonly ConcurrentDictionary<string, HashSet<string>> RoomsByConnection = new();

        public async Task JoinRoom(string roomId)
        {
            // Always join the group (string id avoids binding issues)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");

            // Presence (only if roomId parses)
            if (Guid.TryParse(roomId, out var rid))
            {
                _presence.Join(rid, GetUserIdSafe());
                RoomsByConnection.AddOrUpdate(
                    Context.ConnectionId,
                    _ => new HashSet<string> { roomId },
                    (_, set) => { set.Add(roomId); return set; }
                );
            }

            await Clients.Group($"room:{roomId}").SendAsync("UserJoined", new { userId = GetUserIdSafe() });
            await SendOnlineUsers(roomId);
        }

        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomId}");

            if (Guid.TryParse(roomId, out var rid))
            {
                _presence.Leave(rid, GetUserIdSafe());
            }

            await Clients.Group($"room:{roomId}").SendAsync("UserLeft", new { userId = GetUserIdSafe() });
            await SendOnlineUsers(roomId);
        }

        public async Task SendMessage(string roomId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var userId = GetUserIdSafe();

            // Persist when roomId is a valid Guid
            if (Guid.TryParse(roomId, out var rid))
            {
                var msg = new Message { RoomId = rid, UserId = userId, Content = text };
                _db.Messages.Add(msg);
                await _db.SaveChangesAsync();

                var user = await _db.Users.FindAsync(userId);
                await Clients.Group($"room:{roomId}").SendAsync("MessageReceived", new
                {
                    id = msg.Id,
                    roomId,
                    userId,
                    displayName = user?.DisplayName ?? user?.UserName,
                    content = msg.Content,
                    createdAt = msg.CreatedAt
                });
                return;
            }

            // Fallback broadcast if roomId parse fails (shouldn’t happen with real GUIDs)
            await Clients.Group($"room:{roomId}").SendAsync("MessageReceived", new
            {
                id = Guid.NewGuid(),
                roomId,
                userId,
                displayName = userId,
                content = text,
                createdAt = DateTime.UtcNow
            });
        }

        public async Task<List<object>> GetOnlineUsers(string roomId)
        {
            if (!Guid.TryParse(roomId, out var rid)) return new();

            var ids = _presence.GetOnlineUserIds(rid).ToList();
            if (ids.Count == 0) return new();

            var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
            return users.Select(u => new { userId = u.Id, displayName = u.DisplayName ?? u.UserName } as object).ToList();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (RoomsByConnection.TryRemove(Context.ConnectionId, out var rooms))
            {
                foreach (var roomId in rooms)
                {
                    if (Guid.TryParse(roomId, out var rid))
                    {
                        _presence.Leave(rid, GetUserIdSafe());
                    }
                    await Clients.Group($"room:{roomId}").SendAsync("UserLeft", new { userId = GetUserIdSafe() });
                    await SendOnlineUsers(roomId);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        private async Task SendOnlineUsers(string roomId)
        {
            var list = await GetOnlineUsers(roomId);
            await Clients.Group($"room:{roomId}").SendAsync("OnlineUsers", list);
        }
    }
}
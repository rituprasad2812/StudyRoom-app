using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StudyRoom.Hubs
{
    [Authorize]
    public class TasksHub : Hub
    {
        public Task<string> Ping() => Task.FromResult("pong");
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        }

        public async Task LeaveRoom(string roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomId}");
        }
    }
}
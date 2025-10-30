using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StudyRoom.Services
{
    public class RoomPresenceService : IRoomPresenceService
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _online =
        new();
        public void Join(Guid roomId, string userId)
        {
            var set = _online.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, byte>());
            set[userId] = 1;
        }

        public void Leave(Guid roomId, string userId)
        {
            if (_online.TryGetValue(roomId, out var set))
            {
                set.TryRemove(userId, out _);
                if (set.IsEmpty) _online.TryRemove(roomId, out _);
            }
        }

        public int GetOnlineCount(Guid roomId) =>
            _online.TryGetValue(roomId, out var set) ? set.Count : 0;

        public IReadOnlyDictionary<Guid, int> GetAllCounts() =>
            _online.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

        public IReadOnlyCollection<string> GetOnlineUserIds(Guid roomId) =>
            _online.TryGetValue(roomId, out var set) ? set.Keys.ToArray() : Array.Empty<string>();
    }
}
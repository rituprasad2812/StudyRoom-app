using System;
using System.Collections.Generic;

namespace StudyRoom.Services
{
public interface IRoomPresenceService
{
void Join(Guid roomId, string userId);
void Leave(Guid roomId, string userId);
int GetOnlineCount(Guid roomId);
IReadOnlyDictionary<Guid, int> GetAllCounts();
IReadOnlyCollection<string> GetOnlineUserIds(Guid roomId);
}
}
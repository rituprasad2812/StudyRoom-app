using System;

namespace StudyRoom.Services
{
    // Single source of truth for the timer state
    public record TimerState(Guid RoomId, bool Running, int SecondsRemaining, int TotalSeconds, string Phase);
    public interface IRoomTimerManager
    {
        TimerState Get(Guid roomId);
        TimerState Start(Guid roomId, int seconds, string phase);
        TimerState Pause(Guid roomId);
        TimerState Resume(Guid roomId);
    }
}
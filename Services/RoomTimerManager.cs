using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using StudyRoom.Hubs;

namespace StudyRoom.Services
{
    internal class RoomTimer
    {
        public bool Running { get; set; }
        public int TotalSeconds { get; set; }
        public string Phase { get; set; } = "focus";
        public DateTimeOffset? EndsAt { get; set; } // when running
        public int PausedRemaining { get; set; } // when paused
        public CancellationTokenSource? Cts { get; set; } // end notify
    }
    public class RoomTimerManager : IRoomTimerManager
    {
        private readonly IHubContext<TimerHub> _hub;
        private readonly ConcurrentDictionary<Guid, RoomTimer> _timers = new();

        public RoomTimerManager(IHubContext<TimerHub> hub) { _hub = hub; }

        public TimerState Get(Guid roomId)
        {
            if (!_timers.TryGetValue(roomId, out var rt))
                return new TimerState(roomId, false, 0, 0, "idle");

            var remaining = GetRemaining(rt);
            return new TimerState(roomId, rt.Running, remaining, rt.TotalSeconds, rt.Phase);
        }

        public TimerState Start(Guid roomId, int seconds, string phase)
        {
            var rt = _timers.GetOrAdd(roomId, _ => new RoomTimer());
            CancelEndNotify(rt);

            rt.TotalSeconds = Math.Max(1, seconds);
            rt.Phase = string.IsNullOrWhiteSpace(phase) ? "focus" : phase;
            rt.Running = true;
            rt.EndsAt = DateTimeOffset.UtcNow.AddSeconds(rt.TotalSeconds);
            rt.PausedRemaining = 0;

            ScheduleEndNotify(roomId, rt);
            return Get(roomId);
        }

        public TimerState Pause(Guid roomId)
        {
            if (!_timers.TryGetValue(roomId, out var rt))
                return new TimerState(roomId, false, 0, 0, "idle");

            // Only compute paused remaining if we were running
            CancelEndNotify(rt);
            if (rt.Running && rt.EndsAt.HasValue)
                rt.PausedRemaining = GetRemaining(rt);

            rt.Running = false;
            rt.EndsAt = null;

            return Get(roomId);
        }

        public TimerState Resume(Guid roomId)
        {
            if (!_timers.TryGetValue(roomId, out var rt))
                return new TimerState(roomId, false, 0, 0, "idle");

            // No-op if already running (important: do NOT zero-out)
            if (rt.Running && rt.EndsAt.HasValue)
                return Get(roomId);

            // Only resume if we actually have paused time left
            var rem = Math.Max(0, rt.PausedRemaining);
            if (rem <= 0)
            {
                // Nothing to resume; keep current state (idle/ended)
                rt.Running = false;
                rt.EndsAt = null;
                return Get(roomId);
            }

            CancelEndNotify(rt);
            rt.Running = true;
            rt.EndsAt = DateTimeOffset.UtcNow.AddSeconds(rem);
            rt.PausedRemaining = 0;

            ScheduleEndNotify(roomId, rt);
            return Get(roomId);
        }

        private static int GetRemaining(RoomTimer rt)
        {
            if (rt.Running && rt.EndsAt.HasValue)
            {
                var sec = (int)Math.Ceiling((rt.EndsAt.Value - DateTimeOffset.UtcNow).TotalSeconds);
                return Math.Max(0, sec);
            }
            return Math.Max(0, rt.PausedRemaining);
        }

        private void ScheduleEndNotify(Guid roomId, RoomTimer rt)
        {
            if (!rt.Running || !rt.EndsAt.HasValue) return;

            var delayMs = (int)Math.Max(0, (rt.EndsAt.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
            rt.Cts = new CancellationTokenSource();
            var token = rt.Cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token);
                    if (token.IsCancellationRequested) return;

                    // Timer ended
                    rt.Running = false;
                    rt.PausedRemaining = 0;
                    rt.EndsAt = null;

                    var state = Get(roomId);
                    await _hub.Clients.Group($"room:{roomId}").SendAsync("TimerEnded", state);
                }
                catch (TaskCanceledException) { }
                catch { }
            }, token);
        }

        private static void CancelEndNotify(RoomTimer rt)
        {
            try { rt.Cts?.Cancel(); } catch { }
            rt.Cts = null;
        }
    }
}
using Microsoft.EntityFrameworkCore;
using StudyRoom.Data;
using StudyRoom.Models;

namespace StudyRoom.Services
{
    public class BadgeService : IBadgeService
    {
        private readonly ApplicationDbContext _db;
        public BadgeService(ApplicationDbContext db) { _db = db; }
        public async Task<List<Badge>> EvaluateOnSessionAsync(string userId, DateTime sessionStartUtc, int durationSeconds)
        {
            var awarded = new List<Badge>();
            var keys = new[] { "early_bird", "consistency_7", "focus_master_10" };
            var badges = await _db.Badges.Where(b => keys.Contains(b.Key)).ToListAsync();
            var byKey = badges.ToDictionary(b => b.Key, b => b);

            var owned = await _db.UserBadges
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BadgeId)
                .ToListAsync();

            bool Has(string key) => byKey.TryGetValue(key, out var b) && owned.Contains(b.Id);

            // Early Bird (before 7 AM local)
            var localStart = sessionStartUtc.ToLocalTime();
            if (localStart.Hour < 7 && byKey.TryGetValue("early_bird", out var eb) && !Has("early_bird"))
            {
                _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = eb.Id, AwardedAt = DateTime.UtcNow });
                awarded.Add(eb);
            }

            // Focus Master: >= 10 focus sessions
            var focusCount = await _db.StudySessions.CountAsync(s => s.UserId == userId && s.Phase == "focus");
            if (focusCount >= 10 && byKey.TryGetValue("focus_master_10", out var fm) && !Has("focus_master_10"))
            {
                _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = fm.Id, AwardedAt = DateTime.UtcNow });
                awarded.Add(fm);
            }

            // Consistency 7: focus on each of last 7 days
            if (byKey.TryGetValue("consistency_7", out var c7) && !Has("consistency_7"))
            {
                var today = DateTime.UtcNow.Date;
                var from = today.AddDays(-6);
                var days = await _db.StudySessions
                    .Where(s => s.UserId == userId && s.Phase == "focus" && s.StartedAt >= from)
                    .Select(s => s.StartedAt.Date)
                    .Distinct()
                    .ToListAsync();

                bool ok = true;
                for (var d = from; d <= today; d = d.AddDays(1))
                    if (!days.Contains(d)) { ok = false; break; }

                if (ok)
                {
                    _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = c7.Id, AwardedAt = DateTime.UtcNow });
                    awarded.Add(c7);
                }
            }

            if (awarded.Count > 0)
                await _db.SaveChangesAsync();

            return awarded;
        }
    }
}
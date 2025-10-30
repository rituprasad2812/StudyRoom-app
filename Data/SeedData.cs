using StudyRoom.Models;

namespace StudyRoom.Data
{
    public static class SeedData
    {
        // Idempotent: safe to call every startup
        public static void EnsureBadges(ApplicationDbContext db)
        {
            var defaults = new[]
            {
                new Badge { Key = "early_bird", Name = "Early Bird", Description = "Studied before 7 AM", Icon = "🌅" },
                new Badge { Key = "consistency_7", Name = "7-Day Streak", Description = "Studied 7 days in a row", Icon = "🔥" },
                new Badge { Key = "focus_master_10", Name = "Focus Master", Description = "Completed 10 Pomodoro sessions", Icon = "🎯" }
            };
            foreach (var b in defaults)
            {
                if (!db.Badges.Any(x => x.Key == b.Key))
                    db.Badges.Add(b);
            }
            db.SaveChanges();
        }

        public static void EnsureDisplayNames(ApplicationDbContext db)
        {
            var users = db.Users.Where(u => string.IsNullOrEmpty(u.DisplayName)).ToList();
            foreach (var u in users)
            {
                var shortName = (u.UserName?.Contains('@') == true)
                ? u.UserName.Split('@')[0]
                : (string.IsNullOrWhiteSpace(u.UserName) ? "User" : u.UserName);
                u.DisplayName = char.ToUpper(shortName[0]) + shortName.Substring(1);
            }
            db.SaveChanges();
        }
    }
}
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyRoom.Data;
using StudyRoom.Models;

namespace StudyRoom.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) => _db = db;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        public async Task<IActionResult> Index()
        {
            var userId = UserId;
            var today = DateTime.UtcNow.Date;
            var from30 = today.AddDays(-29);
            var from7 = today.AddDays(-6);

            var my = await _db.StudySessions
                .Where(s => s.UserId == userId && s.Phase == "focus" && s.StartedAt >= from30)
                .AsNoTracking()
                .ToListAsync();

            // Daily totals for last 30 days
            var daily = Enumerable.Range(0, 30)
                .Select(i => from30.AddDays(i))
                .Select(d => new
                {
                    Day = d,
                    Seconds = my.Where(s => s.StartedAt.Date == d).Sum(s => s.DurationSeconds)
                })
                .ToList();

            var total30 = daily.Sum(x => x.Seconds);
            var total7 = daily.Where(x => x.Day >= from7).Sum(x => x.Seconds);

            // Streak (consecutive days up to today)
            var daySet = new HashSet<DateTime>(my.Select(s => s.StartedAt.Date).Distinct());
            var streak = 0;
            for (var d = today; ; d = d.AddDays(-1))
            {
                if (daySet.Contains(d)) streak++;
                else break;
                if (streak > 365) break;
            }

            // My badges
            var myBadges = await _db.UserBadges
                .Include(ub => ub.Badge)
                .Where(ub => ub.UserId == userId)
                .OrderByDescending(ub => ub.AwardedAt)
                .ToListAsync();

            // Leaderboard (week/month)
            var weekAgg = await _db.StudySessions
                .Where(s => s.Phase == "focus" && s.StartedAt >= from7)
                .GroupBy(s => s.UserId)
                .Select(g => new { UserId = g.Key, Seconds = g.Sum(x => x.DurationSeconds) })
                .OrderByDescending(x => x.Seconds)
                .Take(10)
                .ToListAsync();

            var monthAgg = await _db.StudySessions
                .Where(s => s.Phase == "focus" && s.StartedAt >= from30)
                .GroupBy(s => s.UserId)
                .Select(g => new { UserId = g.Key, Seconds = g.Sum(x => x.DurationSeconds) })
                .OrderByDescending(x => x.Seconds)
                .Take(10)
                .ToListAsync();

            var ids = weekAgg.Select(x => x.UserId).Concat(monthAgg.Select(x => x.UserId)).Distinct().ToList();

            // Map userId -> display name (fallback to UserName, then userId)
            var nameMap = await _db.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, Name = u.DisplayName ?? u.UserName })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Id);

            string NameOf(string id) =>
                nameMap.TryGetValue(id, out var nm) && !string.IsNullOrWhiteSpace(nm) ? nm : id;

            var vm = new DashboardViewModel
            {
                Labels = daily.Select(x => x.Day.ToString("MM-dd")).ToList(),
                Values = daily.Select(x => x.Seconds / 60).ToList(), // minutes
                TotalWeekMinutes = total7 / 60,
                TotalMonthMinutes = total30 / 60,
                StreakDays = streak,
                WeekLeaders = weekAgg.Select(x => new LeaderboardEntry { UserName = NameOf(x.UserId), Minutes = x.Seconds / 60 }).ToList(),
                MonthLeaders = monthAgg.Select(x => new LeaderboardEntry { UserName = NameOf(x.UserId), Minutes = x.Seconds / 60 }).ToList(),
                Badges = myBadges.Select(b => new BadgeDTO(b.Badge!.Key, b.Badge!.Name, b.Badge!.Description, b.Badge!.Icon, b.AwardedAt)).ToList()
            };

            return View(vm);
        }
    }

    public class DashboardViewModel
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Values { get; set; } = new();
        public int TotalWeekMinutes { get; set; }
        public int TotalMonthMinutes { get; set; }
        public int StreakDays { get; set; }
        public List<LeaderboardEntry> WeekLeaders { get; set; } = new();
        public List<LeaderboardEntry> MonthLeaders { get; set; } = new();
        public List<BadgeDTO> Badges { get; set; } = new();
    }

    public class LeaderboardEntry
    {
        public string UserName { get; set; } = "";
        public int Minutes { get; set; }
    }

    public record BadgeDTO(string Key, string Name, string Description, string Icon, DateTime AwardedAt);
}
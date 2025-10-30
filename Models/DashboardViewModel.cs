using System;

namespace StudyRoom.ViewModels
{
    public class DashboardViewModel
    {
        public System.Collections.Generic.List<string> Labels { get; set; } = new();
        public System.Collections.Generic.List<int> Values { get; set; } = new();
        public int TotalWeekMinutes { get; set; }
        public int TotalMonthMinutes { get; set; }
        public int StreakDays { get; set; }
        public System.Collections.Generic.List<LeaderboardEntry> WeekLeaders { get; set; } = new();
        public System.Collections.Generic.List<LeaderboardEntry> MonthLeaders { get; set; } = new();
        public System.Collections.Generic.List<BadgeDTO> Badges { get; set; } = new();
    }
    public class LeaderboardEntry
    {
        public string UserName { get; set; } = "";
        public int Minutes { get; set; }
    }

    public record BadgeDTO(string Key, string Name, string Description, string Icon, DateTime AwardedAt);
}
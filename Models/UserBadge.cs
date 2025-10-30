namespace StudyRoom.Models
{
    public class UserBadge
    {
        public string UserId { get; set; } = "";
        public int BadgeId { get; set; }
        public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
        public ApplicationUser? User { get; set; }
        public Badge? Badge { get; set; }
    }
}
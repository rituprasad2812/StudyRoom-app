namespace StudyRoom.Models
{
    public class RoomMember
    {
        public Guid RoomId { get; set; }
        public Room? Room { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        public string Role { get; set; } = "member";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
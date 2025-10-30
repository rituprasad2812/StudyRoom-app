namespace StudyRoom.Models
{
    public class StudySession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Room? Room { get; set; }

        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public int DurationSeconds { get; set; }
        public string Phase { get; set; } = "focus"; // focus/break
    }
}
namespace StudyRoom.Models
{
    public class Message
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Room? Room { get; set; }
        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
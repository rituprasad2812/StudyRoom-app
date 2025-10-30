namespace StudyRoom.Models
{
    public class PollVote
    {
        public Guid PollId { get; set; }
        public Poll? Poll { get; set; }
        public Guid OptionId { get; set; }
        public PollOption? Option { get; set; }

        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
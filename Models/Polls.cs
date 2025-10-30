using System.ComponentModel.DataAnnotations;

namespace StudyRoom.Models
{
    public class Poll
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Room? Room { get; set; }

        [Required, StringLength(200)]
        public string Question { get; set; } = "";

        public bool Multiple { get; set; } = false; // reserved (MVP single-choice)

        [Required]
        public string CreatedBy { get; set; } = "";
        public ApplicationUser? Creator { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(3);
        public bool IsClosed { get; set; } = false;

        public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
        public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    }
}
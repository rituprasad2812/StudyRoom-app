using System.ComponentModel.DataAnnotations;

namespace StudyRoom.Models
{
    public class RoomTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Room? Room { get; set; }

        [Required, StringLength(160)]
        public string Title { get; set; } = "";

        // todo | doing | done
        [Required, StringLength(12)]
        public string Status { get; set; } = "todo";

        // Store in UTC
        public DateTime? DueAt { get; set; }

        [Required]
        public string CreatedBy { get; set; } = "";
        public ApplicationUser? Creator { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int Position { get; set; } = 0; // reserved for future drag&drop
    }
}
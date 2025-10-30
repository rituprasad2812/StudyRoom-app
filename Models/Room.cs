using System.ComponentModel.DataAnnotations;

namespace StudyRoom.Models
{
    public class Room
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required, StringLength(80)]
        public string Name { get; set; } = "";

        [StringLength(80)]
        public string? Subject { get; set; }

        [StringLength(300)]
        public string? Description { get; set; }

        public bool IsPrivate { get; set; }

        // NEW: simple join code for private rooms
        [StringLength(32)]
        public string? JoinCode { get; set; }

        public string OwnerId { get; set; } = "";
        public ApplicationUser? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    }
}
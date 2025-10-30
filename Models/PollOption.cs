using System.ComponentModel.DataAnnotations;

namespace StudyRoom.Models
{
    public class PollOption
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PollId { get; set; }
        public Poll? Poll { get; set; }

        [Required, StringLength(120)]
        public string Text { get; set; } = "";

        public int Position { get; set; } = 0;
    }
}
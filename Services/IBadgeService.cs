using StudyRoom.Models;
namespace StudyRoom.Services
{
    public interface IBadgeService
    {
        // Return the badges newly awarded for this completed session
        Task<List<Badge>> EvaluateOnSessionAsync(string userId, DateTime sessionStartUtc, int durationSeconds);
    }
}

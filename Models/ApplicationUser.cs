using Microsoft.AspNetCore.Identity;

namespace StudyRoom.Models
{
public class ApplicationUser : IdentityUser
{
public string? DisplayName { get; set; }
public string? AvatarUrl { get; set; }
public string? Goals { get; set; }
}
}
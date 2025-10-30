using System;

namespace StudyRoom.Models
{
public class RoomCardVM
{
public Guid Id { get; set; }
public string Name { get; set; } = "";
public string? Subject { get; set; }
public string? Description { get; set; }
public int MembersCount { get; set; }
public int OnlineCount { get; set; }
public DateTime CreatedAt { get; set; }
}
}
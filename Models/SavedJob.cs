namespace Xllerate.Models;

public class SavedJob
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid JobRoleId { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public JobRole JobRole { get; set; } = null!;
}
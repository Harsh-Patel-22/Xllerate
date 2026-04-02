namespace Xllerate.Models.Interfaces;

public interface ITimestamped {
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
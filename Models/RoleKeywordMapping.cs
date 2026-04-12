namespace Xllerate.Models;

public class RoleKeywordMapping
{
    public Guid Id { get; set; }
    public string RoleKey { get; set; } = string.Empty;     
    public string Keyword { get; set; } = string.Empty;      
    public bool IsSeeded { get; set; }                      
    public int ObservedCount { get; set; }                    
    public DateTime CreatedAt { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace Yonder.DataProvider;

public class Classes
{
    [Required]
    public int Id { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public DateTime? EndDate { get; set; }
}

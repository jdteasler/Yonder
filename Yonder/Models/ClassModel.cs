using System.ComponentModel.DataAnnotations;

namespace Yonder.Models;

public class ClassModel
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50, ErrorMessage = "Class name cannot be longer than 50 characters.")]
    public string ClassName { get; set; } = string.Empty;

    [MaxLength(250, ErrorMessage = "Class description cannot be longer than 250 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50, ErrorMessage = "Teacher name cannot be longer than 50 characters.")]
    public string TeacherName { get; set; } = string.Empty;

    public DateTime EndDate { get; set; }
}

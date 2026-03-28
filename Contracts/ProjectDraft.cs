using System.ComponentModel.DataAnnotations;

namespace FocusOnTask.Contracts;

public sealed class ProjectDraft
{
    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public DateTime DateStart { get; set; } = DateTime.Today;

    public DateTime? DateEnd { get; set; }
}

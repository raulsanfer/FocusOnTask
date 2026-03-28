using System.ComponentModel.DataAnnotations;

namespace FocusOnTask.Models;

public sealed class Project
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public DateTime DateStart { get; set; } = DateTime.Today;

    public DateTime? DateEnd { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = [];
}

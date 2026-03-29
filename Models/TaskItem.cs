using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FocusOnTask.Models;

public sealed class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(280)]
    public string Title { get; set; } = string.Empty;

    [StringLength(280)]
    public string? Description { get; set; }

    [StringLength(400)]
    public string? Tags { get; set; }

    public DateTime? DateTimeCreated { get; set; }
    public int? ParentTaskId { get; set; }
    public TaskItem? ParentTask { get; set; }

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public int? WorkSessionId { get; set; }
    public WorkSession? WorkSession { get; set; }

    public DateTime? DateTimeStart { get; set; }
    public DateTime? DateTimeEnd { get; set; }

    public decimal EstimationHours { get; set; }
    public decimal WorkedHours { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.NotStarted;
    public DateTime? ActiveSegmentStart { get; set; }
    public DateTime LastStatusChange { get; set; } = DateTime.Now;

    public ICollection<TaskItem> Subtasks { get; set; } = [];
    public ICollection<TaskSessionLog> SessionLogs { get; set; } = [];

    [NotMapped]
    public bool IsSubtask => ParentTaskId.HasValue;

    [NotMapped]
    public string DisplayName => string.IsNullOrWhiteSpace(Description) ? Title : Description;
}

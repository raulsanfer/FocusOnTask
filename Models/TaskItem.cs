using System.ComponentModel.DataAnnotations;

namespace FocusOnTask.Models;

public sealed class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(140)]
    public string Title { get; set; } = string.Empty;

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

    public ICollection<TaskSessionLog> SessionLogs { get; set; } = [];
}

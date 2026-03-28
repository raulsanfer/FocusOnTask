using System.ComponentModel.DataAnnotations;

namespace FocusOnTask.Contracts;

public sealed class TaskDraft
{
    [Required]
    [StringLength(140)]
    public string Title { get; set; } = string.Empty;

    public int? ProjectId { get; set; }

    [Range(0.25, 500)]
    public decimal EstimationHours { get; set; } = 1m;

    public bool AttachToActiveSession { get; set; } = true;
}

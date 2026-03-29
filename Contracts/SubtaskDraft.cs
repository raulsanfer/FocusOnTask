using System.ComponentModel.DataAnnotations;

namespace FocusOnTask.Contracts;

public sealed class SubtaskDraft
{
    [Required]
    [StringLength(280)]
    public string Description { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Tags { get; set; }

    [Range(0.25, 500)]
    public decimal EstimationHours { get; set; } = 1m;
}

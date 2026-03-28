using System.ComponentModel.DataAnnotations;

namespace FocusOnTask.Contracts;

public sealed class ConfigurationDraft
{
    [Range(1, 16)]
    public decimal DefaultWorkSessionDurationHours { get; set; } = 8m;
}

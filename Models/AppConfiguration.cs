namespace FocusOnTask.Models;

public sealed class AppConfiguration
{
    public int Id { get; set; } = 1;
    public decimal DefaultWorkSessionDurationHours { get; set; } = 8m;
}

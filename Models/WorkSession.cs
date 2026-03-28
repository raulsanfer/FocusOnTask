namespace FocusOnTask.Models;

public sealed class WorkSession
{
    public int Id { get; set; }
    public DateTime DateTimeStart { get; set; } = DateTime.Now;
    public DateTime? DateTimeEnd { get; set; }
    public decimal ExpectedDurationHours { get; set; } = 8m;

    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<TaskSessionLog> SessionLogs { get; set; } = [];
}

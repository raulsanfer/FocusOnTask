namespace FocusOnTask.Models;

public sealed class TaskSessionLog
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public int WorkSessionId { get; set; }
    public WorkSession? WorkSession { get; set; }

    public DateTime DateTimeStart { get; set; }
    public DateTime DateTimeEnd { get; set; }
    public decimal WorkedHours { get; set; }
}

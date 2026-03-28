namespace FocusOnTask.Models;

public sealed class DashboardSnapshot
{
    public WorkSession? ActiveSession { get; init; }
    public string? ActiveTaskTitle { get; init; }
    public decimal ConfiguredDurationHours { get; init; }
    public int TotalProjects { get; init; }
    public int OpenTasks { get; init; }
    public int CompletedTasks { get; init; }
    public decimal EstimatedHours { get; init; }
    public decimal WorkedHours { get; init; }
    public IReadOnlyList<ProjectSnapshot> ActiveProjects { get; init; } = [];
    public IReadOnlyList<TaskSnapshot> FocusTasks { get; init; } = [];
    public IReadOnlyList<WorkSessionSnapshot> RecentSessions { get; init; } = [];
}

public sealed class ProjectSnapshot
{
    public required string Name { get; init; }
    public int OpenTasks { get; init; }
    public int CompletedTasks { get; init; }
    public decimal Progress { get; init; }
}

public sealed class TaskSnapshot
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public string? ProjectName { get; init; }
    public TaskStatus Status { get; init; }
    public decimal EstimationHours { get; init; }
    public decimal WorkedHours { get; init; }
}

public sealed class WorkSessionSnapshot
{
    public int Id { get; init; }
    public DateTime DateTimeStart { get; init; }
    public DateTime? DateTimeEnd { get; init; }
    public int TaskCount { get; init; }
    public decimal TotalWorkedHours { get; init; }
}

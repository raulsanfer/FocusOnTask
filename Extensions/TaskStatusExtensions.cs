using FocusOnTask.Models;
using TaskState = FocusOnTask.Models.TaskStatus;

namespace FocusOnTask.Extensions;

public static class TaskStatusExtensions
{
    public static string ToDisplayName(this TaskState status) =>
        status switch
        {
            TaskState.NotStarted => "No iniciada",
            TaskState.Started => "Iniciada",
            TaskState.Stopped => "Parada",
            TaskState.Completed => "Finalizada",
            _ => status.ToString()
        };

    public static string ToCssClass(this TaskState status) =>
        status switch
        {
            TaskState.NotStarted => "status-waiting",
            TaskState.Started => "status-live",
            TaskState.Stopped => "status-paused",
            TaskState.Completed => "status-done",
            _ => "status-idle"
        };
}

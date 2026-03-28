using FocusOnTask.Contracts;
using FocusOnTask.Data;
using FocusOnTask.Models;
using Microsoft.EntityFrameworkCore;
using TaskState = FocusOnTask.Models.TaskStatus;

namespace FocusOnTask.Services;

public sealed class FocusWorkspaceService(IDbContextFactory<FocusOnTaskDbContext> dbContextFactory)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _initializationLock = new();
    private Task? _initializationTask;

    public async Task<DashboardSnapshot> GetDashboardAsync()
    {
        await EnsureInitializedAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var activeSession = await db.WorkSessions
            .AsNoTracking()
            .OrderByDescending(session => session.DateTimeStart)
            .FirstOrDefaultAsync(session => session.DateTimeEnd == null);

        var activeTaskTitle = activeSession is null
            ? null
            : await db.Tasks
                .AsNoTracking()
                .Where(task => task.WorkSessionId == activeSession.Id && task.Status == TaskState.Started)
                .Select(task => task.Title)
                .FirstOrDefaultAsync();

        var configuration = await db.AppConfigurations.AsNoTracking().SingleAsync();

        var activeProjects = await db.Projects
            .AsNoTracking()
            .Include(project => project.Tasks)
            .OrderByDescending(project => project.DateStart)
            .Take(5)
            .Select(project => new ProjectSnapshot
            {
                Name = project.Name,
                OpenTasks = project.Tasks.Count(task => task.Status != TaskState.Completed),
                CompletedTasks = project.Tasks.Count(task => task.Status == TaskState.Completed),
                Progress = project.Tasks.Count == 0
                    ? 0
                    : decimal.Round(project.Tasks.Count(task => task.Status == TaskState.Completed) * 100m / project.Tasks.Count, 0, MidpointRounding.AwayFromZero)
            })
            .ToListAsync();

        var focusTasks = await db.Tasks
            .AsNoTracking()
            .Include(task => task.Project)
            .OrderBy(task => task.Status == TaskState.Started ? 0 : task.Status == TaskState.Stopped ? 1 : 2)
            .ThenByDescending(task => task.LastStatusChange)
            .Take(6)
            .Select(task => new TaskSnapshot
            {
                Id = task.Id,
                Title = task.Title,
                ProjectName = task.Project != null ? task.Project.Name : null,
                Status = task.Status,
                EstimationHours = task.EstimationHours,
                WorkedHours = task.WorkedHours
            })
            .ToListAsync();

        var recentSessions = await db.WorkSessions
            .AsNoTracking()
            .Include(session => session.Tasks)
            .Include(session => session.SessionLogs)
            .OrderByDescending(session => session.DateTimeStart)
            .Take(5)
            .Select(session => new WorkSessionSnapshot
            {
                Id = session.Id,
                DateTimeStart = session.DateTimeStart,
                DateTimeEnd = session.DateTimeEnd,
                TaskCount = session.Tasks.Count,
                TotalWorkedHours = session.SessionLogs.Sum(log => log.WorkedHours)
            })
            .ToListAsync();

        return new DashboardSnapshot
        {
            ActiveSession = activeSession,
            ActiveTaskTitle = activeTaskTitle,
            ConfiguredDurationHours = configuration.DefaultWorkSessionDurationHours,
            TotalProjects = await db.Projects.CountAsync(),
            OpenTasks = await db.Tasks.CountAsync(task => task.Status != TaskState.Completed),
            CompletedTasks = await db.Tasks.CountAsync(task => task.Status == TaskState.Completed),
            EstimatedHours = await db.Tasks.SumAsync(task => task.EstimationHours),
            WorkedHours = await db.Tasks.SumAsync(task => task.WorkedHours),
            ActiveProjects = activeProjects,
            FocusTasks = focusTasks,
            RecentSessions = recentSessions
        };
    }

    public async Task<IReadOnlyList<Project>> GetProjectsAsync()
    {
        await EnsureInitializedAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.Projects
            .AsNoTracking()
            .Include(project => project.Tasks)
            .OrderByDescending(project => project.DateStart)
            .ThenBy(project => project.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync()
    {
        await EnsureInitializedAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.Tasks
            .AsNoTracking()
            .Include(task => task.Project)
            .OrderBy(task => task.Status == TaskState.Started ? 0 : task.Status == TaskState.Stopped ? 1 : task.Status == TaskState.NotStarted ? 2 : 3)
            .ThenByDescending(task => task.LastStatusChange)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkSession>> GetWorkSessionsAsync()
    {
        await EnsureInitializedAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.WorkSessions
            .AsNoTracking()
            .Include(session => session.Tasks)
            .OrderByDescending(session => session.DateTimeStart)
            .ToListAsync();
    }

    public async Task<AppConfiguration> GetConfigurationAsync()
    {
        await EnsureInitializedAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.AppConfigurations.AsNoTracking().SingleAsync();
    }

    public async Task AddProjectAsync(ProjectDraft draft)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            db.Projects.Add(new Project
            {
                Name = draft.Name.Trim(),
                DateStart = draft.DateStart,
                DateEnd = draft.DateEnd
            });
            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddTaskAsync(TaskDraft draft)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var activeSession = draft.AttachToActiveSession
                ? await db.WorkSessions.SingleOrDefaultAsync(session => session.DateTimeEnd == null)
                : null;

            db.Tasks.Add(new TaskItem
            {
                Title = draft.Title.Trim(),
                ProjectId = draft.ProjectId,
                EstimationHours = decimal.Round(draft.EstimationHours, 2, MidpointRounding.AwayFromZero),
                Status = TaskState.NotStarted,
                WorkSessionId = activeSession?.Id,
                LastStatusChange = DateTime.Now
            });

            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateConfigurationAsync(ConfigurationDraft draft)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var configuration = await db.AppConfigurations.SingleAsync();
            configuration.DefaultWorkSessionDurationHours = decimal.Round(draft.DefaultWorkSessionDurationHours, 2, MidpointRounding.AwayFromZero);
            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StartWorkSessionAsync()
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (await db.WorkSessions.AnyAsync(session => session.DateTimeEnd == null))
            {
                return;
            }

            var now = DateTime.Now;
            var configuration = await db.AppConfigurations.SingleAsync();
            var workSession = new WorkSession
            {
                DateTimeStart = now,
                ExpectedDurationHours = configuration.DefaultWorkSessionDurationHours
            };

            db.WorkSessions.Add(workSession);
            await db.SaveChangesAsync();

            var lastStoppedTask = await db.Tasks
                .Where(task => task.Status == TaskState.Stopped)
                .OrderByDescending(task => task.LastStatusChange)
                .FirstOrDefaultAsync();

            if (lastStoppedTask is not null)
            {
                lastStoppedTask.WorkSessionId = workSession.Id;
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopWorkSessionAsync()
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var activeSession = await db.WorkSessions
                .Include(session => session.Tasks)
                .SingleOrDefaultAsync(session => session.DateTimeEnd == null);

            if (activeSession is null)
            {
                return;
            }

            var now = DateTime.Now;
            var activeTask = activeSession.Tasks.FirstOrDefault(task => task.Status == TaskState.Started);
            if (activeTask is not null)
            {
                CloseActiveSegment(activeTask, activeSession, now, TaskState.Stopped, db);
            }

            activeSession.DateTimeEnd = now;
            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StartTaskAsync(int taskId)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var activeSession = await db.WorkSessions.SingleOrDefaultAsync(session => session.DateTimeEnd == null);
            if (activeSession is null)
            {
                return;
            }

            var task = await db.Tasks.SingleOrDefaultAsync(item => item.Id == taskId);
            if (task is null || task.Status == TaskState.Completed)
            {
                return;
            }

            var currentTask = await db.Tasks.SingleOrDefaultAsync(item => item.Status == TaskState.Started);
            var now = DateTime.Now;

            if (currentTask is not null && currentTask.Id != task.Id && currentTask.WorkSessionId is int currentSessionId)
            {
                var currentSession = await db.WorkSessions.SingleAsync(item => item.Id == currentSessionId);
                CloseActiveSegment(currentTask, currentSession, now, TaskState.Stopped, db);
            }

            task.WorkSessionId = activeSession.Id;
            task.DateTimeStart ??= now;
            task.DateTimeEnd = null;
            task.ActiveSegmentStart = now;
            task.LastStatusChange = now;
            task.Status = TaskState.Started;

            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PauseTaskAsync(int taskId)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var task = await db.Tasks.SingleOrDefaultAsync(item => item.Id == taskId);
            if (task is null || task.Status != TaskState.Started || task.WorkSessionId is null)
            {
                return;
            }

            var session = await db.WorkSessions.SingleAsync(item => item.Id == task.WorkSessionId.Value);
            CloseActiveSegment(task, session, DateTime.Now, TaskState.Stopped, db);
            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteTaskAsync(int taskId)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var task = await db.Tasks.SingleOrDefaultAsync(item => item.Id == taskId);
            if (task is null)
            {
                return;
            }

            var now = DateTime.Now;
            if (task.Status == TaskState.Started && task.WorkSessionId is not null)
            {
                var session = await db.WorkSessions.SingleAsync(item => item.Id == task.WorkSessionId.Value);
                CloseActiveSegment(task, session, now, TaskState.Completed, db);
            }
            else
            {
                task.Status = TaskState.Completed;
                task.DateTimeEnd = now;
                task.LastStatusChange = now;
                task.ActiveSegmentStart = null;
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        Task initializationTask;

        lock (_initializationLock)
        {
            _initializationTask ??= InitializeAsync();
            initializationTask = _initializationTask;
        }

        await initializationTask;
    }

    private async Task InitializeAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    private static void CloseActiveSegment(TaskItem task, WorkSession session, DateTime endedAt, TaskState nextStatus, FocusOnTaskDbContext db)
    {
        if (task.ActiveSegmentStart is DateTime startedAt)
        {
            var workedHours = decimal.Round((decimal)(endedAt - startedAt).TotalHours, 2, MidpointRounding.AwayFromZero);
            task.WorkedHours += workedHours;
            db.TaskSessionLogs.Add(new TaskSessionLog
            {
                TaskItemId = task.Id,
                WorkSessionId = session.Id,
                DateTimeStart = startedAt,
                DateTimeEnd = endedAt,
                WorkedHours = workedHours
            });
        }

        task.DateTimeEnd = endedAt;
        task.ActiveSegmentStart = null;
        task.LastStatusChange = endedAt;
        task.Status = nextStatus;
    }
}

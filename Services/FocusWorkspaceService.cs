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
                .Select(task => task.Description != null && task.Description != string.Empty ? task.Description : task.Title)
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
                Title = task.Description != null && task.Description != string.Empty ? task.Description : task.Title,
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

        var tasks = await db.Tasks
            .AsNoTracking()
            .Where(task => task.ParentTaskId == null)
            .Include(task => task.Project)
            .Include(task => task.Subtasks)
            .OrderBy(task => task.Status == TaskState.Started ? 0 : task.Status == TaskState.Stopped ? 1 : task.Status == TaskState.NotStarted ? 2 : 3)
            .ThenByDescending(task => task.LastStatusChange)
            .ToListAsync();

        foreach (var task in tasks)
        {
            task.Subtasks = task.Subtasks
                .OrderBy(item => item.Status == TaskState.Started ? 0 : item.Status == TaskState.Stopped ? 1 : item.Status == TaskState.NotStarted ? 2 : 3)
                .ThenByDescending(item => item.LastStatusChange)
                .ToList();
        }

        return tasks;
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
            var now = DateTime.Now;
            var title = draft.Title.Trim();

            db.Tasks.Add(new TaskItem
            {
                Title = title,
                Description = title,
                DateTimeCreated = now,
                ProjectId = draft.ProjectId,
                EstimationHours = decimal.Round(draft.EstimationHours, 2, MidpointRounding.AwayFromZero),
                Status = TaskState.NotStarted,
                WorkSessionId = activeSession?.Id,
                LastStatusChange = now
            });

            await db.SaveChangesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddSubtaskAsync(int parentTaskId, SubtaskDraft draft)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            var parentTask = await db.Tasks.SingleOrDefaultAsync(item => item.Id == parentTaskId);
            if (parentTask is null || parentTask.ParentTaskId is not null)
            {
                return;
            }

            var now = DateTime.Now;
            var description = draft.Description.Trim();
            var tags = string.IsNullOrWhiteSpace(draft.Tags) ? null : draft.Tags.Trim();

            db.Tasks.Add(new TaskItem
            {
                Title = description,
                Description = description,
                Tags = tags,
                DateTimeCreated = now,
                ParentTaskId = parentTask.Id,
                ProjectId = parentTask.ProjectId,
                EstimationHours = decimal.Round(draft.EstimationHours, 2, MidpointRounding.AwayFromZero),
                Status = TaskState.NotStarted,
                LastStatusChange = now
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

    public async Task DeleteTaskAsync(int taskId)
    {
        await EnsureInitializedAsync();
        await _gate.WaitAsync();

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            if (!await db.Tasks.AnyAsync(item => item.Id == taskId))
            {
                return;
            }

            var taskIds = await GetTaskIdsForDeletionAsync(db, taskId);
            var sessionLogs = await db.TaskSessionLogs
                .Where(log => taskIds.Contains(log.TaskItemId))
                .ToListAsync();

            if (sessionLogs.Count != 0)
            {
                db.TaskSessionLogs.RemoveRange(sessionLogs);
            }

            var tasks = await db.Tasks
                .Where(task => taskIds.Contains(task.Id))
                .OrderByDescending(task => task.ParentTaskId.HasValue ? 1 : 0)
                .ToListAsync();

            db.Tasks.RemoveRange(tasks);
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
        await EnsureTaskSchemaAsync(db);
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

    private static async Task EnsureTaskSchemaAsync(FocusOnTaskDbContext db)
    {
        var taskColumns = await GetTableColumnsAsync(db, "Tasks");

        if (!taskColumns.Contains("Description"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tasks ADD COLUMN Description TEXT NULL");
        }

        if (!taskColumns.Contains("Tags"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tasks ADD COLUMN Tags TEXT NULL");
        }

        if (!taskColumns.Contains("DateTimeCreated"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tasks ADD COLUMN DateTimeCreated TEXT NULL");
        }

        if (!taskColumns.Contains("ParentTaskId"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tasks ADD COLUMN ParentTaskId INTEGER NULL");
        }

        await db.Database.ExecuteSqlRawAsync("UPDATE Tasks SET Description = COALESCE(NULLIF(Description, ''), Title)");
        await db.Database.ExecuteSqlRawAsync("UPDATE Tasks SET DateTimeCreated = COALESCE(DateTimeCreated, DateTimeStart, LastStatusChange, CURRENT_TIMESTAMP)");
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Tasks_ParentTaskId ON Tasks (ParentTaskId)");
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(FocusOnTaskDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName}')";

            await using var reader = await command.ExecuteReaderAsync();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(reader.GetOrdinal("name")));
            }

            return columns;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<List<int>> GetTaskIdsForDeletionAsync(FocusOnTaskDbContext db, int rootTaskId)
    {
        var pendingIds = new Queue<int>();
        var collectedIds = new HashSet<int>();
        pendingIds.Enqueue(rootTaskId);

        while (pendingIds.Count > 0)
        {
            var currentId = pendingIds.Dequeue();
            if (!collectedIds.Add(currentId))
            {
                continue;
            }

            var childIds = await db.Tasks
                .Where(task => task.ParentTaskId == currentId)
                .Select(task => task.Id)
                .ToListAsync();

            foreach (var childId in childIds)
            {
                pendingIds.Enqueue(childId);
            }
        }

        return [.. collectedIds];
    }
}

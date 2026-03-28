using FocusOnTask.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusOnTask.Data;

public sealed class FocusOnTaskDbContext(DbContextOptions<FocusOnTaskDbContext> options) : DbContext(options)
{
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskSessionLog> TaskSessionLogs => Set<TaskSessionLog>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppConfiguration>()
            .HasData(new AppConfiguration
            {
                Id = 1,
                DefaultWorkSessionDurationHours = 8m
            });

        modelBuilder.Entity<Project>()
            .Property(project => project.Name)
            .HasMaxLength(120);

        modelBuilder.Entity<TaskItem>()
            .ToTable("Tasks");

        modelBuilder.Entity<TaskItem>()
            .Property(task => task.Title)
            .HasMaxLength(140);

        modelBuilder.Entity<TaskItem>()
            .HasOne(task => task.Project)
            .WithMany(project => project.Tasks)
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TaskItem>()
            .HasOne(task => task.WorkSession)
            .WithMany(workSession => workSession.Tasks)
            .HasForeignKey(task => task.WorkSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TaskSessionLog>()
            .HasOne(log => log.TaskItem)
            .WithMany(task => task.SessionLogs)
            .HasForeignKey(log => log.TaskItemId);

        modelBuilder.Entity<TaskSessionLog>()
            .HasOne(log => log.WorkSession)
            .WithMany(workSession => workSession.SessionLogs)
            .HasForeignKey(log => log.WorkSessionId);
    }
}

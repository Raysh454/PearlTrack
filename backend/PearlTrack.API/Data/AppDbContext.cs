using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PearlTrack.API.Models;

namespace PearlTrack.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> TaskItems { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<TaskAssignee> TaskAssignees { get; set; }
    public DbSet<TaskCategory> TaskCategories { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // TaskItem configuration
        builder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Assignees)
                .WithOne(ta => ta.TaskItem)
                .HasForeignKey(ta => ta.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TaskCategories)
                .WithOne(tc => tc.TaskItem)
                .HasForeignKey(tc => tc.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Category configuration
        builder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasMany(e => e.TaskCategories)
                .WithOne(tc => tc.Category)
                .HasForeignKey(tc => tc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TaskAssignee configuration
        builder.Entity<TaskAssignee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.Assignees)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TaskItemId, e.UserId }).IsUnique();
        });

        // TaskCategory configuration
        builder.Entity<TaskCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AddedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.TaskItem)
                .WithMany(t => t.TaskCategories)
                .HasForeignKey(e => e.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.TaskCategories)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TaskItemId, e.CategoryId }).IsUnique();
        });
    }
}

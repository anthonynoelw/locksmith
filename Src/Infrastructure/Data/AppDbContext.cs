namespace Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Domain;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// The application database context.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AppDbContext"/> class.
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options, string? databaseUser = null) : DbContext(options)
{
    /// <summary>
    /// Gets the database user name used for setting up constraints and permissions.
    /// </summary>
    public string DatabaseUser { get; } = databaseUser ?? "postgres";

    /// <summary>
    /// Gets or sets the API keys.
    /// </summary>
    public DbSet<ApiKey> ApiKeys { get; set; }

    /// <summary>
    /// Gets or sets the API key statuses.
    /// </summary>
    public DbSet<ApiKeyStatus> ApiKeyStatuses { get; set; }

    /// <summary>
    /// Gets or sets the API key actions.
    /// </summary>
    public DbSet<ApiKeyAction> ApiKeyActions { get; set; }

    /// <summary>
    /// Validates that append-only entities are not being modified or deleted.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        ValidateAppendOnlyConstraints();
        return base.SaveChanges();
    }

    /// <summary>
    /// Validates that append-only entities are not being modified or deleted.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateAppendOnlyConstraints();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Configures the model for the database context.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApiKey>()
            .HasMany(e => e.Statuses)
            .WithOne(e => e.ApiKey)
            .HasForeignKey(e => e.ApiKeyId);

        modelBuilder.Entity<ApiKey>()
            .HasMany(e => e.Actions)
            .WithOne(e => e.ApiKey)
            .HasForeignKey(e => e.ApiKeyId);

        modelBuilder.Entity<ApiKey>()
            .HasIndex(e => e.IdempotencyKeyHash)
            .IsUnique();

        modelBuilder.Entity<ApiKeyStatus>()
            .HasIndex(e => new { e.ApiKeyId, e.CreatedAt })
            .IsDescending(false, true);

        modelBuilder.Entity<ApiKeyAction>()
            .HasIndex(e => e.ApiKeyId);
    }

    private void ValidateAppendOnlyConstraints()
    {
        var violations = ChangeTracker.Entries()
            .Where(e => e.Entity is IAppendOnlyTable && e.State == EntityState.Deleted)
            .ToList();

        if (violations.Count > 0)
        {
            EntityEntry? firstViolation = violations[0];
            throw new AppendOnlyViolationException(firstViolation.Entity.GetType().Name, "Delete");
        }

        var modifiedAppendOnlyEntries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAppendOnlyTable && e.State == EntityState.Modified)
            .ToList();

        foreach (EntityEntry entry in modifiedAppendOnlyEntries)
        {
            var modifiedProperties = entry.Properties
                .Where(p => p.IsModified && p.Metadata.Name != "DeletedAt")
                .ToList();

            if (modifiedProperties.Count > 0)
            {
                throw new AppendOnlyViolationException(entry.Entity.GetType().Name, "Update");
            }
        }
    }
}

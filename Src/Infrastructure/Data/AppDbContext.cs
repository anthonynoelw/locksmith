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
    /// Name of the partial unique index that guarantees at most one active (non-revoked) grant per
    /// (ApiKeyId, Action) pair. Repositories match on this name to translate unique violations
    /// raised by concurrent grants into domain conflicts.
    /// </summary>
    public const string ACTIVE_ACTION_UNIQUE_INDEX = "IX_ApiKeyActions_ApiKeyId_Action_Active";

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
    /// Gets or sets the idempotency keys.
    /// </summary>
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }

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
    /// Attaches <paramref name="entity"/> in the <see cref="EntityState.Unchanged"/> state if it is not
    /// already tracked by this context.
    /// </summary>
    /// <remarks>
    /// Use this before adding a new entity whose navigation properties were loaded via a no-tracking
    /// query (e.g. resolved through an unrelated lookup). Without it, EF Core treats the detached
    /// navigation as a new entity to insert alongside the one being added, causing a primary key
    /// violation on save.
    /// <para>
    /// <see cref="Attach(object)"/> marks the entire reachable object graph as
    /// <see cref="EntityState.Unchanged"/>, not just <paramref name="entity"/> itself. Only call this
    /// with an entity whose own navigation collections (if any) are empty or already correctly tracked —
    /// attaching an entity with a populated collection of entities meant to be newly inserted in the
    /// same <see cref="SaveChangesAsync(CancellationToken)"/> call would silently mark them unchanged too.
    /// </para>
    /// </remarks>
    /// <param name="entity">The entity to attach if it is currently detached.</param>
    public void AttachIfDetached(object entity)
    {
        if (Entry(entity).State == EntityState.Detached)
        {
            Attach(entity);
        }
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
            .HasIndex(e => e.SecretHash)
            .IsUnique();

        modelBuilder.Entity<IdempotencyKey>()
            .HasOne(e => e.ApiKey)
            .WithMany()
            .HasForeignKey(e => e.ApiKeyId);

        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(e => e.IdempotencyKeyHash)
            .IsUnique();

        modelBuilder.Entity<ApiKeyStatus>()
            .HasIndex(e => new { e.ApiKeyId, e.CreatedAt })
            .IsDescending(false, true);

        modelBuilder.Entity<ApiKeyAction>()
            .HasIndex(e => e.ApiKeyId);

        // At most one active grant per (ApiKeyId, Action): concurrent duplicate grants fail at the
        // database instead of producing duplicate active rows that a single revoke cannot clear.
        modelBuilder.Entity<ApiKeyAction>()
            .HasIndex(e => new { e.ApiKeyId, e.Action })
            .HasDatabaseName(ACTIVE_ACTION_UNIQUE_INDEX)
            .HasFilter("\"DeletedAt\" IS NULL")
            .IsUnique();
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

# Rollback Strategies

Every production migration must have a tested rollback plan before it is applied.
A rollback plan that has not been tested is not a plan.

---

## Rollback levels

There are three levels of rollback, depending on how far the deployment progressed:

| Level | When to use | Mechanism |
|---|---|---|
| **EF Core rollback** | Migration applied; application not yet deployed | `dotnet ef database update <PreviousMigration>` |
| **Application rollback** | Application deployed; critical bug found | Redeploy previous app version + reverse migration |
| **Point-in-time recovery** | Data corrupted; rollback would lose data | Restore from DB snapshot taken before migration |

---

## EF Core rollback commands

```bash
# List all migrations and their current applied status
dotnet ef migrations list \
  --project Src/MyApp.Infrastructure \
  --startup-project Src/MyApp.Api

# Roll back to a specific migration (runs Down() for all migrations after it)
dotnet ef database update AddOrderStatusIndex \
  --project Src/MyApp.Infrastructure \
  --startup-project Src/MyApp.Api

# Roll back ALL migrations — leaves an empty database
dotnet ef database update 0 \
  --project Src/MyApp.Infrastructure \
  --startup-project Src/MyApp.Api

# Generate a DOWN script without applying it
dotnet ef migrations script CurrentMigration PreviousMigration \
  --project Src/MyApp.Infrastructure \
  --output rollback.sql
```

---

## Pre-deployment: take a database snapshot

Always take a snapshot of the production database before applying a migration.
This is the safety net for point-in-time recovery.

```bash
# PostgreSQL — dump the schema and data
pg_dump \
  --host $DB_HOST \
  --username $DB_USER \
  --dbname $DB_NAME \
  --format custom \
  --file "backup-$(date +%Y%m%d-%H%M%S).dump"

# PostgreSQL — schema only (faster, for structure verification)
pg_dump \
  --host $DB_HOST \
  --username $DB_USER \
  --dbname $DB_NAME \
  --schema-only \
  --file "schema-before-$(date +%Y%m%d-%H%M%S).sql"

# SQL Server — via sqlcmd or SSMS
BACKUP DATABASE [MyAppDb]
TO DISK = N'/backups/myappdb-20240615.bak'
WITH FORMAT, COMPRESSION, STATS = 10;
```

For managed cloud databases, use the platform's point-in-time restore:
- **Azure SQL / Azure Database for PostgreSQL**: Portal → Restore → point in time
- **AWS RDS**: Automated snapshots (hourly) + manual snapshot before migration
- **Google Cloud SQL**: On-demand backup before migration

---

## Writing a rollback-safe `Down()` method

### Down() is mandatory — never skip it

```csharp
// WRONG — leaves the schema in a forward-only state
protected override void Down(MigrationBuilder migrationBuilder)
{
    throw new NotImplementedException("Rollback not supported.");
}

// WRONG — empty Down()
protected override void Down(MigrationBuilder migrationBuilder) { }
```

### Down() must be the exact inverse of Up()

```csharp
// Up() adds a column
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "IsVerified",
        table: "Users",
        nullable: false,
        defaultValue: false);

    migrationBuilder.CreateIndex(
        name: "ix_users_is_verified",
        table: "Users",
        column: "IsVerified");
}

// Down() removes them in reverse order
protected override void Down(MigrationBuilder migrationBuilder)
{
    // Remove index before removing column (dependency order)
    migrationBuilder.DropIndex(
        name: "ix_users_is_verified",
        table: "Users");

    migrationBuilder.DropColumn(
        name: "IsVerified",
        table: "Users");
}
```

### When Down() cannot restore data

Document it clearly — do not pretend data can be recovered if it cannot:

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // IRREVERSIBLE: The LegacyNotes column is dropped.
    // Restoring the column structure is possible, but the data is permanently lost.
    // Before applying this migration to production, verify that:
    // 1. No application code reads LegacyNotes
    // 2. A full database backup was taken immediately before this migration
    migrationBuilder.AddColumn<string>(
        name: "LegacyNotes",
        table: "Orders",
        nullable: true);
    // Data is not restored — column will be empty after rollback.
}
```

---

## Rollback decision tree

```
Migration applied to production?
├─ NO  → Run: dotnet ef database update <PreviousMigration>
│         Done.
│
└─ YES, application also deployed?
   ├─ NO (migration applied, old code still running)
   │   └─ Can Down() safely run without breaking the running old code?
   │       ├─ YES → Run: dotnet ef database update <PreviousMigration>
   │       └─ NO  → Fix forward — do NOT rollback the DB while old code is live
   │                Create a corrective migration instead.
   │
   └─ YES (new code is live)
       ├─ Is the bug in application code only (migration is fine)?
       │   └─ Redeploy previous application version.
       │      Migration stays applied.
       │
       ├─ Is the bug in the migration and data is still intact?
       │   └─ 1. Redeploy previous application version
       │      2. Run: dotnet ef database update <PreviousMigration>
       │      3. Fix the migration
       │      4. Redeploy
       │
       └─ Is data corrupted or lost?
           └─ 1. Take the application offline (maintenance mode)
              2. Restore from the pre-migration snapshot
              3. Fix root cause
              4. Re-apply corrected migration
```

---

## Testing rollback (mandatory before every production deployment)

Run this procedure on a copy of the production database every time, not just once:

```bash
# 1. Take a schema snapshot of the current production state
dotnet ef migrations script --idempotent --output before.sql

# 2. Apply the new migration
dotnet ef database update

# 3. Verify the schema is as expected
dotnet ef migrations list  # new migration should show as Applied

# 4. Roll back
dotnet ef database update <PreviousMigrationName>

# 5. Take a schema snapshot of the rolled-back state
dotnet ef migrations script --idempotent --output after-rollback.sql

# 6. Diff the two snapshots — they must be identical
diff before.sql after-rollback.sql
# Expected output: no differences
```

Any differences in step 6 indicate a bug in `Down()`. Fix it before deploying.

---

## Squashing migrations (consolidating history)

After a long period of development, the migrations folder can accumulate dozens of
small migrations. Squashing consolidates them into a single baseline.

**Only squash migrations that have been applied to all environments** —
you cannot squash a migration that a developer or environment still needs to apply.

```bash
# 1. Ensure all migrations are applied everywhere
dotnet ef migrations list  # all should show as Applied

# 2. Remove all migration files (keep the snapshot)
# Delete all .cs files in the Migrations/ folder EXCEPT the ModelSnapshot

# 3. Create a single consolidated migration
dotnet ef migrations add InitialSchema \
  --project Src/MyApp.Infrastructure \
  --startup-project Src/MyApp.Api

# 4. The generated migration contains all current tables.
# Manually add the __EFMigrationsHistory insertion to skip it in existing DBs:
```

```csharp
// In the squashed migration's Up():
// Guard: if the DB already has tables, skip this migration
protected override void Up(MigrationBuilder migrationBuilder)
{
    // This migration is a consolidated baseline.
    // It will only run on fresh databases.
    // Existing databases already have this schema applied via previous migrations.
    migrationBuilder.Sql("""
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_name = 'Users'
            ) THEN
                -- (All CREATE TABLE statements go here)
            END IF;
        END $$;
        """);
}
```

---

## Runbook template for production deployments

Copy this for each production migration:

```markdown
## Migration Runbook: [MigrationName]

**Date:** [YYYY-MM-DD]
**Author:** [Name]
**Reviewer:** [Name]

### Changes
[One sentence: what this migration does]

### Classification
[ ] Additive  [ ] Breaking  [ ] Rename  [ ] Destructive  [ ] Data migration

### Zero-downtime required?
[ ] Yes — expand/contract steps documented below
[ ] No — single-step deploy

### Pre-deployment checklist
- [ ] Idempotent SQL script generated and reviewed
- [ ] Migration tested on staging with production-size dataset
- [ ] Rollback tested on staging (diff was clean)
- [ ] Database snapshot taken: [backup filename/timestamp]
- [ ] Deployment window communicated to team

### Deployment steps
1. Apply migration: `dotnet ef database update`
2. Verify: [specific SQL query to confirm schema is as expected]
3. Deploy application version: [version/tag]
4. Smoke test: [URL or test to run]

### Rollback steps
1. Redeploy previous application version: [version/tag]
2. Roll back migration: `dotnet ef database update [PreviousMigrationName]`
3. Verify rollback: `diff before.sql after-rollback.sql`
4. If data is corrupted: restore from snapshot [backup filename]

### Monitoring
- Watch for errors in: [log query / dashboard link]
- Expected: [what normal looks like after migration]
- Alert on: [what would indicate the migration caused a problem]

### Sign-off
- [ ] Migration applied to production
- [ ] Smoke tests passed
- [ ] No errors in logs for 15 minutes post-deployment
```

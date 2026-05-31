# Zero-Downtime Deployments

Schema changes that touch columns read or written by live application code require a
multi-step deployment strategy. A single-step migration will either break the running
application or block the database.

---

## The core principle

At the moment you apply a migration, **two versions of your application are running**:
the old version (still serving traffic) and the new version (starting up).
The database schema must be valid for both simultaneously.

This means:
- You cannot remove a column the old code still reads.
- You cannot rename a column the old code still writes.
- You cannot add a non-nullable column without a default, because the old code's INSERTs won't include it.

---

## Expand / Contract pattern (required for all breaking changes)

Every breaking schema change follows three phases:

```
Phase 1 — EXPAND    Add new structure alongside old. Both versions work.
Phase 2 — MIGRATE   Move code to use new structure. Deploy new version.
Phase 3 — CONTRACT  Remove old structure. Safe because new code no longer uses it.
```

Each phase is a separate deployment. Phases 1 and 3 are schema migrations.
Phase 2 is a code change.

---

## Pattern 1: Add a non-nullable column

### Problem
Old code INSERTs rows without the new column → constraint violation.

### Solution — three steps

**Step 1 (Schema migration):** Add as nullable with a server default.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "IsVerified",
        table: "Users",
        nullable: true,
        defaultValue: null);

    // Backfill existing rows so future NOT NULL constraint succeeds
    migrationBuilder.Sql("""
        UPDATE "Users" SET "IsVerified" = FALSE WHERE "IsVerified" IS NULL;
        """);
}
```

**Step 2 (Code change):** Update application code to write the new column.
Deploy this version. Old pods rolling off, new pods writing the column.

**Step 3 (Schema migration):** Now that all code writes the column, enforce NOT NULL.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AlterColumn<bool>(
        name: "IsVerified",
        table: "Users",
        nullable: false,
        defaultValue: false,
        oldClrType: typeof(bool?),
        oldNullable: true);
}
```

---

## Pattern 2: Rename a column

### Problem
Old code reads `OldName`; new code reads `NewName`. During the rolling deploy,
both names must work simultaneously.

### Solution — four steps

**Step 1 (Schema migration):** Add the new column alongside the old.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Add new column, copy data
    migrationBuilder.AddColumn<string>(
        name: "DisplayName",
        table: "Users",
        nullable: true);

    migrationBuilder.Sql("""
        UPDATE "Users" SET "DisplayName" = "FullName";
        """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "DisplayName", table: "Users");
}
```

**Step 2 (Code change):** Write to BOTH columns; read from the new column.
Deploy. All pods now write both columns and read the new name.

**Step 3 (Code change):** Remove writes to the old column.
Deploy. All pods now ignore the old column entirely.

**Step 4 (Schema migration):** Drop the old column.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "FullName", table: "Users");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>(name: "FullName", table: "Users", nullable: true);
    migrationBuilder.Sql("""UPDATE "Users" SET "FullName" = "DisplayName";""");
}
```

> **Never use `RenameColumn` for a zero-downtime rename.** `RenameColumn` is a
> single atomic step — old code breaks the moment it runs.
> Use it only for offline maintenance windows.

---

## Pattern 3: Change a column's type

### Problem
Type change may require an implicit cast that the old code or DB engine cannot perform.

### Solution — same as rename (add new, migrate, drop old)

**Step 1:** Add a new column with the target type alongside the old.

```csharp
// Old: Status NVARCHAR(50)  →  New: StatusId INT (FK to Statuses table)
migrationBuilder.AddColumn<int>(
    name: "StatusId",
    table: "Orders",
    nullable: true);
```

**Step 2 (Data migration):** Populate the new column from the old.

```csharp
// Separate data migration
migrationBuilder.Sql("""
    UPDATE "Orders" SET "StatusId" = CASE "Status"
        WHEN 'Pending'   THEN 1
        WHEN 'Confirmed' THEN 2
        WHEN 'Shipped'   THEN 3
        WHEN 'Delivered' THEN 4
        ELSE 5
    END;
    """);
```

**Step 3 (Code change):** Update code to use `StatusId`. Deploy.

**Step 4 (Schema migration):** Drop old `Status` column.

---

## Pattern 4: Drop a column or table

### Problem
Old code still queries the column/table. Dropping it immediately breaks the running app.

### Solution — two deploys

**Deploy 1 (Code change):** Remove all reads, writes, includes, and mappings
that reference the column. This is a pure code change — no migration.
After this deploy, no running code touches the column.

**Deploy 2 (Schema migration):** Now it is safe to drop.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "LegacyNotes", table: "Orders");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Restore the column (data cannot be recovered — document this)
    migrationBuilder.AddColumn<string>(
        name: "LegacyNotes",
        table: "Orders",
        nullable: true);
}
```

> **`Down()` on a DROP cannot restore data.** Document this explicitly in a
> comment on the migration and in the deployment runbook.

---

## Pattern 5: Add a unique constraint to an existing column

### Problem
Existing data may have duplicates. Adding the constraint fails if duplicates exist.
The `ALTER TABLE` also locks the table while scanning for violations.

### Solution

**Step 1:** Find and resolve duplicates in a data migration (do not use EF Core
index creation for this — write explicit SQL).

```csharp
// Data migration — deduplication
migrationBuilder.Sql("""
    -- Keep the most recently created record for each email
    DELETE FROM "Users"
    WHERE "Id" NOT IN (
        SELECT MIN("Id")
        FROM "Users"
        GROUP BY "Email"
    );
    """);
```

**Step 2:** Add the unique index (non-blocking where supported).

```csharp
// PostgreSQL — CONCURRENTLY avoids locking
migrationBuilder.Sql("""
    CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "ix_users_email_unique"
    ON "Users" ("Email");
    """);
```

```csharp
// SQL Server — ONLINE minimizes locking
migrationBuilder.Sql("""
    CREATE UNIQUE INDEX ix_Users_Email ON Users (Email)
    WITH (ONLINE = ON, IGNORE_DUP_KEY = OFF);
    """);
```

---

## Pattern 6: Split a column (e.g. FullName → FirstName + LastName)

**Step 1 (Schema migration):** Add both new columns as nullable.

```csharp
migrationBuilder.AddColumn<string>("FirstName", "Users", nullable: true);
migrationBuilder.AddColumn<string>("LastName",  "Users", nullable: true);
```

**Step 2 (Data migration):** Populate from the old column.

```csharp
migrationBuilder.Sql("""
    UPDATE "Users"
    SET
        "FirstName" = SPLIT_PART("FullName", ' ', 1),
        "LastName"  = NULLIF(TRIM(SUBSTR("FullName", STRPOS("FullName", ' '))), '');
    """);
```

**Step 3 (Code change):** Update code to read `FirstName` + `LastName`. Write all three during transition.

**Step 4 (Schema migration):** Make new columns non-nullable. Drop old column.

---

## Deployment sequence checklist

For any production schema change:

- [ ] Generate idempotent SQL script and have it reviewed by a second person.
- [ ] Apply migration to a staging environment with a **production-sized dataset**.
- [ ] Measure migration duration on staging — plan a deployment window if > 30 seconds.
- [ ] Test rollback on staging — apply migration, roll back, verify schema is unchanged.
- [ ] For PostgreSQL: confirm indexes are created with `CONCURRENTLY` where applicable.
- [ ] For SQL Server: confirm `ALTER TABLE` operations use `ONLINE = ON` where applicable.
- [ ] Confirm no table-level locks will block traffic for more than 100ms.
- [ ] Apply migration to production **before** deploying the new application code
      (Expand phase) or **after** (Contract phase) — never simultaneously with the code deploy.

---

## Monitoring during migration

Know these before you apply a migration to production:

```sql
-- PostgreSQL: watch for locks
SELECT
    pid, state, wait_event_type, wait_event,
    LEFT(query, 100) AS query_start
FROM pg_stat_activity
WHERE wait_event_type = 'Lock';

-- PostgreSQL: watch long-running queries that could block migration
SELECT pid, now() - query_start AS duration, LEFT(query, 100)
FROM pg_stat_activity
WHERE state = 'active'
  AND now() - query_start > INTERVAL '30 seconds';

-- SQL Server: watch blocking
SELECT
    r.session_id, r.blocking_session_id,
    r.wait_type, r.wait_time,
    LEFT(t.text, 100) AS query_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.blocking_session_id > 0;
```

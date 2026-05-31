# Data Migrations

Data migrations transform, move, backfill, or clean up existing rows.
They are always separate from schema migrations and follow different rules.

---

## Why separate schema from data

| Concern | Schema migration | Data migration |
|---|---|---|
| Purpose | Change structure | Transform rows |
| Transaction safety | Usually safe in a transaction | Long-running — transaction may time out or lock the table |
| Rollback | `Down()` reverses the DDL | Data changes may be irreversible — must be planned explicitly |
| Performance impact | Usually fast (DDL) | Potentially slow (millions of rows) |
| Idempotency | EF Core handles via `__EFMigrationsHistory` | Must be explicitly written to be idempotent |

**Rule: one migration file does one thing. Never mix `AddColumn` with `UPDATE` rows.**

---

## Structure of a data migration

```csharp
/// <summary>
/// Backfills IsVerified = FALSE for all existing users created before
/// the email verification feature was introduced (2024-06-01).
/// This migration is safe to re-run — the WHERE clause is idempotent.
/// </summary>
public partial class BackfillIsVerifiedForExistingUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Always:
        // 1. Comment describing what data is changing and why
        // 2. Idempotent SQL (safe to run twice — uses WHERE IS NULL or similar guard)
        // 3. Batched for large tables

        migrationBuilder.Sql("""
            UPDATE "Users"
            SET "IsVerified" = FALSE
            WHERE "IsVerified" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data migrations are often irreversible.
        // Document this explicitly — do not leave Down() empty or throwing.
        migrationBuilder.Sql("""
            -- Intentionally not reversible: setting IsVerified back to NULL
            -- would re-run the backfill on the next Up(), which is acceptable.
            UPDATE "Users"
            SET "IsVerified" = NULL;
            """);
    }
}
```

---

## Idempotency

Every data migration SQL must be safe to run more than once.
This is required for `--idempotent` scripts and for re-applying after a failed deploy.

```sql
-- NOT idempotent — running twice doubles the price
UPDATE "Products" SET "Price" = "Price" * 1.1;

-- Idempotent — uses a flag or date guard
UPDATE "Products"
SET "Price" = "Price" * 1.1, "PriceUpdatedAt" = NOW()
WHERE "PriceUpdatedAt" IS NULL;

-- NOT idempotent — INSERT fails on second run if UNIQUE constraint exists
INSERT INTO "Roles" ("Name") VALUES ('Admin');

-- Idempotent — INSERT OR IGNORE / ON CONFLICT DO NOTHING
INSERT INTO "Roles" ("Name") VALUES ('Admin')
ON CONFLICT ("Name") DO NOTHING;
```

---

## Batched updates for large tables

Updating millions of rows in a single statement acquires a long-held table lock
and may time out. Break large updates into batches.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // PostgreSQL — batch update using ctid or a sequential id
    migrationBuilder.Sql("""
        DO $$
        DECLARE
            v_batch_size INT := 5000;
            v_offset     INT := 0;
            v_rows       INT;
        BEGIN
            LOOP
                UPDATE "Orders"
                SET "ProcessedAt" = "CreatedAt"
                WHERE "ProcessedAt" IS NULL
                  AND "Id" IN (
                      SELECT "Id" FROM "Orders"
                      WHERE "ProcessedAt" IS NULL
                      LIMIT v_batch_size
                  );

                GET DIAGNOSTICS v_rows = ROW_COUNT;
                EXIT WHEN v_rows = 0;

                PERFORM pg_sleep(0.05); -- brief pause between batches
            END LOOP;
        END $$;
        """);
}
```

```csharp
// SQL Server — batch update with TOP
migrationBuilder.Sql("""
    DECLARE @BatchSize INT = 5000;
    DECLARE @Rows INT = 1;

    WHILE @Rows > 0
    BEGIN
        UPDATE TOP (@BatchSize) Orders
        SET ProcessedAt = CreatedAt
        WHERE ProcessedAt IS NULL;

        SET @Rows = @@ROWCOUNT;

        WAITFOR DELAY '00:00:00.050'; -- 50ms pause between batches
    END;
    """);
```

---

## Transformation patterns

### Splitting a column

```sql
-- Split "FullName" into "FirstName" + "LastName"
-- Works for "FirstName LastName" format; adjust for your data shape
UPDATE "Users"
SET
    "FirstName" = TRIM(SPLIT_PART("FullName", ' ', 1)),
    "LastName"  = NULLIF(TRIM(SUBSTRING("FullName" FROM POSITION(' ' IN "FullName") + 1)), '')
WHERE "FirstName" IS NULL;  -- idempotency guard
```

### Normalizing an enum stored as string

```sql
-- Migrate from free-text Status to canonical values
UPDATE "Orders"
SET "Status" = CASE LOWER(TRIM("Status"))
    WHEN 'pending'   THEN 'Pending'
    WHEN 'confirmed' THEN 'Confirmed'
    WHEN 'shipped'   THEN 'Shipped'
    WHEN 'done'      THEN 'Delivered'   -- old alias
    WHEN 'complete'  THEN 'Delivered'   -- another old alias
    ELSE "Status"   -- leave unknown values unchanged — investigate separately
END
WHERE "Status" NOT IN ('Pending', 'Confirmed', 'Shipped', 'Delivered', 'Cancelled');
```

### Moving data between tables (extract to separate table)

```sql
-- Step 1: Copy data to new table (schema migration creates the table first)
INSERT INTO "ShippingAddresses" ("Id", "OrderId", "Line1", "Line2", "PostalCode", "City")
SELECT
    gen_random_uuid(),
    "Id",
    "ShippingLine1",
    "ShippingLine2",
    "ShippingPostalCode",
    "ShippingCity"
FROM "Orders"
WHERE "ShippingLine1" IS NOT NULL
ON CONFLICT DO NOTHING;  -- idempotency

-- Step 2: Verify row counts match before the code deploy
-- (run this manually before proceeding)
-- SELECT COUNT(*) FROM "Orders" WHERE "ShippingLine1" IS NOT NULL;
-- SELECT COUNT(*) FROM "ShippingAddresses";
```

### Deduplication before adding a unique constraint

```sql
-- Keep the most recently created record; delete duplicates
DELETE FROM "Users"
WHERE "Id" NOT IN (
    SELECT MAX("Id")      -- or MIN, or MAX("CreatedAt") depending on which to keep
    FROM "Users"
    GROUP BY LOWER("Email")
);

-- Verify no duplicates remain before creating the index
-- SELECT LOWER("Email"), COUNT(*) FROM "Users" GROUP BY LOWER("Email") HAVING COUNT(*) > 1;
```

---

## Seeding reference/lookup data

Reference data (roles, permission codes, country codes) belongs in a dedicated
migration, not in test seeds or application startup code.

```csharp
public partial class SeedDefaultRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "Roles" ("Id", "Name", "IsSystem")
            VALUES
                ('00000000-0000-0000-0000-000000000001', 'Admin',  TRUE),
                ('00000000-0000-0000-0000-000000000002', 'User',   TRUE),
                ('00000000-0000-0000-0000-000000000003', 'Viewer', TRUE)
            ON CONFLICT ("Name") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Roles"
            WHERE "IsSystem" = TRUE
              AND "Name" IN ('Admin', 'User', 'Viewer');
            """);
    }
}
```

---

## Data migration checklist

Before committing a data migration:

- [ ] SQL is idempotent — safe to run twice without changing the result.
- [ ] Large tables use batched updates, not a single UPDATE of all rows.
- [ ] `Down()` is implemented and documented. If truly irreversible, say so in a comment.
- [ ] Migration is **separate** from any schema migration.
- [ ] Migration is named clearly: `BackfillIsVerifiedForExistingUsers`, `NormalizeOrderStatusValues`, `SeedDefaultRoles`.
- [ ] Row counts verified on staging before applying to production.
- [ ] Migration tested on a copy of the production database — not just an empty dev schema.
- [ ] Estimated duration measured on staging with production data volume.

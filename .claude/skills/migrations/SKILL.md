---
name: dotnet-migrations
description: >
  Write and review EF Core migrations safely for .NET 10 projects. Trigger this skill
  whenever the user asks to add a migration, modify a schema, rename a column, add an index,
  handle a data migration, plan a rollback, or deploy a schema change to production.
  Also trigger for questions about zero-downtime deployments, breaking vs non-breaking changes,
  migration squashing, multi-tenant migrations, or any time the user says "dotnet ef migrations add",
  "schema change", "rename column", "add column", or "deploy migration".
---

# EF Core Migrations

Author, review, and deploy EF Core migrations safely across all environments,
with explicit strategies for data migrations, rollbacks, and zero-downtime deployments.

## Before writing any migration

1. **Classify the change** — is it additive, breaking, or destructive? See the classification table below.
2. **Read `references/migration-checklist.md`** — run every item before committing.
3. **Check if a zero-downtime strategy is required** — any change that touches a column read or written by live code needs one. See `references/zero-downtime.md`.
4. **Decide if data must move** — data migrations are always separate from schema migrations. See `references/data-migrations.md`.

---

## Change classification

Classify every migration before writing it. The class determines the deployment strategy.

| Class | Definition | Can deploy immediately? |
|---|---|---|
| **Additive** | New nullable column, new table, new index | Yes — old code ignores new columns |
| **Additive breaking** | New non-nullable column without default | No — old code INSERT fails |
| **Rename** | Column or table rename | No — old code reads wrong name |
| **Type change** | Column type or size change | No — implicit casts may fail |
| **Destructive** | DROP column, DROP table | No — old code breaks immediately |
| **Data migration** | Moving or transforming existing data | Separate step — never mixed with schema |

---

## Golden rules

1. **Never mix schema and data migrations** — a schema migration changes structure only; a data migration transforms rows only.
2. **Always implement `Down()`** — every migration must be reversible. No exceptions.
3. **Never edit a committed migration** — once a migration has been applied to any shared environment (staging, production), it is immutable. Create a new migration to fix it.
4. **Preview SQL before applying** — run `dotnet ef migrations script` and read the output before touching any non-dev environment.
5. **Destructive changes require two deploys** — remove the code that uses the column first, then drop the column in the next release.
6. **Test rollback before every production deployment** — apply the migration on a copy of the production DB, then roll it back, and verify the schema is identical to the pre-migration state.

---

## Workflow

```
1. Make model change
        ↓
2. dotnet ef migrations add <MigrationName> --project Src/MyApp.Infrastructure
        ↓
3. Review the generated migration — never accept generated code blindly
        ↓
4. dotnet ef migrations script <From> <To> --idempotent > review.sql
        ↓
5. Read review.sql — does it do exactly what you expect?
        ↓
6. Run migration-checklist.md
        ↓
7. Apply to dev: dotnet ef database update
        ↓
8. Commit migration + model snapshot together (never separately)
        ↓
9. Apply to staging → verify → apply to production
```

---

## Migration naming conventions

Names must describe the structural change, not a ticket number.

```
GOOD:
  AddEmailVerifiedToUsers
  AddOrderStatusIndex
  AddShippingAddressToOrders_Nullable
  RenameCustomerIdToClientId_Orders   ← include both names for renames
  DropLegacyAuditLogTable             ← always prefix drops with "Drop"
  SplitFullNameIntoFirstAndLastName

BAD:
  Update
  Fix
  JIRA-1234
  NewMigration
  Changes
```

---

## Anatomy of a safe migration

```csharp
/// <inheritdoc />
public partial class AddEmailVerifiedToUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Additive changes first (add column as nullable)
        migrationBuilder.AddColumn<bool>(
            name: "EmailVerified",
            table: "Users",
            type: "boolean",
            nullable: true,            // always nullable on first add
            defaultValue: null);

        // 2. Backfill existing rows (if the column will later be made non-nullable)
        // NOTE: if backfill is expensive, do it in a separate data migration
        migrationBuilder.Sql("""
            UPDATE "Users"
            SET "EmailVerified" = FALSE
            WHERE "EmailVerified" IS NULL;
            """);

        // 3. Apply constraint only after backfill
        migrationBuilder.AlterColumn<bool>(
            name: "EmailVerified",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false,
            oldClrType: typeof(bool?),
            oldType: "boolean",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Always the exact inverse of Up()
        migrationBuilder.DropColumn(
            name: "EmailVerified",
            table: "Users");
    }
}
```

---

## Output format

When producing a migration, always output in this order:

1. **Classification** — additive / breaking / rename / destructive / data
2. **Zero-downtime strategy** — required or not, and why
3. **Migration file(s)** — schema migration first, data migration second if needed
4. **Down() verification** — confirm it exactly undoes Up()
5. **Deployment steps** — numbered, in order
6. **Rollback steps** — numbered, in order

---

## Reference files

| File | When to read |
|---|---|
| `references/migration-checklist.md` | Before committing any migration |
| `references/zero-downtime.md` | Any change to a column used by live code |
| `references/data-migrations.md` | Any migration that moves or transforms rows |
| `references/rollback-strategies.md` | Planning production deployments and disaster recovery |

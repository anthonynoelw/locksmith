namespace Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

/// <inheritdoc />
public partial class AddAppendOnlyConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        string databaseUser = MigrationExtensions.GetDatabaseUser();
        migrationBuilder.Sql($"COMMENT ON TABLE api_keys IS 'Append-only table - owner: {databaseUser}';");
        migrationBuilder.Sql($"COMMENT ON TABLE api_key_statuses IS 'Append-only table - owner: {databaseUser}';");
        migrationBuilder.Sql($"COMMENT ON TABLE api_key_actions IS 'Append-only table - owner: {databaseUser}';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("COMMENT ON TABLE api_keys IS NULL;");
        migrationBuilder.Sql("COMMENT ON TABLE api_key_statuses IS NULL;");
        migrationBuilder.Sql("COMMENT ON TABLE api_key_actions IS NULL;");
    }
}

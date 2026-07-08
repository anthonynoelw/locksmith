#nullable disable

namespace Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <inheritdoc />
public partial class AddActiveActionUniqueIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ApiKeyActions_ApiKeyId_Action_Active",
            table: "ApiKeyActions",
            columns: new[] { "ApiKeyId", "Action" },
            unique: true,
            filter: "\"DeletedAt\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ApiKeyActions_ApiKeyId_Action_Active",
            table: "ApiKeyActions");
    }
}

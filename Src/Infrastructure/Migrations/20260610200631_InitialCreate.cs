namespace Infrastructure.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                Secret = table.Column<string>(type: "text", nullable: false),
                Salt = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ApiKeyActions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeyActions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiKeyActions_ApiKeys_ApiKeyId",
                    column: x => x.ApiKeyId,
                    principalTable: "ApiKeys",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApiKeyStatuses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeyStatuses", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiKeyStatuses_ApiKeys_ApiKeyId",
                    column: x => x.ApiKeyId,
                    principalTable: "ApiKeys",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeyActions_ApiKeyId",
            table: "ApiKeyActions",
            column: "ApiKeyId");

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_IdempotencyKey",
            table: "ApiKeys",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeyStatuses_ApiKeyId_CreatedAt",
            table: "ApiKeyStatuses",
            columns: new[] { "ApiKeyId", "CreatedAt" },
            descending: new[] { false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiKeyActions");

        migrationBuilder.DropTable(
            name: "ApiKeyStatuses");

        migrationBuilder.DropTable(
            name: "ApiKeys");
    }
}

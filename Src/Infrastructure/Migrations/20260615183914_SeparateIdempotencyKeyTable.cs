namespace Infrastructure.Migrations;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

    /// <inheritdoc />
    public partial class SeparateIdempotencyKeyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_IdempotencyKeyHash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Salt",
                table: "ApiKeys");

            migrationBuilder.AddColumn<string>(
                name: "SecretHash",
                table: "ApiKeys",
                type: "text",
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "text", nullable: false),
                    Salt = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_SecretHash",
                table: "ApiKeys",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ApiKeyId",
                table: "IdempotencyKeys",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_IdempotencyKeyHash",
                table: "IdempotencyKeys",
                column: "IdempotencyKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_SecretHash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "SecretHash",
                table: "ApiKeys");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                table: "ApiKeys",
                type: "text",
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "Salt",
                table: "ApiKeys",
                type: "text",
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_IdempotencyKeyHash",
                table: "ApiKeys",
                column: "IdempotencyKeyHash",
                unique: true);
        }
    }

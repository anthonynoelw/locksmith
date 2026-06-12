using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameIdempotencyKeyColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_IdempotencyKey",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ApiKeys");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKeyHash",
                table: "ApiKeys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_IdempotencyKeyHash",
                table: "ApiKeys",
                column: "IdempotencyKeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_IdempotencyKeyHash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "IdempotencyKeyHash",
                table: "ApiKeys");

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "ApiKeys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_IdempotencyKey",
                table: "ApiKeys",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}

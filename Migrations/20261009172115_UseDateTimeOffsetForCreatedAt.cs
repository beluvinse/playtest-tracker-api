using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaytestTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class UseDateTimeOffsetForCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows written before this migration hold Argentina local time (UTC-3, no DST). Shift them to UTC
            // so that, once the column carries an offset, they are stored as +00:00 like the new ones.
            migrationBuilder.Sql("UPDATE Projects SET CreatedAt = DATEADD(hour, 3, CreatedAt)");
            migrationBuilder.Sql("UPDATE Bugs SET CreatedAt = DATEADD(hour, 3, CreatedAt)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Projects",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Bugs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Projects",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Bugs",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.Sql("UPDATE Projects SET CreatedAt = DATEADD(hour, -3, CreatedAt)");
            migrationBuilder.Sql("UPDATE Bugs SET CreatedAt = DATEADD(hour, -3, CreatedAt)");
        }
    }
}

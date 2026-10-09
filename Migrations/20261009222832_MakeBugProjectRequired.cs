using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaytestTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeBugProjectRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bugs without a project are leftover test data; every bug must belong to a project now
            migrationBuilder.Sql("DELETE FROM Bugs WHERE ProjectId IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "Bugs",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ProjectId",
                table: "Bugs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}

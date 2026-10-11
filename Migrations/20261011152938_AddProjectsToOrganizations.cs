using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaytestTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsToOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Code",
                table: "Projects");

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_Code",
                table: "Projects",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId_Code",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);
        }
    }
}

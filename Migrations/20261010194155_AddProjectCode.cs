using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaytestTracker.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF scaffolded this as NOT NULL with defaultValue "", which would give every existing
            // project the same empty code and make the unique index below fail. So, in three steps:

            // 1. Add the column allowing NULL, so existing rows can exist without a code for a moment
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Projects",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            // 2. Give every existing project a code
            migrationBuilder.Sql(@"
                -- The first 3 characters of the name without spaces: 'Pink in the Night' -> 'PIN'
                UPDATE Projects SET Code = UPPER(LEFT(REPLACE(Name, ' ', ''), 3));

                -- Fix, one project at a time (oldest first), every code that is not 3 letters
                -- (a digit, a symbol, a short name) or that an older project already has.
                -- Each one gets the first free code in alphabetical order: AAA, AAB, AAC...
                DECLARE @id int, @n int, @code nvarchar(3);

                WHILE EXISTS (
                    SELECT 1 FROM Projects p
                    WHERE p.Code LIKE '%[^A-Z]%' OR LEN(p.Code) <> 3
                       OR EXISTS (SELECT 1 FROM Projects o WHERE o.Code = p.Code AND o.Id < p.Id))
                BEGIN
                    SELECT TOP 1 @id = p.Id FROM Projects p
                    WHERE p.Code LIKE '%[^A-Z]%' OR LEN(p.Code) <> 3
                       OR EXISTS (SELECT 1 FROM Projects o WHERE o.Code = p.Code AND o.Id < p.Id)
                    ORDER BY p.Id;

                    -- @n counts AAA = 0, AAB = 1 ... ZZZ = 17575, written as three letters
                    SET @n = 0;
                    SET @code = 'AAA';
                    WHILE EXISTS (SELECT 1 FROM Projects WHERE Code = @code)
                    BEGIN
                        SET @n = @n + 1;
                        SET @code = CHAR(65 + @n / 676 % 26) + CHAR(65 + @n / 26 % 26) + CHAR(65 + @n % 26);
                    END

                    UPDATE Projects SET Code = @code WHERE Id = @id;
                END
            ");

            // 3. Now every row has a unique code: make it required and unique
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Projects",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Code",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Projects");
        }
    }
}

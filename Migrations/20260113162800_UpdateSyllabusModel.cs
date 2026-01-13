using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSyllabusModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMiniProject",
                table: "SyllabusModules",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMiniProject",
                table: "SyllabusModules");
        }
    }
}

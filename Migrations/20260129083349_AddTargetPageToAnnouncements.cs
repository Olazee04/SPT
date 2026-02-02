using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetPageToAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetPage",
                table: "Announcements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetPage",
                table: "Announcements");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetPageToNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetPage",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Announcements",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetPage",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Announcements");
        }
    }
}

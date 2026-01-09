using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorDateJoined : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProjectMilestone",
                table: "ProgressLogs",
                newName: "VerifiedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Mentors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateJoined",
                table: "Mentors",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "NextOfKin",
                table: "Mentors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextOfKinPhone",
                table: "Mentors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Mentors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePicture",
                table: "Mentors",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Mentors");

            migrationBuilder.DropColumn(
                name: "DateJoined",
                table: "Mentors");

            migrationBuilder.DropColumn(
                name: "NextOfKin",
                table: "Mentors");

            migrationBuilder.DropColumn(
                name: "NextOfKinPhone",
                table: "Mentors");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Mentors");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                table: "Mentors");

            migrationBuilder.RenameColumn(
                name: "VerifiedByUserId",
                table: "ProgressLogs",
                newName: "ProjectMilestone");
        }
    }
}

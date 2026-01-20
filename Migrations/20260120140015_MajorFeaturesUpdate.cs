using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class MajorFeaturesUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IPAddress",
                table: "AuditLogs",
                newName: "IpAddress");

            migrationBuilder.RenameColumn(
                name: "EditedAt",
                table: "AuditLogs",
                newName: "Timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_EditedAt",
                table: "AuditLogs",
                newName: "IX_AuditLogs_Timestamp");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PerformedBy",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Capstones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GitHubUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LiveUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalCount = table.Column<int>(type: "int", nullable: false),
                    MentorFeedback = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capstones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Capstones_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Capstones_StudentId",
                table: "Capstones",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Capstones");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PerformedBy",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "AuditLogs",
                newName: "IPAddress");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "AuditLogs",
                newName: "EditedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                newName: "IX_AuditLogs_EditedAt");

            migrationBuilder.AlterColumn<string>(
                name: "IPAddress",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}

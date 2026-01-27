using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class AddCapstoneReviewAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalCount",
                table: "Capstones");

            migrationBuilder.RenameColumn(
                name: "LiveUrl",
                table: "Capstones",
                newName: "RepositoryUrl");

            migrationBuilder.RenameColumn(
                name: "GitHubUrl",
                table: "Capstones",
                newName: "LiveDemoUrl");

            migrationBuilder.AddColumn<string>(
                name: "CertificateCode",
                table: "Certificates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAt",
                table: "Certificates",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Capstones",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Capstones",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Capstones",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Capstones",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateCode",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Capstones");

            migrationBuilder.RenameColumn(
                name: "RepositoryUrl",
                table: "Capstones",
                newName: "LiveUrl");

            migrationBuilder.RenameColumn(
                name: "LiveDemoUrl",
                table: "Capstones",
                newName: "GitHubUrl");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Capstones",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Capstones",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Capstones",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalCount",
                table: "Capstones",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}

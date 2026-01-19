using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SPT.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleIdToResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModuleId",
                table: "Resources",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ModuleId",
                table: "Resources",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_SyllabusModules_ModuleId",
                table: "Resources",
                column: "ModuleId",
                principalTable: "SyllabusModules",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Resources_SyllabusModules_ModuleId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Resources_ModuleId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                table: "Resources");
        }
    }
}

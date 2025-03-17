using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanYonetimAraclari.Migrations
{
    public partial class AddTaskAssignedMember : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedMemberId",
                table: "Tasks",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedMemberId",
                table: "Tasks",
                column: "AssignedMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_AspNetUsers_AssignedMemberId",
                table: "Tasks",
                column: "AssignedMemberId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_AspNetUsers_AssignedMemberId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AssignedMemberId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AssignedMemberId",
                table: "Tasks");
        }
    }
}

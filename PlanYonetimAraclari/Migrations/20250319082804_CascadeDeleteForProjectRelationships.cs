using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanYonetimAraclari.Migrations
{
    public partial class CascadeDeleteForProjectRelationships : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectInvitations_Projects_ProjectId",
                table: "ProjectInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTeamMembers_Projects_ProjectId",
                table: "ProjectTeamMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectInvitations_Projects_ProjectId",
                table: "ProjectInvitations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTeamMembers_Projects_ProjectId",
                table: "ProjectTeamMembers",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectInvitations_Projects_ProjectId",
                table: "ProjectInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTeamMembers_Projects_ProjectId",
                table: "ProjectTeamMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectInvitations_Projects_ProjectId",
                table: "ProjectInvitations",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTeamMembers_Projects_ProjectId",
                table: "ProjectTeamMembers",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }
    }
}

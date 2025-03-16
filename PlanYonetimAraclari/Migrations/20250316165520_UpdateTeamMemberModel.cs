using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanYonetimAraclari.Migrations
{
    public partial class UpdateTeamMemberModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JoinedDate",
                table: "ProjectTeamMembers",
                newName: "InvitedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedAt",
                table: "ProjectTeamMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProjectTeamMembers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "ProjectTeamMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProjectTeamMembers");

            migrationBuilder.RenameColumn(
                name: "InvitedAt",
                table: "ProjectTeamMembers",
                newName: "JoinedDate");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanYonetimAraclari.Migrations
{
    public partial class AddUserLimitsColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxMembersPerProject",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "MaxProjectsAllowed",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 3);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxMembersPerProject",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MaxProjectsAllowed",
                table: "AspNetUsers");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanYonetimAraclari.Migrations
{
    public partial class UpdateUserLimitsDefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing users with MaxProjectsAllowed = 0 to have MaxProjectsAllowed = 3
            migrationBuilder.Sql("UPDATE AspNetUsers SET MaxProjectsAllowed = 3 WHERE MaxProjectsAllowed = 0");
            
            // Update existing users with MaxMembersPerProject = 0 to have MaxMembersPerProject = 10
            migrationBuilder.Sql("UPDATE AspNetUsers SET MaxMembersPerProject = 10 WHERE MaxMembersPerProject = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No down migration needed since we're fixing data
        }
    }
}

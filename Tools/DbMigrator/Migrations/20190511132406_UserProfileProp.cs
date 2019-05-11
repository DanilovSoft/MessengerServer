using Microsoft.EntityFrameworkCore.Migrations;

namespace DbMigrator.Migrations
{
    public partial class UserProfileProp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "UserProfiles",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "UserProfiles",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "UserProfiles");
        }
    }
}

using System;
using DbMigrator.Extensions;
using DbModel.DbTypes;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DbMigrator.Migrations
{
    public partial class CreateUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:gender", "undefined,male,female")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false),
                    CreatedUtc = table.Column<DateTime>(nullable: false),
                    UpdatedUtc = table.Column<DateTime>(nullable: false),
                    Login = table.Column<string>(maxLength: 32, nullable: false),
                    NormalLogin = table.Column<string>(maxLength: 32, nullable: false),
                    Password = table.Column<string>(maxLength: 60, nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Users", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false),
                    Gender = table.Column<Gender>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_Id",
                        column: x => x.Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedUtc",
                table: "Users",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalLogin",
                table: "Users",
                column: "NormalLogin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UpdatedUtc",
                table: "Users",
                column: "UpdatedUtc");

            CreateUserExpansion.Up(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:Enum:gender", "undefined,male,female")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            CreateUserExpansion.Down(migrationBuilder);
        }
    }
}
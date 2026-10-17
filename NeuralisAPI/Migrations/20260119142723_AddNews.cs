using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralisAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DoctorProfiles_UserId",
                table: "DoctorProfiles");

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorProfiles_UserId",
                table: "DoctorProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropIndex(
                name: "IX_DoctorProfiles_UserId",
                table: "DoctorProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorProfiles_UserId",
                table: "DoctorProfiles",
                column: "UserId");
        }
    }
}

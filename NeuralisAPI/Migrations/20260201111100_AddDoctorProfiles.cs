using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralisAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "DoctorProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "DoctorProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByAdminId",
                table: "DoctorProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DoctorProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "ReviewedByAdminId",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DoctorProfiles");
        }
    }
}

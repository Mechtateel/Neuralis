using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralisAPI.Migrations
{
    /// <inheritdoc />
    public partial class PatientLabAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientLabAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileBytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientLabAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientLabAttachments_DoctorProfiles_UploadedByDoctorProfil~",
                        column: x => x.UploadedByDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientLabAttachments_Users_PatientUserId",
                        column: x => x.PatientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabAttachments_PatientUserId",
                table: "PatientLabAttachments",
                column: "PatientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientLabAttachments_UploadedByDoctorProfileId",
                table: "PatientLabAttachments",
                column: "UploadedByDoctorProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientLabAttachments");
        }
    }
}

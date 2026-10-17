using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralisAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientLabAttachmentTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "PatientLabAttachments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "PatientLabAttachments");
        }
    }
}

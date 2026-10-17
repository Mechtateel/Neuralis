using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeuralisAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsContentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "News",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "News",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "News",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "general");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "News",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "News",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "News",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Neuralis Admin");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorId",
                table: "News",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "News",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "News",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "News",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "News",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "News",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Views",
                table: "News",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: any pre-existing row with IsPublished=true gets PublishedAt = CreatedAt
            // so the new patient feed sort behaves predictably for legacy content.
            migrationBuilder.Sql(@"
                UPDATE ""News""
                SET ""PublishedAt"" = ""CreatedAt"",
                    ""Status"" = 1
                WHERE ""IsPublished"" = TRUE;

                UPDATE ""News""
                SET ""Status"" = 0
                WHERE ""IsPublished"" = FALSE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Subtitle", table: "News");
            migrationBuilder.DropColumn(name: "Summary", table: "News");
            migrationBuilder.DropColumn(name: "Category", table: "News");
            migrationBuilder.DropColumn(name: "Tags", table: "News");
            migrationBuilder.DropColumn(name: "CoverImageUrl", table: "News");
            migrationBuilder.DropColumn(name: "AuthorName", table: "News");
            migrationBuilder.DropColumn(name: "AuthorId", table: "News");
            migrationBuilder.DropColumn(name: "Status", table: "News");
            migrationBuilder.DropColumn(name: "Priority", table: "News");
            migrationBuilder.DropColumn(name: "IsFeatured", table: "News");
            migrationBuilder.DropColumn(name: "PublishedAt", table: "News");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "News");
            migrationBuilder.DropColumn(name: "Views", table: "News");
        }
    }
}

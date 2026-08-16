using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TextoExtraidoPersistido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "documents");
        }
    }
}

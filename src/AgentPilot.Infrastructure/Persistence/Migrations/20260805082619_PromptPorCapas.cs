using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromptPorCapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssistantInstructions",
                table: "campaigns");

            // Valor por defecto: un objeto JSON vacío, no una cadena vacía (que no es
            // JSON válido y rompería la lectura de la única fila existente, TeleNova).
            migrationBuilder.AddColumn<string>(
                name: "AssistantPrompt",
                table: "campaigns",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "prompt_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_versions_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_versions_CampaignId_CreatedAtUtc",
                table: "prompt_versions",
                columns: new[] { "CampaignId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prompt_versions");

            migrationBuilder.DropColumn(
                name: "AssistantPrompt",
                table: "campaigns");

            migrationBuilder.AddColumn<string>(
                name: "AssistantInstructions",
                table: "campaigns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampanaEnConversacionYTelemetria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "llm_call_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignName",
                table: "llm_call_logs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_call_logs_CampaignId",
                table: "llm_call_logs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_CampaignId",
                table: "conversations",
                column: "CampaignId");

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_campaigns_CampaignId",
                table: "conversations",
                column: "CampaignId",
                principalTable: "campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_conversations_campaigns_CampaignId",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_llm_call_logs_CampaignId",
                table: "llm_call_logs");

            migrationBuilder.DropIndex(
                name: "IX_conversations_CampaignId",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "llm_call_logs");

            migrationBuilder.DropColumn(
                name: "CampaignName",
                table: "llm_call_logs");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "conversations");
        }
    }
}

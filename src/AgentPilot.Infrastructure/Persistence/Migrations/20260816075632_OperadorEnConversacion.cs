using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperadorEnConversacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "conversations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Relleno del histórico. Hasta ahora el operador solo quedaba registrado en
            // llm_call_logs, que sí guarda ConversationId: se recupera de ahí en vez de
            // dejar en blanco todas las conversaciones anteriores, que son justamente las
            // que ya tienen valoraciones que revisar.
            //
            // Se toma la PRIMERA llamada de cada conversación: una conversación la
            // mantiene un único operador, y si por lo que fuera hubiera varias, la que
            // la abrió es la atribución honesta.
            migrationBuilder.Sql("""
                UPDATE conversations c
                SET "UserName" = sub."UserName"
                FROM (
                    SELECT DISTINCT ON ("ConversationId") "ConversationId", "UserName"
                    FROM llm_call_logs
                    WHERE "ConversationId" IS NOT NULL AND "UserName" IS NOT NULL
                    ORDER BY "ConversationId", "CreatedAtUtc"
                ) AS sub
                WHERE c."Id" = sub."ConversationId" AND c."UserName" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_UserName",
                table: "conversations",
                column: "UserName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_conversations_UserName",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "conversations");
        }
    }
}

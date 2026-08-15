using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentPilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ValoracionUnicaPorMensaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_feedback_MessageId",
                table: "feedback");

            // Hasta ahora nada impedía valorar dos veces el mismo mensaje (el servicio
            // siempre insertaba). Si alguna base de datos arrastra repetidos, el índice
            // único fallaría al crearse y la migración dejaría el despliegue tirado, así
            // que se deduplica antes conservando la valoración MÁS RECIENTE de cada
            // mensaje, que es la que el usuario quiso dejar.
            migrationBuilder.Sql("""
                DELETE FROM feedback
                WHERE "Id" NOT IN (
                    SELECT DISTINCT ON ("MessageId") "Id"
                    FROM feedback
                    ORDER BY "MessageId", "CreatedAtUtc" DESC, "Id"
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_feedback_MessageId",
                table: "feedback",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_feedback_MessageId",
                table: "feedback");

            migrationBuilder.CreateIndex(
                name: "IX_feedback_MessageId",
                table: "feedback",
                column: "MessageId");
        }
    }
}

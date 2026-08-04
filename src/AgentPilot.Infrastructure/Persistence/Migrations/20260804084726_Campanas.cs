using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentPilot.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Introduce las campañas y ata la documentación existente a una de ellas.
    ///
    /// El orden importa: añadir una columna obligatoria a una tabla con filas falla, y
    /// crear la clave foránea antes de rellenar la columna también. Por eso va en tres
    /// pasos —columna anulable, relleno, columna obligatoria— con la campaña de
    /// compatibilidad creada antes. La versión generada automáticamente ponía
    /// Guid.Empty en los documentos existentes y rompía la clave foránea al crearla.
    ///
    /// No hay re-vectorización: al haber un único modelo de embeddings para todo el
    /// sistema, la campaña es metadato del documento y los fragmentos no se tocan.
    /// </summary>
    public partial class Campanas : Migration
    {
        /// <summary>
        /// Campaña que recibe el corpus ya indexado. Guid fijo y no aleatorio para que
        /// la migración sea determinista y reproducible en cualquier entorno.
        /// </summary>
        private const string CampañaTeleNova = "11111111-1111-1111-1111-111111111111";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) La tabla, antes de que nada la referencie.
            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssistantInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                });

            // Índice funcional: el API fluido de EF no puede expresarlo, así que va en
            // SQL. Sin él, "Luz y Gas" y "luz y gas" serían dos campañas distintas y
            // nadie sabría en cuál está la documentación buena.
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_campaigns_Name_lower"" ON campaigns (lower(""Name""));");

            // 2) La campaña de compatibilidad para el corpus que ya existe.
            migrationBuilder.Sql($@"
                INSERT INTO campaigns (""Id"", ""Name"", ""Status"", ""CreatedAtUtc"")
                VALUES ('{CampañaTeleNova}', 'TeleNova', 1, now());");

            // 3) Columna anulable, relleno, y solo entonces obligatoria.
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                $@"UPDATE documents SET ""CampaignId"" = '{CampañaTeleNova}' WHERE ""CampaignId"" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                table: "documents",
                type: "uuid",
                nullable: false);

            // 4) Índices y clave foránea, con la columna ya poblada.
            migrationBuilder.CreateIndex(
                name: "IX_documents_CampaignId",
                table: "documents",
                column: "CampaignId");

            // Un mismo fichero no se repite dentro de una campaña, pero sí puede existir
            // en otra: son corpus independientes.
            migrationBuilder.CreateIndex(
                name: "IX_documents_CampaignId_FileName",
                table: "documents",
                columns: new[] { "CampaignId", "FileName" },
                unique: true);

            // En cascada: eliminar una campaña se lleva su corpus. Que solo se pueda
            // eliminar estando cerrada es regla de negocio y vive en el dominio.
            migrationBuilder.AddForeignKey(
                name: "FK_documents_campaigns_CampaignId",
                table: "documents",
                column: "CampaignId",
                principalTable: "campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_campaigns_CampaignId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_CampaignId_FileName",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_CampaignId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "documents");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_campaigns_Name_lower"";");

            migrationBuilder.DropTable(
                name: "campaigns");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Glorific.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaContaMelhorEnvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contas_melhor_envio",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conta_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    tipo_token = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    escopo = table.Column<string>(type: "text", nullable: true),
                    expira_em_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    atualizado_em_utc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_melhor_envio", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contas_melhor_envio_conta_id",
                table: "contas_melhor_envio",
                column: "conta_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contas_melhor_envio");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "sat_half_kwh",
                table: "meter_energy_summary",
                type: "numeric(16,2)",
                precision: 16,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "campus_demand_15m",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    block_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    load_kw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    solar_kw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    net_kw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tou = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    meters_ok = table.Column<int>(type: "integer", nullable: false),
                    meters_total = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campus_demand_15m", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    regular_kw = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: false),
                    half_peak_kw = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: false),
                    sat_half_kw = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: false),
                    off_peak_kw = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: false),
                    warn_pct = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    shed_aircon_pct = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    shed_fan_pct = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    shed_light_pct = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "demand_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    block_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    demand_kw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    threshold_kw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    contract_kw = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demand_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tariff_rates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    scheme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    season = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    item = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tariff_rates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campus_demand_15m_block_start",
                table: "campus_demand_15m",
                column: "block_start",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_demand_events_block_start_level",
                table: "demand_events",
                columns: new[] { "block_start", "level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_demand_events_occurred_at",
                table: "demand_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_tariff_rates_scheme_season_item",
                table: "tariff_rates",
                columns: new[] { "scheme", "season", "item" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campus_demand_15m");

            migrationBuilder.DropTable(
                name: "contract_settings");

            migrationBuilder.DropTable(
                name: "demand_events");

            migrationBuilder.DropTable(
                name: "tariff_rates");

            migrationBuilder.DropColumn(
                name: "sat_half_kwh",
                table: "meter_energy_summary");
        }
    }
}

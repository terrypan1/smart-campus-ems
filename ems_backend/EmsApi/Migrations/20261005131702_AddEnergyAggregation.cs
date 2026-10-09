using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyAggregation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "meter_readings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "poll");

            migrationBuilder.CreateTable(
                name: "meter_energy_summary",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meter_id = table.Column<int>(type: "integer", nullable: false),
                    period_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    total_kwh = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: true),
                    measured_kwh = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    peak_kwh = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    half_kwh = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    off_kwh = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: false),
                    unassigned_kwh = table.Column<decimal>(type: "numeric(16,2)", precision: 16, scale: 2, nullable: true),
                    ok_hours = table.Column<int>(type: "integer", nullable: false),
                    total_hours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_energy_summary", x => x.id);
                    table.ForeignKey(
                        name: "fk_meter_energy_summary_meters_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meter_hourly_energy",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meter_id = table.Column<int>(type: "integer", nullable: false),
                    hour_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kwh = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tou = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    start_kwh = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_kwh = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_hourly_energy", x => x.id);
                    table.ForeignKey(
                        name: "fk_meter_hourly_energy_meters_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meter_energy_summary_meter_id_period_type_period_start",
                table: "meter_energy_summary",
                columns: new[] { "meter_id", "period_type", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meter_energy_summary_period_type_period_start",
                table: "meter_energy_summary",
                columns: new[] { "period_type", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ix_meter_hourly_energy_meter_id_hour_start",
                table: "meter_hourly_energy",
                columns: new[] { "meter_id", "hour_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_energy_summary");

            migrationBuilder.DropTable(
                name: "meter_hourly_energy");

            migrationBuilder.DropColumn(
                name: "source",
                table: "meter_readings");
        }
    }
}

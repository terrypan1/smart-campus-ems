using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EmsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contract_kw = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "buildings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    campus_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    space_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildings", x => x.id);
                    table.ForeignKey(
                        name: "fk_buildings_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    building_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rated_kw = table.Column<double>(type: "double precision", nullable: false),
                    modbus_host = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    modbus_port = table.Column<int>(type: "integer", nullable: false),
                    unit_id = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meters", x => x.id);
                    table.ForeignKey(
                        name: "fk_meters_buildings_building_id",
                        column: x => x.building_id,
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    meter_id = table.Column<int>(type: "integer", nullable: false),
                    sampled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vrs = table.Column<float>(type: "real", nullable: false),
                    vst = table.Column<float>(type: "real", nullable: false),
                    vtr = table.Column<float>(type: "real", nullable: false),
                    i_r = table.Column<float>(type: "real", nullable: false),
                    i_s = table.Column<float>(type: "real", nullable: false),
                    i_t = table.Column<float>(type: "real", nullable: false),
                    kw = table.Column<float>(type: "real", nullable: false),
                    kvar = table.Column<float>(type: "real", nullable: false),
                    kva = table.Column<float>(type: "real", nullable: false),
                    pf = table.Column<float>(type: "real", nullable: false),
                    hz = table.Column<float>(type: "real", nullable: false),
                    demand_kw = table.Column<float>(type: "real", nullable: false),
                    kwh_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_meter_readings_meters_meter_id",
                        column: x => x.meter_id,
                        principalTable: "meters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buildings_campus_id_code",
                table: "buildings",
                columns: new[] { "campus_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campuses_code",
                table: "campuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_meter_id_sampled_at",
                table: "meter_readings",
                columns: new[] { "meter_id", "sampled_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meters_building_id",
                table: "meters",
                column: "building_id");

            migrationBuilder.CreateIndex(
                name: "ix_meters_code",
                table: "meters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_account",
                table: "users",
                column: "account",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_readings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "meters");

            migrationBuilder.DropTable(
                name: "buildings");

            migrationBuilder.DropTable(
                name: "campuses");
        }
    }
}

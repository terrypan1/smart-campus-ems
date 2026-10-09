using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingTimeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_sampled_at",
                table: "meter_readings",
                column: "sampled_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_meter_readings_sampled_at",
                table: "meter_readings");
        }
    }
}

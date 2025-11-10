using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TTT.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurrentPositions",
                columns: table => new
                {
                    TrainId = table.Column<string>(type: "text", nullable: false),
                    LocStanox = table.Column<string>(type: "text", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: true),
                    Line = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentPositions", x => x.TrainId);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Stanox = table.Column<string>(type: "text", nullable: false),
                    Tiploc = table.Column<string>(type: "text", nullable: true),
                    Crs = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lon = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Stanox);
                });

            migrationBuilder.CreateTable(
                name: "MovementEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrainId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ActualTimestampMs = table.Column<long>(type: "bigint", nullable: false),
                    LocStanox = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: true),
                    VariationStatus = table.Column<string>(type: "text", nullable: false),
                    NextReportStanox = table.Column<string>(type: "text", nullable: true),
                    TocId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovementEvents_TrainId_ActualTimestampMs_LocStanox_EventType",
                table: "MovementEvents",
                columns: new[] { "TrainId", "ActualTimestampMs", "LocStanox", "EventType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrentPositions");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "MovementEvents");
        }
    }
}

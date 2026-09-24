using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AddRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rat");

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                schema: "rat",
                columns: table => new
                {
                    ExchangeRateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ToCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.ExchangeRateId);
                });

            migrationBuilder.CreateTable(
                name: "MetalRates",
                schema: "rat",
                columns: table => new
                {
                    MetalRateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PurityCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RatePerGram = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetalRates", x => x.MetalRateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_FromCurrencyCode_ToCurrencyCode_RateDate_Sour~",
                schema: "rat",
                table: "ExchangeRates",
                columns: new[] { "FromCurrencyCode", "ToCurrencyCode", "RateDate", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetalRates_Metal_PurityCode_RateDate_Source",
                schema: "rat",
                table: "MetalRates",
                columns: new[] { "Metal", "PurityCode", "RateDate", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRates",
                schema: "rat");

            migrationBuilder.DropTable(
                name: "MetalRates",
                schema: "rat");
        }
    }
}

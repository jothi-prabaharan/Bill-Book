using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Site.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSiteMastersSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedRole",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "MinLaborMarginPercent",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "MinMaterialMarginPercent",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "SiteId",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "SubcontractorMarkupPercent",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "BillingHourlyRate",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropColumn(
                name: "CostHourlyRate",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropColumn(
                name: "LaborerType",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.RenameColumn(
                name: "StandardOvertimeHourlyRate",
                schema: "sit",
                table: "Trades",
                newName: "DefaultSellRatePerDay");

            migrationBuilder.RenameColumn(
                name: "StandardHourlyRate",
                schema: "sit",
                table: "Trades",
                newName: "DefaultOtSellRatePerHour");

            migrationBuilder.RenameColumn(
                name: "GeoFenceRadiusMeters",
                schema: "sit",
                table: "Sites",
                newName: "GeofenceRadiusMeters");

            migrationBuilder.RenameColumn(
                name: "SiteAddress",
                schema: "sit",
                table: "Sites",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "ExpectedCompletionDate",
                schema: "sit",
                table: "Sites",
                newName: "EstimatedEndDate");

            migrationBuilder.RenameColumn(
                name: "ClientContactId",
                schema: "sit",
                table: "Sites",
                newName: "ContactId");

            migrationBuilder.RenameColumn(
                name: "ActualCompletionDate",
                schema: "sit",
                table: "Sites",
                newName: "ActualEndDate");

            migrationBuilder.RenameColumn(
                name: "TradeId",
                schema: "sit",
                table: "MarginRules",
                newName: "ScopeId");

            migrationBuilder.RenameColumn(
                name: "ContactId",
                schema: "sit",
                table: "Laborers",
                newName: "EmployeeId");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultCostRatePerDay",
                schema: "sit",
                table: "Trades",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultOtCostRatePerHour",
                schema: "sit",
                table: "Trades",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "DefaultServiceItemId",
                schema: "sit",
                table: "Trades",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                schema: "sit",
                table: "Sites",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                schema: "sit",
                table: "Sites",
                type: "numeric(10,7)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                schema: "sit",
                table: "Sites",
                type: "numeric(10,7)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedDays",
                schema: "sit",
                table: "SiteAssignments",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedOtHours",
                schema: "sit",
                table: "SiteAssignments",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledDays",
                schema: "sit",
                table: "SiteAssignments",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledOtHours",
                schema: "sit",
                table: "SiteAssignments",
                type: "numeric(8,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostRatePerDay",
                schema: "sit",
                table: "SiteAssignments",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellRatePerDay",
                schema: "sit",
                table: "SiteAssignments",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "sit",
                table: "SiteAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TradeId",
                schema: "sit",
                table: "SiteAssignments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AppliesTo",
                schema: "sit",
                table: "MarginRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFrom",
                schema: "sit",
                table: "MarginRules",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveTo",
                schema: "sit",
                table: "MarginRules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MarginType",
                schema: "sit",
                table: "MarginRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MarginValue",
                schema: "sit",
                table: "MarginRules",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                schema: "sit",
                table: "MarginRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostRatePerDay",
                schema: "sit",
                table: "Laborers",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtCostRatePerHour",
                schema: "sit",
                table: "Laborers",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtSellRatePerHour",
                schema: "sit",
                table: "Laborers",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellRatePerDay",
                schema: "sit",
                table: "Laborers",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_LaborerId",
                schema: "sit",
                table: "SiteAssignments",
                column: "LaborerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_SiteId",
                schema: "sit",
                table: "SiteAssignments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAssignments_TradeId",
                schema: "sit",
                table: "SiteAssignments",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarginRules_CustomerId_OrgId_Scope_ScopeId",
                schema: "sit",
                table: "MarginRules",
                columns: new[] { "CustomerId", "OrgId", "Scope", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Laborers_TradeId",
                schema: "sit",
                table: "Laborers",
                column: "TradeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Laborers_Trades_TradeId",
                schema: "sit",
                table: "Laborers",
                column: "TradeId",
                principalSchema: "sit",
                principalTable: "Trades",
                principalColumn: "TradeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteAssignments_Laborers_LaborerId",
                schema: "sit",
                table: "SiteAssignments",
                column: "LaborerId",
                principalSchema: "sit",
                principalTable: "Laborers",
                principalColumn: "LaborerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteAssignments_Sites_SiteId",
                schema: "sit",
                table: "SiteAssignments",
                column: "SiteId",
                principalSchema: "sit",
                principalTable: "Sites",
                principalColumn: "SiteId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SiteAssignments_Trades_TradeId",
                schema: "sit",
                table: "SiteAssignments",
                column: "TradeId",
                principalSchema: "sit",
                principalTable: "Trades",
                principalColumn: "TradeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Laborers_Trades_TradeId",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteAssignments_Laborers_LaborerId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteAssignments_Sites_SiteId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SiteAssignments_Trades_TradeId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SiteAssignments_LaborerId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SiteAssignments_SiteId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SiteAssignments_TradeId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropIndex(
                name: "IX_MarginRules_CustomerId_OrgId_Scope_ScopeId",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropIndex(
                name: "IX_Laborers_TradeId",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropColumn(
                name: "DefaultCostRatePerDay",
                schema: "sit",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "DefaultOtCostRatePerHour",
                schema: "sit",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "DefaultServiceItemId",
                schema: "sit",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "ApprovedDays",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "ApprovedOtHours",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "BilledDays",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "BilledOtHours",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "CostRatePerDay",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "SellRatePerDay",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "TradeId",
                schema: "sit",
                table: "SiteAssignments");

            migrationBuilder.DropColumn(
                name: "AppliesTo",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "MarginType",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "MarginValue",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "sit",
                table: "MarginRules");

            migrationBuilder.DropColumn(
                name: "CostRatePerDay",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropColumn(
                name: "OtCostRatePerHour",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropColumn(
                name: "OtSellRatePerHour",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.DropColumn(
                name: "SellRatePerDay",
                schema: "sit",
                table: "Laborers");

            migrationBuilder.RenameColumn(
                name: "DefaultSellRatePerDay",
                schema: "sit",
                table: "Trades",
                newName: "StandardOvertimeHourlyRate");

            migrationBuilder.RenameColumn(
                name: "DefaultOtSellRatePerHour",
                schema: "sit",
                table: "Trades",
                newName: "StandardHourlyRate");

            migrationBuilder.RenameColumn(
                name: "GeofenceRadiusMeters",
                schema: "sit",
                table: "Sites",
                newName: "GeoFenceRadiusMeters");

            migrationBuilder.RenameColumn(
                name: "EstimatedEndDate",
                schema: "sit",
                table: "Sites",
                newName: "ExpectedCompletionDate");

            migrationBuilder.RenameColumn(
                name: "ContactId",
                schema: "sit",
                table: "Sites",
                newName: "ClientContactId");

            migrationBuilder.RenameColumn(
                name: "Address",
                schema: "sit",
                table: "Sites",
                newName: "SiteAddress");

            migrationBuilder.RenameColumn(
                name: "ActualEndDate",
                schema: "sit",
                table: "Sites",
                newName: "ActualCompletionDate");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                schema: "sit",
                table: "MarginRules",
                newName: "TradeId");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                schema: "sit",
                table: "Laborers",
                newName: "ContactId");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                schema: "sit",
                table: "Sites",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                schema: "sit",
                table: "Sites",
                type: "numeric(9,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                schema: "sit",
                table: "Sites",
                type: "numeric(9,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,7)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedRole",
                schema: "sit",
                table: "SiteAssignments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "sit",
                table: "SiteAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinLaborMarginPercent",
                schema: "sit",
                table: "MarginRules",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinMaterialMarginPercent",
                schema: "sit",
                table: "MarginRules",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "SiteId",
                schema: "sit",
                table: "MarginRules",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubcontractorMarkupPercent",
                schema: "sit",
                table: "MarginRules",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BillingHourlyRate",
                schema: "sit",
                table: "Laborers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostHourlyRate",
                schema: "sit",
                table: "Laborers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LaborerType",
                schema: "sit",
                table: "Laborers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}

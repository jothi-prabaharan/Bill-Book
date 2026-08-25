using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferMoney_OrgId",
                schema: "acc",
                table: "TransferMoney");

            migrationBuilder.DropIndex(
                name: "IX_TransactionRatios_OrgId",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropIndex(
                name: "IX_TaxMasters_OrgId",
                schema: "acc",
                table: "TaxMasters");

            migrationBuilder.DropIndex(
                name: "IX_SubAccounts_OrgId",
                schema: "acc",
                table: "SubAccounts");

            migrationBuilder.DropIndex(
                name: "IX_StatementImportProfiles_OrgId",
                schema: "acc",
                table: "StatementImportProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SpendMoneyDetails_OrgId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropIndex(
                name: "IX_SpendMoney_OrgId",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropIndex(
                name: "IX_ReceiveMoneyDetails_OrgId",
                schema: "acc",
                table: "ReceiveMoneyDetails");

            migrationBuilder.DropIndex(
                name: "IX_ReceiveMoney_OrgId",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropIndex(
                name: "IX_PeriodLocks_OrgId",
                schema: "acc",
                table: "PeriodLocks");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTerms_OrgId",
                schema: "acc",
                table: "PaymentTerms");

            migrationBuilder.DropIndex(
                name: "IX_OpeningBalanceLines_OrgId",
                schema: "acc",
                table: "OpeningBalanceLines");

            migrationBuilder.DropIndex(
                name: "IX_NumberingSeries_OrgId",
                schema: "acc",
                table: "NumberingSeries");

            migrationBuilder.DropIndex(
                name: "IX_Journals_OrgId",
                schema: "acc",
                table: "Journals");

            migrationBuilder.DropIndex(
                name: "IX_JournalLedger_OrgId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropIndex(
                name: "IX_JournalDetails_OrgId",
                schema: "acc",
                table: "JournalDetails");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssets_OrgId",
                schema: "acc",
                table: "FixedAssets");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetCategories_OrgId",
                schema: "acc",
                table: "FixedAssetCategories");

            migrationBuilder.DropIndex(
                name: "IX_DepreciationSchedules_OrgId",
                schema: "acc",
                table: "DepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Banks_OrgId",
                schema: "acc",
                table: "Banks");

            migrationBuilder.DropIndex(
                name: "IX_BankStatements_OrgId",
                schema: "acc",
                table: "BankStatements");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementLines_OrgId",
                schema: "acc",
                table: "BankStatementLines");

            migrationBuilder.DropIndex(
                name: "IX_AssetTransactions_OrgId",
                schema: "acc",
                table: "AssetTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrgId",
                schema: "acc",
                table: "Accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "TransferMoney",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "TransactionRatios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "TaxMasters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "SubAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "StatementImportProfiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "SpendMoneyDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "SpendMoney",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "ReceiveMoney",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "PeriodLocks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "PaymentTerms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "OpeningBalances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "OpeningBalanceLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "NumberingSeries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "Journals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "JournalLedger",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "JournalDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "FixedAssets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "FixedAssetCategories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "DepreciationSchedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "Banks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "BankStatements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "BankStatementLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "BankAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "AssetTransactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "acc",
                table: "Accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_CustomerId_OrgId",
                schema: "acc",
                table: "TransferMoney",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_CustomerId_OrgId",
                schema: "acc",
                table: "TransactionRatios",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_CustomerId_OrgId",
                schema: "acc",
                table: "TaxMasters",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_CustomerId_OrgId",
                schema: "acc",
                table: "SubAccounts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_CustomerId_OrgId",
                schema: "acc",
                table: "StatementImportProfiles",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_CustomerId_OrgId",
                schema: "acc",
                table: "SpendMoneyDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_CustomerId_OrgId",
                schema: "acc",
                table: "SpendMoney",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_CustomerId_OrgId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_CustomerId_OrgId",
                schema: "acc",
                table: "ReceiveMoney",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_CustomerId_OrgId",
                schema: "acc",
                table: "PeriodLocks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_CustomerId_OrgId",
                schema: "acc",
                table: "PaymentTerms",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_CustomerId_OrgId",
                schema: "acc",
                table: "OpeningBalances",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_CustomerId_OrgId",
                schema: "acc",
                table: "OpeningBalanceLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_CustomerId_OrgId",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Journals_CustomerId_OrgId",
                schema: "acc",
                table: "Journals",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_CustomerId_OrgId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_CustomerId_OrgId",
                schema: "acc",
                table: "JournalDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CustomerId_OrgId",
                schema: "acc",
                table: "FixedAssets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_CustomerId_OrgId",
                schema: "acc",
                table: "FixedAssetCategories",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_CustomerId_OrgId",
                schema: "acc",
                table: "DepreciationSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_CustomerId_OrgId",
                schema: "acc",
                table: "Banks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_CustomerId_OrgId",
                schema: "acc",
                table: "BankStatements",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_CustomerId_OrgId",
                schema: "acc",
                table: "BankStatementLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CustomerId_OrgId",
                schema: "acc",
                table: "BankAccounts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_CustomerId_OrgId",
                schema: "acc",
                table: "AssetTransactions",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_CustomerId_OrgId",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferMoney_CustomerId_OrgId",
                schema: "acc",
                table: "TransferMoney");

            migrationBuilder.DropIndex(
                name: "IX_TransactionRatios_CustomerId_OrgId",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropIndex(
                name: "IX_TaxMasters_CustomerId_OrgId",
                schema: "acc",
                table: "TaxMasters");

            migrationBuilder.DropIndex(
                name: "IX_SubAccounts_CustomerId_OrgId",
                schema: "acc",
                table: "SubAccounts");

            migrationBuilder.DropIndex(
                name: "IX_StatementImportProfiles_CustomerId_OrgId",
                schema: "acc",
                table: "StatementImportProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SpendMoneyDetails_CustomerId_OrgId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropIndex(
                name: "IX_SpendMoney_CustomerId_OrgId",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropIndex(
                name: "IX_ReceiveMoneyDetails_CustomerId_OrgId",
                schema: "acc",
                table: "ReceiveMoneyDetails");

            migrationBuilder.DropIndex(
                name: "IX_ReceiveMoney_CustomerId_OrgId",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropIndex(
                name: "IX_PeriodLocks_CustomerId_OrgId",
                schema: "acc",
                table: "PeriodLocks");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTerms_CustomerId_OrgId",
                schema: "acc",
                table: "PaymentTerms");

            migrationBuilder.DropIndex(
                name: "IX_OpeningBalances_CustomerId_OrgId",
                schema: "acc",
                table: "OpeningBalances");

            migrationBuilder.DropIndex(
                name: "IX_OpeningBalanceLines_CustomerId_OrgId",
                schema: "acc",
                table: "OpeningBalanceLines");

            migrationBuilder.DropIndex(
                name: "IX_NumberingSeries_CustomerId_OrgId",
                schema: "acc",
                table: "NumberingSeries");

            migrationBuilder.DropIndex(
                name: "IX_Journals_CustomerId_OrgId",
                schema: "acc",
                table: "Journals");

            migrationBuilder.DropIndex(
                name: "IX_JournalLedger_CustomerId_OrgId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropIndex(
                name: "IX_JournalDetails_CustomerId_OrgId",
                schema: "acc",
                table: "JournalDetails");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssets_CustomerId_OrgId",
                schema: "acc",
                table: "FixedAssets");

            migrationBuilder.DropIndex(
                name: "IX_FixedAssetCategories_CustomerId_OrgId",
                schema: "acc",
                table: "FixedAssetCategories");

            migrationBuilder.DropIndex(
                name: "IX_DepreciationSchedules_CustomerId_OrgId",
                schema: "acc",
                table: "DepreciationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_Banks_CustomerId_OrgId",
                schema: "acc",
                table: "Banks");

            migrationBuilder.DropIndex(
                name: "IX_BankStatements_CustomerId_OrgId",
                schema: "acc",
                table: "BankStatements");

            migrationBuilder.DropIndex(
                name: "IX_BankStatementLines_CustomerId_OrgId",
                schema: "acc",
                table: "BankStatementLines");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CustomerId_OrgId",
                schema: "acc",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AssetTransactions_CustomerId_OrgId",
                schema: "acc",
                table: "AssetTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_CustomerId_OrgId",
                schema: "acc",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "TransferMoney");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "TransactionRatios");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "TaxMasters");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "SubAccounts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "StatementImportProfiles");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "ReceiveMoneyDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "PeriodLocks");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "PaymentTerms");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "OpeningBalances");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "OpeningBalanceLines");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "NumberingSeries");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "Journals");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "JournalDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "FixedAssetCategories");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "DepreciationSchedules");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "Banks");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "BankStatements");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "BankStatementLines");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "AssetTransactions");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "acc",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_TransferMoney_OrgId",
                schema: "acc",
                table: "TransferMoney",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRatios_OrgId",
                schema: "acc",
                table: "TransactionRatios",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxMasters_OrgId",
                schema: "acc",
                table: "TaxMasters",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SubAccounts_OrgId",
                schema: "acc",
                table: "SubAccounts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementImportProfiles_OrgId",
                schema: "acc",
                table: "StatementImportProfiles",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_OrgId",
                schema: "acc",
                table: "SpendMoneyDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoney_OrgId",
                schema: "acc",
                table: "SpendMoney",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_OrgId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoney_OrgId",
                schema: "acc",
                table: "ReceiveMoney",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_OrgId",
                schema: "acc",
                table: "PeriodLocks",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_OrgId",
                schema: "acc",
                table: "PaymentTerms",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OrgId",
                schema: "acc",
                table: "OpeningBalanceLines",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_OrgId",
                schema: "acc",
                table: "NumberingSeries",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Journals_OrgId",
                schema: "acc",
                table: "Journals",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId",
                schema: "acc",
                table: "JournalLedger",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_OrgId",
                schema: "acc",
                table: "JournalDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_OrgId",
                schema: "acc",
                table: "FixedAssets",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_OrgId",
                schema: "acc",
                table: "FixedAssetCategories",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_OrgId",
                schema: "acc",
                table: "DepreciationSchedules",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Banks_OrgId",
                schema: "acc",
                table: "Banks",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatements_OrgId",
                schema: "acc",
                table: "BankStatements",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_OrgId",
                schema: "acc",
                table: "BankStatementLines",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_OrgId",
                schema: "acc",
                table: "AssetTransactions",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId",
                schema: "acc",
                table: "Accounts",
                column: "OrgId");
        }
    }
}

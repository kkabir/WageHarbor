using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paycrest.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EarningTypes_Code",
                table: "EarningTypes");

            migrationBuilder.DropIndex(
                name: "IX_DeductionTypes_Code",
                table: "DeductionTypes");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "WiseSayings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TimeOffTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TimeOffRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TimeOffPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TimeOffBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "TimeEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "PayrollRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "PayrollAdjustments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Paychecks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "PaycheckLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "PaycheckDeposits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Holidays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "EmployeeDeductions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "EmployeeBankAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "EarningTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "DeductionTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Announcements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Everything that existed before multi-company support belongs to the original company.
            migrationBuilder.Sql(@"
                DECLARE @c int = (SELECT MIN(Id) FROM [Companies]);
                IF @c IS NOT NULL
                BEGIN
                UPDATE [Announcements] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [DeductionTypes] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [Departments] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [EarningTypes] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [EmployeeBankAccounts] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [EmployeeDeductions] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [EmployeeDocuments] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [Employees] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [Holidays] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [PaycheckDeposits] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [PaycheckLines] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [Paychecks] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [PayrollAdjustments] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [PayrollRuns] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [TimeEntries] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [TimeOffBalances] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [TimeOffPolicies] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [TimeOffRequests] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [TimeOffTransactions] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [WiseSayings] SET CompanyId = @c WHERE CompanyId = 0;
                UPDATE [AspNetUsers] SET CompanyId = @c WHERE CompanyId IS NULL;
                UPDATE [AuditLogs] SET CompanyId = @c WHERE CompanyId IS NULL;
                END");

            migrationBuilder.CreateTable(
                name: "CompanyLogos",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLogos", x => x.CompanyId);
                    table.ForeignKey(
                        name: "FK_CompanyLogos_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WiseSayings_CompanyId",
                table: "WiseSayings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffTransactions_CompanyId",
                table: "TimeOffTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffRequests_CompanyId",
                table: "TimeOffRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffPolicies_CompanyId",
                table: "TimeOffPolicies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeOffBalances_CompanyId",
                table: "TimeOffBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_CompanyId",
                table: "TimeEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_CompanyId",
                table: "PayrollRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdjustments_CompanyId",
                table: "PayrollAdjustments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Paychecks_CompanyId",
                table: "Paychecks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckLines_CompanyId",
                table: "PaycheckLines",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaycheckDeposits_CompanyId",
                table: "PaycheckDeposits",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_CompanyId",
                table: "Holidays",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                table: "Employees",
                columns: new[] { "CompanyId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_CompanyId",
                table: "EmployeeDocuments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_CompanyId",
                table: "EmployeeDeductions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_CompanyId",
                table: "EmployeeBankAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EarningTypes_CompanyId_Code",
                table: "EarningTypes",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CompanyId",
                table: "Departments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionTypes_CompanyId_Code",
                table: "DeductionTypes",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CompanyId",
                table: "AuditLogs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CompanyId",
                table: "Announcements",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Announcements_Companies_CompanyId",
                table: "Announcements",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Companies_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Companies_CompanyId",
                table: "AuditLogs",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeductionTypes_Companies_CompanyId",
                table: "DeductionTypes",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Companies_CompanyId",
                table: "Departments",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EarningTypes_Companies_CompanyId",
                table: "EarningTypes",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeBankAccounts_Companies_CompanyId",
                table: "EmployeeBankAccounts",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDeductions_Companies_CompanyId",
                table: "EmployeeDeductions",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_Companies_CompanyId",
                table: "EmployeeDocuments",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Companies_CompanyId",
                table: "Employees",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Holidays_Companies_CompanyId",
                table: "Holidays",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaycheckDeposits_Companies_CompanyId",
                table: "PaycheckDeposits",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaycheckLines_Companies_CompanyId",
                table: "PaycheckLines",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Paychecks_Companies_CompanyId",
                table: "Paychecks",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollAdjustments_Companies_CompanyId",
                table: "PayrollAdjustments",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollRuns_Companies_CompanyId",
                table: "PayrollRuns",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_Companies_CompanyId",
                table: "TimeEntries",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffBalances_Companies_CompanyId",
                table: "TimeOffBalances",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffPolicies_Companies_CompanyId",
                table: "TimeOffPolicies",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffRequests_Companies_CompanyId",
                table: "TimeOffRequests",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeOffTransactions_Companies_CompanyId",
                table: "TimeOffTransactions",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WiseSayings_Companies_CompanyId",
                table: "WiseSayings",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Announcements_Companies_CompanyId",
                table: "Announcements");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Companies_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Companies_CompanyId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_DeductionTypes_Companies_CompanyId",
                table: "DeductionTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Companies_CompanyId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_EarningTypes_Companies_CompanyId",
                table: "EarningTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeBankAccounts_Companies_CompanyId",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDeductions_Companies_CompanyId",
                table: "EmployeeDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_Companies_CompanyId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Companies_CompanyId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Holidays_Companies_CompanyId",
                table: "Holidays");

            migrationBuilder.DropForeignKey(
                name: "FK_PaycheckDeposits_Companies_CompanyId",
                table: "PaycheckDeposits");

            migrationBuilder.DropForeignKey(
                name: "FK_PaycheckLines_Companies_CompanyId",
                table: "PaycheckLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Paychecks_Companies_CompanyId",
                table: "Paychecks");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollAdjustments_Companies_CompanyId",
                table: "PayrollAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollRuns_Companies_CompanyId",
                table: "PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_Companies_CompanyId",
                table: "TimeEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffBalances_Companies_CompanyId",
                table: "TimeOffBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffPolicies_Companies_CompanyId",
                table: "TimeOffPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffRequests_Companies_CompanyId",
                table: "TimeOffRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeOffTransactions_Companies_CompanyId",
                table: "TimeOffTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_WiseSayings_Companies_CompanyId",
                table: "WiseSayings");

            migrationBuilder.DropTable(
                name: "CompanyLogos");

            migrationBuilder.DropIndex(
                name: "IX_WiseSayings_CompanyId",
                table: "WiseSayings");

            migrationBuilder.DropIndex(
                name: "IX_TimeOffTransactions_CompanyId",
                table: "TimeOffTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TimeOffRequests_CompanyId",
                table: "TimeOffRequests");

            migrationBuilder.DropIndex(
                name: "IX_TimeOffPolicies_CompanyId",
                table: "TimeOffPolicies");

            migrationBuilder.DropIndex(
                name: "IX_TimeOffBalances_CompanyId",
                table: "TimeOffBalances");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_CompanyId",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_CompanyId",
                table: "PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PayrollAdjustments_CompanyId",
                table: "PayrollAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_Paychecks_CompanyId",
                table: "Paychecks");

            migrationBuilder.DropIndex(
                name: "IX_PaycheckLines_CompanyId",
                table: "PaycheckLines");

            migrationBuilder.DropIndex(
                name: "IX_PaycheckDeposits_CompanyId",
                table: "PaycheckDeposits");

            migrationBuilder.DropIndex(
                name: "IX_Holidays_CompanyId",
                table: "Holidays");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_CompanyId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDeductions_CompanyId",
                table: "EmployeeDeductions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeBankAccounts_CompanyId",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_EarningTypes_CompanyId_Code",
                table: "EarningTypes");

            migrationBuilder.DropIndex(
                name: "IX_Departments_CompanyId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_DeductionTypes_CompanyId_Code",
                table: "DeductionTypes");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CompanyId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_CompanyId",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "WiseSayings");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TimeOffTransactions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TimeOffRequests");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TimeOffPolicies");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TimeOffBalances");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PayrollAdjustments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Paychecks");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PaycheckLines");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PaycheckDeposits");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "EmployeeDeductions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "EmployeeBankAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "EarningTypes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "DeductionTypes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarningTypes_Code",
                table: "EarningTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeductionTypes_Code",
                table: "DeductionTypes",
                column: "Code",
                unique: true);
        }
    }
}

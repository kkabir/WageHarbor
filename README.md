# Paycrest — Payroll & HR for small business

A free, self-hosted payroll and HR app built with ASP.NET Core 10 Blazor Server and SQL Server.

## Features

- **Payroll engine:** gross-to-net with federal withholding (IRS Pub. 15-T percentage method, 2020+ W-4), Social Security
  with wage base, Medicare + Additional Medicare, state income tax (flat/bracket rules), FUTA/SUTA, FLSA weekly overtime,
  pre-/post-tax deductions with annual limits, employer 401(k) match, supplemental (bonus) withholding, salary proration, 1099 contractors.
- **Payroll workflow:** regular and off-cycle runs, one-time earnings, draft → approve → paid → void, NACHA ACH file,
  split direct deposits, check numbers, printable pay stubs with YTD.
- **HR:** employee records, encrypted SSN and bank accounts, W-4, documents, departments, managers, terminations.
- **Time & attendance:** web time clock, manual entries, manager approvals.
- **Time off:** accrual policies (per period / per hour / annual), requests and approvals, balances ledger, holidays.
- **Employee self-service:** pay stubs, W-4 updates, contact info, time clock, time-off requests.
- **Reports:** payroll register, tax liability, Form 941 worksheet, W-2 summary, 1099 contractors, headcount — CSV export.
- **Admin:** roles (Admin, Payroll, HR, Manager, Employee), 2FA/passkeys, audit log, dark mode, mobile layout.

## Run locally

Requires the .NET 10 SDK and SQL Server LocalDB (installed with Visual Studio).

```bash
dotnet run --project src/Paycrest --launch-profile http
```

Open http://localhost:5062, create the administrator account, then click **Load sample company** in the setup wizard.

## Tests

```bash
dotnet test
```

## Deploy

See [docs/DEPLOY-SMARTERASP.md](docs/DEPLOY-SMARTERASP.md).

## Project layout

| Path | Contents |
|---|---|
| `src/Paycrest/Domain` | Entities and enums |
| `src/Paycrest/Data` | EF Core context (with audit trail), migrations, seed data |
| `src/Paycrest/Services/Payroll` | `PayrollCalculator` (pure engine), `PayrollService`, `NachaFileBuilder`, pay periods |
| `src/Paycrest/Services` | Employees, time, time off, reports, sample data, email |
| `src/Paycrest/Components/Pages` | Blazor pages |
| `tests/Paycrest.Tests` | Unit tests for the payroll engine and ACH builder |

> Tax tables are best-effort starting points. Verify rates every year before running payroll.

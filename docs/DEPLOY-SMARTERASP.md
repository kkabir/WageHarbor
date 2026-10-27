# Deploying Paycrest to SmarterASP.NET

Paycrest is an ASP.NET Core 10 Blazor Server app with a SQL Server database. SmarterASP.NET hosts both on Windows/IIS.
This takes about 30 minutes the first time.

---

## 1. Prepare the hosting account

1. Sign in to the SmarterASP.NET **Control Panel**.
2. **Websites** → your site → **ASP.NET version**: choose **.NET 10** (ASP.NET Core).
   If .NET 10 isn't offered yet, use the *self-contained* publish in step 3b — it bundles the runtime.
3. **Websites** → **WebSocket**: turn it **On**. Blazor Server works without it (falls back to long polling) but is much snappier with it.
4. **Websites** → **SSL**: install the free **Let's Encrypt** certificate and enable **Force HTTPS**.
   Payroll data must never travel over plain HTTP.

## 2. Create the database

1. **Databases** → **MSSQL** → **Create database**. Pick SQL Server 2019 or later.
2. Note the values the panel shows: server (e.g. `SQL1234.site4now.net`), database name, login and password.
3. Your connection string looks like:

```
Data Source=SQL1234.site4now.net;Initial Catalog=db_abc123_paycrest;User Id=db_abc123_paycrest_admin;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True
```

You do **not** need to run any SQL scripts. On first start Paycrest applies its EF Core migrations and seeds roles,
2025/2026 tax tables, earning/deduction types and time-off policies.

## 3. Publish the app

From the repository root on your PC:

**3a. Framework-dependent** (when .NET 10 is selectable in the panel — smaller upload):

```bash
dotnet publish src/Paycrest -c Release -o publish
```

**3b. Self-contained** (works regardless of the server's installed runtime):

```bash
dotnet publish src/Paycrest -c Release -r win-x64 --self-contained true -o publish
```

`dotnet publish` generates `publish/web.config` for IIS automatically.

## 4. Configure production settings

Create `publish/appsettings.Production.json` (do **not** commit it to source control):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=SQL1234.site4now.net;Initial Catalog=db_abc123_paycrest;User Id=db_abc123_paycrest_admin;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Smtp": {
    "Host": "mail.yourdomain.com",
    "Port": 8889,
    "EnableSsl": false,
    "UserName": "payroll@yourdomain.com",
    "Password": "MAIL_PASSWORD",
    "From": "payroll@yourdomain.com"
  },
  "AllowedHosts": "yourdomain.com;www.yourdomain.com"
}
```

- Get the SMTP host/port from **Email** → your mailbox → *Mail settings* in the control panel. SMTP is optional;
  without it password-reset emails are skipped (admins can still reset passwords under **Users & roles**).
- IIS runs the app with `ASPNETCORE_ENVIRONMENT=Production` by default, so this file is picked up automatically.
- Remove the `DevTestAccount` section from `appsettings.Development.json` if you copied it — it is only for local testing.

## 5. Upload

**Option A — Web Deploy (recommended).** In the control panel open **Websites → Web Deploy**, enable it and download the
`.publishsettings` file. In Visual Studio: right-click *Paycrest* → **Publish** → **Import profile** → select the file → Publish.

**Option B — FTP.** Use the FTP credentials from the control panel and upload the *contents* of the `publish` folder
to the site root (usually `/site1` or `/yourdomain.com`). Before re-uploading over a running site, upload an
`app_offline.htm` file first so IIS releases the DLLs, then delete it when the upload is done.

## 6. First run

1. Browse to `https://yourdomain.com`. The first request takes 10–30 seconds while migrations run.
2. Click **create the administrator account** on the sign-in page. Only the very first user can self-register;
   registration closes automatically after that.
3. The setup wizard opens. Enter your legal name, EIN, address, state unemployment rate and the bank account
   payroll is paid from. Optionally click **Load sample company** to explore with demo data (do this only on a
   trial database — sample data can't be removed except by deleting employees individually).
4. Review **Administration → Tax tables** for your state, then add employees and create their self-service logins.

## 7. Troubleshooting

| Symptom | Fix |
|---|---|
| **HTTP 500.30 / 500.31** | The runtime isn't available — republish self-contained (3b), or pick .NET 10 in the panel. |
| **"A network-related error… SQL Server"** | Check the connection string; the DB server is `SQLxxxx.site4now.net`, not `localhost`. |
| **Blank page / "Attempting to reconnect"** | Turn WebSocket on (step 1.3) and make sure HTTPS is forced. |
| Need detailed errors | In `web.config` set `stdoutLogEnabled="true"`, create a `logs` folder, reproduce, read the log, then turn it off again. |
| Everyone signed out after a deploy and SSNs show "Unavailable" | The Data Protection keys table was emptied. Keys live in the `DataProtectionKeys` table — never truncate it, and include it in backups. |

## 8. Operating checklist

- **Backups:** enable automatic MSSQL backups in the control panel (daily) and download one before every upgrade.
  The `DataProtectionKeys` table is required to decrypt SSNs and bank numbers — a backup without it is useless for those fields.
- **Every January:** Settings → Tax tables → *Create next year from this year*, then update the Social Security wage base,
  standard deductions, brackets (IRS Pub. 15-T), your SUTA rate and any state changes **before** the first payroll with a January pay date.
- **Two-factor authentication:** require Admin and Payroll users to enable 2FA (Account → Two-factor authentication).
- **Least privilege:** give managers only the *Manager* role; reserve *Payroll* for the people who approve and pay.
- **Audit log:** review Administration → Audit log periodically, especially *ViewedSSN* and bank-account changes.
- **ACH:** generate the NACHA file after approving payroll and upload it to your bank 2 business days before pay date.
  Deposit withheld taxes through EFTPS and your state's portal on your assigned schedule.

## 9. Upgrading

1. Back up the database.
2. `dotnet publish` the new version and upload it as in step 5 (use `app_offline.htm` with FTP).
3. New migrations apply automatically on the next start.

> **Disclaimer:** Paycrest calculates payroll from configurable tax tables. Tax rules change frequently and vary by
> state and locality; the operator is responsible for verifying rates and filing/depositing taxes on time.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(256) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserPasskeys] (
        [CredentialId] varbinary(1024) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Data] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_AspNetUserPasskeys] PRIMARY KEY ([CredentialId]),
        CONSTRAINT [FK_AspNetUserPasskeys_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserPasskeys_UserId] ON [AspNetUserPasskeys] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'00000000000000_CreateIdentitySchema', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [Announcements] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(120) NOT NULL,
        [Body] nvarchar(4000) NOT NULL,
        [IsPinned] bit NOT NULL,
        [ExpiresOn] date NULL,
        [PostedBy] nvarchar(256) NULL,
        [PostedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Announcements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TimestampUtc] datetime2 NOT NULL,
        [UserName] nvarchar(256) NULL,
        [Action] nvarchar(40) NOT NULL,
        [EntityType] nvarchar(80) NOT NULL,
        [EntityId] nvarchar(40) NULL,
        [Details] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [LegalName] nvarchar(160) NULL,
        [Ein] nvarchar(10) NULL,
        [StateTaxId] nvarchar(40) NULL,
        [StateUnemploymentId] nvarchar(40) NULL,
        [AddressLine1] nvarchar(120) NULL,
        [AddressLine2] nvarchar(120) NULL,
        [City] nvarchar(80) NULL,
        [State] nvarchar(2) NULL,
        [PostalCode] nvarchar(10) NULL,
        [Phone] nvarchar(30) NULL,
        [Email] nvarchar(120) NULL,
        [TimeZoneId] nvarchar(64) NOT NULL,
        [DefaultPayFrequency] int NOT NULL,
        [PayDateOffsetDays] int NOT NULL,
        [WorkWeekStart] int NOT NULL,
        [OvertimeThresholdHours] decimal(18,4) NOT NULL,
        [SutaRate] decimal(18,4) NOT NULL,
        [SutaWageBase] decimal(18,4) NOT NULL,
        [NextCheckNumber] int NOT NULL,
        [BankName] nvarchar(60) NULL,
        [BankRoutingNumber] nvarchar(9) NULL,
        [BankAccountNumberProtected] nvarchar(max) NULL,
        [BankAccountLast4] nvarchar(4) NULL,
        [AchCompanyId] nvarchar(10) NULL,
        [AchImmediateOrigin] nvarchar(10) NULL,
        [SetupCompleted] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [DeductionTypes] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(12) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [Category] int NOT NULL,
        [ReducesFederalTaxable] bit NOT NULL,
        [ReducesFicaTaxable] bit NOT NULL,
        [ReducesStateTaxable] bit NOT NULL,
        [W2Box12Code] nvarchar(3) NULL,
        [AnnualLimit] decimal(18,4) NULL,
        [EmployerMatchRate] decimal(18,4) NOT NULL,
        [EmployerMatchCapPercent] decimal(18,4) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_DeductionTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(80) NOT NULL,
        [Code] nvarchar(20) NULL,
        [Description] nvarchar(250) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [EarningTypes] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(12) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [Multiplier] decimal(18,4) NOT NULL,
        [IsHoursBased] bit NOT NULL,
        [IsTaxable] bit NOT NULL,
        [IsSupplemental] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_EarningTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [Holidays] (
        [Id] int NOT NULL IDENTITY,
        [Date] date NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [IsPaid] bit NOT NULL,
        CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [PayrollRuns] (
        [Id] int NOT NULL IDENTITY,
        [RunType] int NOT NULL,
        [PayFrequency] int NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [PayDate] date NOT NULL,
        [Status] int NOT NULL,
        [ApplyDeductions] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [EmployeeCount] int NOT NULL,
        [TotalGross] decimal(18,4) NOT NULL,
        [TotalEmployeeTaxes] decimal(18,4) NOT NULL,
        [TotalDeductions] decimal(18,4) NOT NULL,
        [TotalNet] decimal(18,4) NOT NULL,
        [TotalEmployerTaxes] decimal(18,4) NOT NULL,
        [TotalEmployerContributions] decimal(18,4) NOT NULL,
        [Warnings] nvarchar(max) NULL,
        [CreatedBy] nvarchar(256) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [CalculatedUtc] datetime2 NULL,
        [ApprovedBy] nvarchar(256) NULL,
        [ApprovedUtc] datetime2 NULL,
        [PaidUtc] datetime2 NULL,
        CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [StateTaxRules] (
        [Id] int NOT NULL IDENTITY,
        [StateCode] nvarchar(2) NOT NULL,
        [Year] int NOT NULL,
        [Method] int NOT NULL,
        [FlatRate] decimal(18,4) NOT NULL,
        [StandardDeduction] decimal(18,4) NOT NULL,
        [AllowanceAmount] decimal(18,4) NOT NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_StateTaxRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [TaxYears] (
        [Year] int NOT NULL,
        [SocialSecurityRate] decimal(18,4) NOT NULL,
        [SocialSecurityWageBase] decimal(18,4) NOT NULL,
        [MedicareRate] decimal(18,4) NOT NULL,
        [AdditionalMedicareRate] decimal(18,4) NOT NULL,
        [AdditionalMedicareThreshold] decimal(18,4) NOT NULL,
        [FutaRate] decimal(18,4) NOT NULL,
        [FutaWageBase] decimal(18,4) NOT NULL,
        [SupplementalRate] decimal(18,4) NOT NULL,
        [StandardDeductionSingle] decimal(18,4) NOT NULL,
        [StandardDeductionMarried] decimal(18,4) NOT NULL,
        [StandardDeductionHeadOfHousehold] decimal(18,4) NOT NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_TaxYears] PRIMARY KEY ([Year])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeNumber] nvarchar(20) NOT NULL,
        [FirstName] nvarchar(60) NOT NULL,
        [MiddleName] nvarchar(60) NULL,
        [LastName] nvarchar(60) NOT NULL,
        [PreferredName] nvarchar(60) NULL,
        [Email] nvarchar(120) NULL,
        [Phone] nvarchar(30) NULL,
        [DateOfBirth] date NULL,
        [SsnProtected] nvarchar(max) NULL,
        [SsnLast4] nvarchar(4) NULL,
        [AddressLine1] nvarchar(120) NULL,
        [AddressLine2] nvarchar(120) NULL,
        [City] nvarchar(80) NULL,
        [State] nvarchar(2) NULL,
        [PostalCode] nvarchar(10) NULL,
        [EmergencyContactName] nvarchar(120) NULL,
        [EmergencyContactPhone] nvarchar(30) NULL,
        [EmergencyContactRelation] nvarchar(40) NULL,
        [HireDate] date NOT NULL,
        [TerminationDate] date NULL,
        [TerminationReason] nvarchar(250) NULL,
        [Status] int NOT NULL,
        [EmploymentType] int NOT NULL,
        [DepartmentId] int NULL,
        [JobTitle] nvarchar(100) NULL,
        [ManagerId] int NULL,
        [WorkState] nvarchar(2) NOT NULL,
        [PayType] int NOT NULL,
        [PayRate] decimal(18,4) NOT NULL,
        [PayFrequency] int NOT NULL,
        [DefaultHours] decimal(18,4) NOT NULL,
        [IsExempt] bit NOT NULL,
        [PaymentMethod] int NOT NULL,
        [FilingStatus] int NOT NULL,
        [W4MultipleJobs] bit NOT NULL,
        [W4DependentsAmount] decimal(18,4) NOT NULL,
        [W4OtherIncome] decimal(18,4) NOT NULL,
        [W4Deductions] decimal(18,4) NOT NULL,
        [W4ExtraWithholding] decimal(18,4) NOT NULL,
        [FederalExempt] bit NOT NULL,
        [StateFilingStatus] int NOT NULL,
        [StateAllowances] int NOT NULL,
        [StateExtraWithholding] decimal(18,4) NOT NULL,
        [StateExempt] bit NOT NULL,
        [UserId] nvarchar(450) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Employees_Employees_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [TimeOffPolicies] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(60) NOT NULL,
        [EarningTypeId] int NOT NULL,
        [AccrualMethod] int NOT NULL,
        [AccrualRate] decimal(18,4) NOT NULL,
        [MaxBalance] decimal(18,4) NULL,
        [AllowNegativeBalance] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TimeOffPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeOffPolicies_EarningTypes_EarningTypeId] FOREIGN KEY ([EarningTypeId]) REFERENCES [EarningTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [StateTaxBrackets] (
        [Id] int NOT NULL IDENTITY,
        [StateTaxRuleId] int NOT NULL,
        [FilingStatus] int NOT NULL,
        [Threshold] decimal(18,4) NOT NULL,
        [Rate] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_StateTaxBrackets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StateTaxBrackets_StateTaxRules_StateTaxRuleId] FOREIGN KEY ([StateTaxRuleId]) REFERENCES [StateTaxRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [FederalTaxBrackets] (
        [Id] int NOT NULL IDENTITY,
        [Year] int NOT NULL,
        [FilingStatus] int NOT NULL,
        [Threshold] decimal(18,4) NOT NULL,
        [Rate] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_FederalTaxBrackets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FederalTaxBrackets_TaxYears_Year] FOREIGN KEY ([Year]) REFERENCES [TaxYears] ([Year]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [EmployeeBankAccounts] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [BankName] nvarchar(60) NULL,
        [RoutingNumber] nvarchar(max) NOT NULL,
        [AccountNumberProtected] nvarchar(max) NOT NULL,
        [AccountLast4] nvarchar(4) NOT NULL,
        [AccountType] int NOT NULL,
        [SplitType] int NOT NULL,
        [SplitValue] decimal(18,4) NOT NULL,
        [Priority] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_EmployeeBankAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeBankAccounts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [EmployeeDeductions] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [DeductionTypeId] int NOT NULL,
        [IsPercentOfGross] bit NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [EmployerAmount] decimal(18,4) NOT NULL,
        [AnnualLimit] decimal(18,4) NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [Reference] nvarchar(120) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_EmployeeDeductions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeDeductions_DeductionTypes_DeductionTypeId] FOREIGN KEY ([DeductionTypeId]) REFERENCES [DeductionTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EmployeeDeductions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [EmployeeDocuments] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Content] varbinary(max) NOT NULL,
        [VisibleToEmployee] bit NOT NULL,
        [UploadedBy] nvarchar(256) NULL,
        [UploadedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EmployeeDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeDocuments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [Paychecks] (
        [Id] int NOT NULL IDENTITY,
        [PayrollRunId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [EmployeeName] nvarchar(130) NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [PayDate] date NOT NULL,
        [PaymentMethod] int NOT NULL,
        [CheckNumber] int NULL,
        [TotalHours] decimal(18,4) NOT NULL,
        [GrossPay] decimal(18,4) NOT NULL,
        [PreTaxDeductions] decimal(18,4) NOT NULL,
        [PostTaxDeductions] decimal(18,4) NOT NULL,
        [EmployeeTaxes] decimal(18,4) NOT NULL,
        [NetPay] decimal(18,4) NOT NULL,
        [FederalTaxableWages] decimal(18,4) NOT NULL,
        [SocialSecurityWages] decimal(18,4) NOT NULL,
        [MedicareWages] decimal(18,4) NOT NULL,
        [StateTaxableWages] decimal(18,4) NOT NULL,
        [FutaWages] decimal(18,4) NOT NULL,
        [SutaWages] decimal(18,4) NOT NULL,
        [FederalIncomeTax] decimal(18,4) NOT NULL,
        [SocialSecurityTax] decimal(18,4) NOT NULL,
        [MedicareTax] decimal(18,4) NOT NULL,
        [AdditionalMedicareTax] decimal(18,4) NOT NULL,
        [StateCode] nvarchar(2) NULL,
        [StateIncomeTax] decimal(18,4) NOT NULL,
        [EmployerSocialSecurity] decimal(18,4) NOT NULL,
        [EmployerMedicare] decimal(18,4) NOT NULL,
        [FutaTax] decimal(18,4) NOT NULL,
        [SutaTax] decimal(18,4) NOT NULL,
        [EmployerContributions] decimal(18,4) NOT NULL,
        [Warnings] nvarchar(max) NULL,
        CONSTRAINT [PK_Paychecks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Paychecks_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Paychecks_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [PayrollAdjustments] (
        [Id] int NOT NULL IDENTITY,
        [PayrollRunId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [EarningTypeId] int NOT NULL,
        [Hours] decimal(18,4) NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Note] nvarchar(200) NULL,
        CONSTRAINT [PK_PayrollAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollAdjustments_EarningTypes_EarningTypeId] FOREIGN KEY ([EarningTypeId]) REFERENCES [EarningTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollAdjustments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollAdjustments_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [TimeEntries] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [WorkDate] date NOT NULL,
        [EarningTypeId] int NOT NULL,
        [Hours] decimal(18,4) NOT NULL,
        [ClockInUtc] datetime2 NULL,
        [ClockOutUtc] datetime2 NULL,
        [Notes] nvarchar(250) NULL,
        [Source] nvarchar(20) NOT NULL,
        [Status] int NOT NULL,
        [ApprovedBy] nvarchar(256) NULL,
        [ApprovedUtc] datetime2 NULL,
        [PayrollRunId] int NULL,
        [TimeOffRequestId] int NULL,
        CONSTRAINT [PK_TimeEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeEntries_EarningTypes_EarningTypeId] FOREIGN KEY ([EarningTypeId]) REFERENCES [EarningTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TimeEntries_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [TimeOffBalances] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TimeOffPolicyId] int NOT NULL,
        [Balance] decimal(18,4) NOT NULL,
        [AccruedYtd] decimal(18,4) NOT NULL,
        [UsedYtd] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_TimeOffBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeOffBalances_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TimeOffBalances_TimeOffPolicies_TimeOffPolicyId] FOREIGN KEY ([TimeOffPolicyId]) REFERENCES [TimeOffPolicies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [TimeOffRequests] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TimeOffPolicyId] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Hours] decimal(18,4) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [RequestedUtc] datetime2 NOT NULL,
        [ReviewedBy] nvarchar(256) NULL,
        [ReviewedUtc] datetime2 NULL,
        [ReviewNote] nvarchar(500) NULL,
        CONSTRAINT [PK_TimeOffRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeOffRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TimeOffRequests_TimeOffPolicies_TimeOffPolicyId] FOREIGN KEY ([TimeOffPolicyId]) REFERENCES [TimeOffPolicies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [TimeOffTransactions] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [TimeOffPolicyId] int NOT NULL,
        [Date] date NOT NULL,
        [Hours] decimal(18,4) NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [PayrollRunId] int NULL,
        [TimeOffRequestId] int NULL,
        [Note] nvarchar(250) NULL,
        [CreatedBy] nvarchar(256) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TimeOffTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeOffTransactions_TimeOffPolicies_TimeOffPolicyId] FOREIGN KEY ([TimeOffPolicyId]) REFERENCES [TimeOffPolicies] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [PaycheckDeposits] (
        [Id] int NOT NULL IDENTITY,
        [PaycheckId] int NOT NULL,
        [BankName] nvarchar(60) NULL,
        [RoutingNumber] nvarchar(9) NOT NULL,
        [AccountNumberProtected] nvarchar(max) NOT NULL,
        [AccountLast4] nvarchar(4) NOT NULL,
        [AccountType] int NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_PaycheckDeposits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaycheckDeposits_Paychecks_PaycheckId] FOREIGN KEY ([PaycheckId]) REFERENCES [Paychecks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE TABLE [PaycheckLines] (
        [Id] int NOT NULL IDENTITY,
        [PaycheckId] int NOT NULL,
        [LineType] int NOT NULL,
        [Code] nvarchar(12) NOT NULL,
        [Description] nvarchar(80) NOT NULL,
        [Hours] decimal(18,4) NULL,
        [Rate] decimal(18,4) NULL,
        [Amount] decimal(18,4) NOT NULL,
        [EarningTypeId] int NULL,
        [DeductionTypeId] int NULL,
        CONSTRAINT [PK_PaycheckLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaycheckLines_Paychecks_PaycheckId] FOREIGN KEY ([PaycheckId]) REFERENCES [Paychecks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs] ([TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DeductionTypes_Code] ON [DeductionTypes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EarningTypes_Code] ON [EarningTypes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeBankAccounts_EmployeeId] ON [EmployeeBankAccounts] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeDeductions_DeductionTypeId] ON [EmployeeDeductions] ([DeductionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeDeductions_EmployeeId] ON [EmployeeDeductions] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_EmployeeDocuments_EmployeeId] ON [EmployeeDocuments] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_EmployeeNumber] ON [Employees] ([EmployeeNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_Employees_ManagerId] ON [Employees] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_Employees_UserId] ON [Employees] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_FederalTaxBrackets_Year] ON [FederalTaxBrackets] ([Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_PaycheckDeposits_PaycheckId] ON [PaycheckDeposits] ([PaycheckId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_PaycheckLines_PaycheckId] ON [PaycheckLines] ([PaycheckId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_Paychecks_EmployeeId_PayDate] ON [Paychecks] ([EmployeeId], [PayDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_Paychecks_PayrollRunId] ON [Paychecks] ([PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_EarningTypeId] ON [PayrollAdjustments] ([EarningTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_EmployeeId] ON [PayrollAdjustments] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_PayrollAdjustments_PayrollRunId] ON [PayrollAdjustments] ([PayrollRunId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_PayrollRuns_PayDate] ON [PayrollRuns] ([PayDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_StateTaxBrackets_StateTaxRuleId] ON [StateTaxBrackets] ([StateTaxRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StateTaxRules_StateCode_Year] ON [StateTaxRules] ([StateCode], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_EarningTypeId] ON [TimeEntries] ([EarningTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeEntries_EmployeeId_WorkDate] ON [TimeEntries] ([EmployeeId], [WorkDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TimeOffBalances_EmployeeId_TimeOffPolicyId] ON [TimeOffBalances] ([EmployeeId], [TimeOffPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeOffBalances_TimeOffPolicyId] ON [TimeOffBalances] ([TimeOffPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeOffPolicies_EarningTypeId] ON [TimeOffPolicies] ([EarningTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeOffRequests_EmployeeId] ON [TimeOffRequests] ([EmployeeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeOffRequests_TimeOffPolicyId] ON [TimeOffRequests] ([TimeOffPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeOffTransactions_EmployeeId_TimeOffPolicyId] ON [TimeOffTransactions] ([EmployeeId], [TimeOffPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    CREATE INDEX [IX_TimeOffTransactions_TimeOffPolicyId] ON [TimeOffTransactions] ([TimeOffPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027181026_InitialPayroll'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027181026_InitialPayroll', N'10.0.12');
END;

COMMIT;
GO


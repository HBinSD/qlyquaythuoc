USE QuanLyQuayThuoc;
GO

IF OBJECT_ID(N'dbo.LoyaltySettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LoyaltySettings
    (
        SettingsId tinyint NOT NULL
            CONSTRAINT PK_LoyaltySettings PRIMARY KEY
            CONSTRAINT CK_LoyaltySettings_SingleRow CHECK (SettingsId = 1),
        EarnPercent decimal(6,3) NOT NULL,
        MoneyPerPoint decimal(18,2) NOT NULL,
        UpdatedBy varchar(20) NOT NULL,
        UpdatedAt datetime2(0) NOT NULL
            CONSTRAINT DF_LoyaltySettings_UpdatedAt DEFAULT SYSDATETIME(),
        CONSTRAINT FK_LoyaltySettings_Employee
            FOREIGN KEY (UpdatedBy) REFERENCES dbo.Employee(EmployeeId),
        CONSTRAINT CK_LoyaltySettings_Values
            CHECK (EarnPercent >= 0 AND EarnPercent <= 100 AND MoneyPerPoint > 0)
    );
END;
GO

IF COL_LENGTH(N'dbo.VoucherUsage', N'CancelledAt') IS NULL
    ALTER TABLE dbo.VoucherUsage ADD CancelledAt datetime2(0) NULL;
GO

IF COL_LENGTH(N'dbo.VoucherUsage', N'CancelledBy') IS NULL
    ALTER TABLE dbo.VoucherUsage ADD CancelledBy varchar(20) NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VoucherUsage_CancelledBy'
)
    ALTER TABLE dbo.VoucherUsage
        ADD CONSTRAINT FK_VoucherUsage_CancelledBy
        FOREIGN KEY (CancelledBy) REFERENCES dbo.Employee(EmployeeId);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.LoyaltySettings WHERE SettingsId = 1)
BEGIN
    DECLARE @InitialManager varchar(20);
    SELECT TOP (1) @InitialManager = EmployeeId
    FROM dbo.Employee
    WHERE RoleId = 'ADMIN' AND IsActive = 1
    ORDER BY EmployeeId;

    IF @InitialManager IS NULL
        THROW 50020, N'Không tìm thấy tài khoản Quản lý đang hoạt động để khởi tạo cấu hình điểm.', 1;

    INSERT dbo.LoyaltySettings(SettingsId, EarnPercent, MoneyPerPoint, UpdatedBy)
    VALUES (1, 1.000, 100.00, @InitialManager);
END;
GO

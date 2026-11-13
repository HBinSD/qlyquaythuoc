/* ============================================================
   QUAN LY QUAY THUOC - SQL SERVER 2022
   Giai doan 1: Database Schema + Seed Data
   Based on: Dac_Ta_Nghiep_Vu_Quan_Ly_Quay_Thuoc_Day_Du.docx
   ============================================================ */

USE master;
GO

IF DB_ID(N'QuanLyQuayThuoc') IS NULL
    CREATE DATABASE QuanLyQuayThuoc;
GO

USE QuanLyQuayThuoc;
GO

/* Re-runnable setup: drop tables in dependency order */
DECLARE @dropSql nvarchar(max) = N'';
SELECT @dropSql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) +
                   N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) +
                   N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.foreign_keys;
IF LEN(@dropSql) > 0 EXEC sp_executesql @dropSql;
GO

DROP TABLE IF EXISTS dbo.AuditLog;
DROP TABLE IF EXISTS dbo.ReturnDetail;
DROP TABLE IF EXISTS dbo.ReturnReceipt;
DROP TABLE IF EXISTS dbo.InvoicePayment;
DROP TABLE IF EXISTS dbo.InvoiceDetail;
DROP TABLE IF EXISTS dbo.Invoice;
DROP TABLE IF EXISTS dbo.PrescriptionDetail;
DROP TABLE IF EXISTS dbo.Prescription;
DROP TABLE IF EXISTS dbo.VoucherUsage;
DROP TABLE IF EXISTS dbo.Voucher;
DROP TABLE IF EXISTS dbo.LoyaltyTransaction;
DROP TABLE IF EXISTS dbo.Customer;
DROP TABLE IF EXISTS dbo.DisposalDetail;
DROP TABLE IF EXISTS dbo.Disposal;
DROP TABLE IF EXISTS dbo.StocktakeDetail;
DROP TABLE IF EXISTS dbo.Stocktake;
DROP TABLE IF EXISTS dbo.StockTransaction;
DROP TABLE IF EXISTS dbo.InventoryReservation;
DROP TABLE IF EXISTS dbo.MedicineBatch;
DROP TABLE IF EXISTS dbo.GoodsReceiptDetail;
DROP TABLE IF EXISTS dbo.GoodsReceipt;
DROP TABLE IF EXISTS dbo.Supplier;
DROP TABLE IF EXISTS dbo.MedicinePriceHistory;
DROP TABLE IF EXISTS dbo.MedicineUnit;
DROP TABLE IF EXISTS dbo.Medicine;
DROP TABLE IF EXISTS dbo.MedicineCategory;
DROP TABLE IF EXISTS dbo.WorkShift;
DROP TABLE IF EXISTS dbo.Employee;
DROP TABLE IF EXISTS dbo.Role;
GO

/* 1. SYSTEM / STAFF */
CREATE TABLE dbo.Role (
    RoleId varchar(20) NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
    RoleName nvarchar(100) NOT NULL,
    Description nvarchar(300) NULL
);
GO

CREATE TABLE dbo.Employee (
    EmployeeId varchar(20) NOT NULL CONSTRAINT PK_Employee PRIMARY KEY,
    RoleId varchar(20) NOT NULL,
    FullName nvarchar(150) NOT NULL,
    Username varchar(50) NOT NULL CONSTRAINT UQ_Employee_Username UNIQUE,
    PasswordHash varchar(255) NOT NULL,
    Phone varchar(20) NULL,
    Email varchar(150) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Employee_IsActive DEFAULT 1,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Employee_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Employee_Role FOREIGN KEY (RoleId) REFERENCES dbo.Role(RoleId)
);
GO

/* 2. MEDICINE CATALOG AND UNIT / PRICE */
CREATE TABLE dbo.MedicineCategory (
    CategoryId varchar(20) NOT NULL CONSTRAINT PK_MedicineCategory PRIMARY KEY,
    CategoryName nvarchar(120) NOT NULL CONSTRAINT UQ_MedicineCategory_Name UNIQUE,
    Description nvarchar(300) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_MedicineCategory_IsActive DEFAULT 1
);
GO

CREATE TABLE dbo.Medicine (
    MedicineId varchar(20) NOT NULL CONSTRAINT PK_Medicine PRIMARY KEY,
    CategoryId varchar(20) NULL,
    MedicineName nvarchar(200) NOT NULL,
    ActiveIngredient nvarchar(200) NULL,
    Strength nvarchar(100) NULL,
    DosageForm nvarchar(100) NULL,
    BaseUnit nvarchar(30) NOT NULL, -- smallest stock unit, e.g. Vien
    MinimumStock decimal(18,3) NOT NULL CONSTRAINT DF_Medicine_MinimumStock DEFAULT 0,
    RequiresPrescription bit NOT NULL CONSTRAINT DF_Medicine_RequiresPrescription DEFAULT 0,
    Barcode varchar(50) NULL,
    Description nvarchar(1000) NULL,
    BusinessStatus varchar(20) NOT NULL CONSTRAINT DF_Medicine_BusinessStatus DEFAULT 'ACTIVE',
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Medicine_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Medicine_Category FOREIGN KEY (CategoryId) REFERENCES dbo.MedicineCategory(CategoryId),
    CONSTRAINT CK_Medicine_MinimumStock CHECK (MinimumStock >= 0),
    CONSTRAINT CK_Medicine_BusinessStatus CHECK (BusinessStatus IN ('ACTIVE','INACTIVE'))
);
GO

/* ConversionFactor = number of smallest units contained in this unit.
   Example: Hop=100, Vi=10, Vien=1. */
CREATE TABLE dbo.MedicineUnit (
    MedicineUnitId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MedicineUnit PRIMARY KEY,
    MedicineId varchar(20) NOT NULL,
    UnitName nvarchar(30) NOT NULL,
    ConversionFactor decimal(18,3) NOT NULL,
    SalePrice decimal(18,2) NOT NULL,
    IsBaseUnit bit NOT NULL CONSTRAINT DF_MedicineUnit_IsBase DEFAULT 0,
    IsActive bit NOT NULL CONSTRAINT DF_MedicineUnit_IsActive DEFAULT 1,
    CONSTRAINT FK_MedicineUnit_Medicine FOREIGN KEY (MedicineId) REFERENCES dbo.Medicine(MedicineId),
    CONSTRAINT UQ_MedicineUnit UNIQUE (MedicineId, UnitName),
    CONSTRAINT CK_MedicineUnit_Factor CHECK (ConversionFactor > 0),
    CONSTRAINT CK_MedicineUnit_Price CHECK (SalePrice >= 0)
);
GO

CREATE TABLE dbo.MedicinePriceHistory (
    PriceHistoryId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MedicinePriceHistory PRIMARY KEY,
    MedicineUnitId int NOT NULL,
    OldPrice decimal(18,2) NOT NULL,
    NewPrice decimal(18,2) NOT NULL,
    ChangedBy varchar(20) NOT NULL,
    ChangedAt datetime2(0) NOT NULL CONSTRAINT DF_PriceHistory_ChangedAt DEFAULT SYSDATETIME(),
    Note nvarchar(300) NULL,
    CONSTRAINT FK_PriceHistory_Unit FOREIGN KEY (MedicineUnitId) REFERENCES dbo.MedicineUnit(MedicineUnitId),
    CONSTRAINT FK_PriceHistory_Employee FOREIGN KEY (ChangedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_PriceHistory_Prices CHECK (OldPrice >= 0 AND NewPrice >= 0)
);
GO

/* 3. SUPPLIER / GOODS RECEIPT / BATCH */
CREATE TABLE dbo.Supplier (
    SupplierId varchar(20) NOT NULL CONSTRAINT PK_Supplier PRIMARY KEY,
    SupplierName nvarchar(200) NOT NULL,
    Phone varchar(20) NULL,
    Email varchar(150) NULL,
    Address nvarchar(300) NULL,
    TaxCode varchar(30) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Supplier_IsActive DEFAULT 1
);
GO

CREATE TABLE dbo.GoodsReceipt (
    ReceiptId varchar(20) NOT NULL CONSTRAINT PK_GoodsReceipt PRIMARY KEY,
    SupplierId varchar(20) NOT NULL,
    CreatedBy varchar(20) NOT NULL,
    ReceiptDate datetime2(0) NOT NULL CONSTRAINT DF_GoodsReceipt_Date DEFAULT SYSDATETIME(),
    Status varchar(20) NOT NULL CONSTRAINT DF_GoodsReceipt_Status DEFAULT 'DRAFT',
    Note nvarchar(500) NULL,
    ConfirmedBy varchar(20) NULL,
    ConfirmedAt datetime2(0) NULL,
    CONSTRAINT FK_GoodsReceipt_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Supplier(SupplierId),
    CONSTRAINT FK_GoodsReceipt_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT FK_GoodsReceipt_ConfirmedBy FOREIGN KEY (ConfirmedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_GoodsReceipt_Status CHECK (Status IN ('DRAFT','CONFIRMED','CANCELLED'))
);
GO

CREATE TABLE dbo.GoodsReceiptDetail (
    ReceiptDetailId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GoodsReceiptDetail PRIMARY KEY,
    ReceiptId varchar(20) NOT NULL,
    MedicineId varchar(20) NOT NULL,
    UnitName nvarchar(30) NOT NULL,
    Quantity decimal(18,3) NOT NULL,
    ConversionFactor decimal(18,3) NOT NULL,
    QuantityBase decimal(18,3) NOT NULL,
    PurchaseUnitPrice decimal(18,2) NOT NULL,
    BatchNumber varchar(50) NOT NULL,
    ManufactureDate date NULL,
    ExpiryDate date NOT NULL,
    CONSTRAINT FK_ReceiptDetail_Receipt FOREIGN KEY (ReceiptId) REFERENCES dbo.GoodsReceipt(ReceiptId),
    CONSTRAINT FK_ReceiptDetail_Medicine FOREIGN KEY (MedicineId) REFERENCES dbo.Medicine(MedicineId),
    CONSTRAINT UQ_ReceiptDetail_Batch UNIQUE (ReceiptId, MedicineId, BatchNumber),
    CONSTRAINT CK_ReceiptDetail_Quantity CHECK (Quantity > 0 AND ConversionFactor > 0 AND QuantityBase > 0),
    CONSTRAINT CK_ReceiptDetail_Price CHECK (PurchaseUnitPrice >= 0),
    CONSTRAINT CK_ReceiptDetail_Dates CHECK (ManufactureDate IS NULL OR ExpiryDate > ManufactureDate)
);
GO

CREATE TABLE dbo.MedicineBatch (
    BatchId varchar(25) NOT NULL CONSTRAINT PK_MedicineBatch PRIMARY KEY,
    ReceiptDetailId bigint NOT NULL CONSTRAINT UQ_MedicineBatch_ReceiptDetail UNIQUE,
    MedicineId varchar(20) NOT NULL,
    BatchNumber varchar(50) NOT NULL,
    ManufactureDate date NULL,
    ExpiryDate date NOT NULL,
    PurchasePriceBase decimal(18,4) NOT NULL,
    InitialQuantity decimal(18,3) NOT NULL,
    PhysicalStock decimal(18,3) NOT NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Batch_CreatedAt DEFAULT SYSDATETIME(),
    BatchStatus varchar(20) NOT NULL CONSTRAINT DF_Batch_Status DEFAULT 'ACTIVE',
    CONSTRAINT FK_Batch_ReceiptDetail FOREIGN KEY (ReceiptDetailId) REFERENCES dbo.GoodsReceiptDetail(ReceiptDetailId),
    CONSTRAINT FK_Batch_Medicine FOREIGN KEY (MedicineId) REFERENCES dbo.Medicine(MedicineId),
    CONSTRAINT CK_Batch_Quantities CHECK (InitialQuantity >= 0 AND PhysicalStock >= 0),
    CONSTRAINT CK_Batch_Price CHECK (PurchasePriceBase >= 0),
    CONSTRAINT CK_Batch_Status CHECK (BatchStatus IN ('ACTIVE','DISPOSED','CLOSED'))
);
GO

/* 4. SHIFT */
CREATE TABLE dbo.WorkShift (
    ShiftId varchar(20) NOT NULL CONSTRAINT PK_WorkShift PRIMARY KEY,
    EmployeeId varchar(20) NOT NULL,
    OpenedAt datetime2(0) NOT NULL CONSTRAINT DF_WorkShift_OpenedAt DEFAULT SYSDATETIME(),
    OpeningCash decimal(18,2) NOT NULL CONSTRAINT DF_WorkShift_OpeningCash DEFAULT 0,
    ClosedAt datetime2(0) NULL,
    CashSales decimal(18,2) NOT NULL CONSTRAINT DF_WorkShift_CashSales DEFAULT 0,
    TransferSales decimal(18,2) NOT NULL CONSTRAINT DF_WorkShift_TransferSales DEFAULT 0,
    CashRefunds decimal(18,2) NOT NULL CONSTRAINT DF_WorkShift_CashRefunds DEFAULT 0,
    TheoreticalCash AS (OpeningCash + CashSales - CashRefunds) PERSISTED,
    ActualCash decimal(18,2) NULL,
    DifferenceAmount AS (CASE WHEN ActualCash IS NULL THEN NULL ELSE ActualCash - (OpeningCash + CashSales - CashRefunds) END) PERSISTED,
    CashHandover decimal(18,2) NULL,
    DifferenceNote nvarchar(500) NULL,
    Status varchar(20) NOT NULL CONSTRAINT DF_WorkShift_Status DEFAULT 'OPEN',
    CONSTRAINT FK_WorkShift_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_WorkShift_Amounts CHECK (OpeningCash >= 0 AND CashSales >= 0 AND TransferSales >= 0 AND CashRefunds >= 0),
    CONSTRAINT CK_WorkShift_Status CHECK (Status IN ('OPEN','CLOSED')),
    CONSTRAINT CK_WorkShift_Close CHECK ((Status = 'OPEN' AND ClosedAt IS NULL) OR (Status = 'CLOSED' AND ClosedAt IS NOT NULL))
);
GO
CREATE UNIQUE INDEX UX_WorkShift_OneOpenPerEmployee ON dbo.WorkShift(EmployeeId) WHERE Status = 'OPEN';
GO

/* 5. CUSTOMER / LOYALTY / VOUCHER */
CREATE TABLE dbo.Customer (
    CustomerId varchar(20) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    FullName nvarchar(150) NOT NULL,
    Phone varchar(20) NOT NULL CONSTRAINT UQ_Customer_Phone UNIQUE,
    Email varchar(150) NULL,
    Address nvarchar(300) NULL,
    LoyaltyPoints int NOT NULL CONSTRAINT DF_Customer_Points DEFAULT 0,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Customer_CreatedAt DEFAULT SYSDATETIME(),
    IsActive bit NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT 1,
    CONSTRAINT CK_Customer_Points CHECK (LoyaltyPoints >= 0)
);
GO

CREATE TABLE dbo.LoyaltyTransaction (
    LoyaltyTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoyaltyTransaction PRIMARY KEY,
    CustomerId varchar(20) NOT NULL,
    TransactionType varchar(20) NOT NULL,
    Points int NOT NULL,
    MoneyValue decimal(18,2) NULL,
    ReferenceCode varchar(20) NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_LoyaltyTransaction_Date DEFAULT SYSDATETIME(),
    Note nvarchar(300) NULL,
    CONSTRAINT FK_Loyalty_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(CustomerId),
    CONSTRAINT CK_Loyalty_Type CHECK (TransactionType IN ('EARN','REDEEM','RECOVER','REFUND')),
    CONSTRAINT CK_Loyalty_Points CHECK (Points > 0)
);
GO

CREATE TABLE dbo.Voucher (
    VoucherId varchar(20) NOT NULL CONSTRAINT PK_Voucher PRIMARY KEY,
    VoucherCode varchar(40) NOT NULL CONSTRAINT UQ_Voucher_Code UNIQUE,
    Description nvarchar(300) NULL,
    DiscountType varchar(10) NOT NULL, -- AMOUNT / PERCENT
    DiscountValue decimal(18,2) NOT NULL,
    MinimumOrder decimal(18,2) NOT NULL CONSTRAINT DF_Voucher_MinOrder DEFAULT 0,
    MaximumDiscount decimal(18,2) NULL,
    UsageLimit int NULL,
    UsedCount int NOT NULL CONSTRAINT DF_Voucher_UsedCount DEFAULT 0,
    StartDate datetime2(0) NOT NULL,
    EndDate datetime2(0) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Voucher_IsActive DEFAULT 1,
    CONSTRAINT CK_Voucher_Type CHECK (DiscountType IN ('AMOUNT','PERCENT')),
    CONSTRAINT CK_Voucher_Value CHECK (DiscountValue > 0 AND MinimumOrder >= 0 AND UsedCount >= 0),
    CONSTRAINT CK_Voucher_Dates CHECK (EndDate > StartDate),
    CONSTRAINT CK_Voucher_Usage CHECK (UsageLimit IS NULL OR UsageLimit >= UsedCount)
);
GO

/* 6. PRESCRIPTION */
CREATE TABLE dbo.Prescription (
    PrescriptionId varchar(20) NOT NULL CONSTRAINT PK_Prescription PRIMARY KEY,
    PatientName nvarchar(150) NOT NULL,
    PatientPhone varchar(20) NULL,
    PrescribedDate date NOT NULL,
    DoctorName nvarchar(150) NULL,
    MedicalFacility nvarchar(200) NULL,
    CreatedBy varchar(20) NOT NULL,
    Status varchar(20) NOT NULL CONSTRAINT DF_Prescription_Status DEFAULT 'UNSOLD',
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Prescription_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Prescription_Employee FOREIGN KEY (CreatedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_Prescription_Status CHECK (Status IN ('UNSOLD','PARTIAL','SOLD'))
);
GO

CREATE TABLE dbo.PrescriptionDetail (
    PrescriptionDetailId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PrescriptionDetail PRIMARY KEY,
    PrescriptionId varchar(20) NOT NULL,
    MedicineId varchar(20) NOT NULL,
    PrescribedUnit nvarchar(30) NOT NULL,
    PrescribedQuantity decimal(18,3) NOT NULL,
    DosageInstruction nvarchar(500) NULL,
    CONSTRAINT FK_PrescriptionDetail_Prescription FOREIGN KEY (PrescriptionId) REFERENCES dbo.Prescription(PrescriptionId),
    CONSTRAINT FK_PrescriptionDetail_Medicine FOREIGN KEY (MedicineId) REFERENCES dbo.Medicine(MedicineId),
    CONSTRAINT CK_PrescriptionDetail_Qty CHECK (PrescribedQuantity > 0)
);
GO

/* 7. INVOICE / DETAILS / PAYMENTS */
CREATE TABLE dbo.Invoice (
    InvoiceId varchar(20) NOT NULL CONSTRAINT PK_Invoice PRIMARY KEY,
    ShiftId varchar(20) NOT NULL,
    EmployeeId varchar(20) NOT NULL,
    CustomerId varchar(20) NULL,
    PrescriptionId varchar(20) NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Invoice_CreatedAt DEFAULT SYSDATETIME(),
    PaidAt datetime2(0) NULL,
    Subtotal decimal(18,2) NOT NULL CONSTRAINT DF_Invoice_Subtotal DEFAULT 0,
    PointDiscount decimal(18,2) NOT NULL CONSTRAINT DF_Invoice_PointDiscount DEFAULT 0,
    VoucherDiscount decimal(18,2) NOT NULL CONSTRAINT DF_Invoice_VoucherDiscount DEFAULT 0,
    TotalAmount decimal(18,2) NOT NULL CONSTRAINT DF_Invoice_Total DEFAULT 0,
    AmountPaid decimal(18,2) NOT NULL CONSTRAINT DF_Invoice_Paid DEFAULT 0,
    Status varchar(25) NOT NULL CONSTRAINT DF_Invoice_Status DEFAULT 'PENDING',
    CancelledBy varchar(20) NULL,
    CancelledAt datetime2(0) NULL,
    CancelReason nvarchar(500) NULL,
    CONSTRAINT FK_Invoice_Shift FOREIGN KEY (ShiftId) REFERENCES dbo.WorkShift(ShiftId),
    CONSTRAINT FK_Invoice_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT FK_Invoice_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(CustomerId),
    CONSTRAINT FK_Invoice_Prescription FOREIGN KEY (PrescriptionId) REFERENCES dbo.Prescription(PrescriptionId),
    CONSTRAINT FK_Invoice_CancelledBy FOREIGN KEY (CancelledBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_Invoice_Amounts CHECK (Subtotal >= 0 AND PointDiscount >= 0 AND VoucherDiscount >= 0 AND TotalAmount >= 0 AND AmountPaid >= 0),
    CONSTRAINT CK_Invoice_Status CHECK (Status IN ('PENDING','PAID','CANCELLED','RETURNED_PARTIAL','RETURNED_FULL'))
);
GO

CREATE TABLE dbo.InvoiceDetail (
    InvoiceDetailId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceDetail PRIMARY KEY,
    InvoiceId varchar(20) NOT NULL,
    MedicineId varchar(20) NOT NULL,
    MedicineUnitId int NOT NULL,
    BatchId varchar(25) NOT NULL,
    UnitNameSnapshot nvarchar(30) NOT NULL,
    ConversionFactorSnapshot decimal(18,3) NOT NULL,
    Quantity decimal(18,3) NOT NULL,
    QuantityBase decimal(18,3) NOT NULL,
    UnitPrice decimal(18,2) NOT NULL,
    DiscountAllocated decimal(18,2) NOT NULL CONSTRAINT DF_InvoiceDetail_Discount DEFAULT 0,
    LineTotal decimal(18,2) NOT NULL,
    ReturnedQuantity decimal(18,3) NOT NULL CONSTRAINT DF_InvoiceDetail_Returned DEFAULT 0,
    CONSTRAINT FK_InvoiceDetail_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoice(InvoiceId),
    CONSTRAINT FK_InvoiceDetail_Medicine FOREIGN KEY (MedicineId) REFERENCES dbo.Medicine(MedicineId),
    CONSTRAINT FK_InvoiceDetail_Unit FOREIGN KEY (MedicineUnitId) REFERENCES dbo.MedicineUnit(MedicineUnitId),
    CONSTRAINT FK_InvoiceDetail_Batch FOREIGN KEY (BatchId) REFERENCES dbo.MedicineBatch(BatchId),
    CONSTRAINT CK_InvoiceDetail_Qty CHECK (Quantity > 0 AND QuantityBase > 0 AND ConversionFactorSnapshot > 0),
    CONSTRAINT CK_InvoiceDetail_Money CHECK (UnitPrice >= 0 AND DiscountAllocated >= 0 AND LineTotal >= 0),
    CONSTRAINT CK_InvoiceDetail_Returned CHECK (ReturnedQuantity >= 0 AND ReturnedQuantity <= Quantity)
);
GO

CREATE TABLE dbo.InvoicePayment (
    PaymentId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoicePayment PRIMARY KEY,
    InvoiceId varchar(20) NOT NULL,
    PaymentMethod varchar(20) NOT NULL,
    Amount decimal(18,2) NOT NULL,
    TransactionReference varchar(100) NULL,
    PaidAt datetime2(0) NOT NULL CONSTRAINT DF_InvoicePayment_PaidAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_InvoicePayment_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoice(InvoiceId),
    CONSTRAINT CK_InvoicePayment_Method CHECK (PaymentMethod IN ('CASH','TRANSFER','QR')),
    CONSTRAINT CK_InvoicePayment_Amount CHECK (Amount > 0)
);
GO

CREATE TABLE dbo.VoucherUsage (
    VoucherUsageId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_VoucherUsage PRIMARY KEY,
    VoucherId varchar(20) NOT NULL,
    InvoiceId varchar(20) NOT NULL,
    CustomerId varchar(20) NULL,
    DiscountAmount decimal(18,2) NOT NULL,
    UsedAt datetime2(0) NOT NULL CONSTRAINT DF_VoucherUsage_UsedAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_VoucherUsage_Voucher FOREIGN KEY (VoucherId) REFERENCES dbo.Voucher(VoucherId),
    CONSTRAINT FK_VoucherUsage_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoice(InvoiceId),
    CONSTRAINT FK_VoucherUsage_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(CustomerId),
    CONSTRAINT UQ_VoucherUsage_Invoice UNIQUE (InvoiceId),
    CONSTRAINT CK_VoucherUsage_Discount CHECK (DiscountAmount >= 0)
);
GO

/* 8. INVENTORY RESERVATION / STOCK LEDGER */
CREATE TABLE dbo.InventoryReservation (
    ReservationId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryReservation PRIMARY KEY,
    InvoiceId varchar(20) NOT NULL,
    InvoiceDetailId bigint NULL,
    BatchId varchar(25) NOT NULL,
    ReservedQuantity decimal(18,3) NOT NULL,
    ReservedAt datetime2(0) NOT NULL CONSTRAINT DF_Reservation_ReservedAt DEFAULT SYSDATETIME(),
    ExpiresAt datetime2(0) NOT NULL,
    Status varchar(20) NOT NULL CONSTRAINT DF_Reservation_Status DEFAULT 'ACTIVE',
    ReleasedAt datetime2(0) NULL,
    CONSTRAINT FK_Reservation_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoice(InvoiceId),
    CONSTRAINT FK_Reservation_InvoiceDetail FOREIGN KEY (InvoiceDetailId) REFERENCES dbo.InvoiceDetail(InvoiceDetailId),
    CONSTRAINT FK_Reservation_Batch FOREIGN KEY (BatchId) REFERENCES dbo.MedicineBatch(BatchId),
    CONSTRAINT CK_Reservation_Qty CHECK (ReservedQuantity > 0),
    CONSTRAINT CK_Reservation_Status CHECK (Status IN ('ACTIVE','CONSUMED','RELEASED','EXPIRED'))
);
GO

CREATE TABLE dbo.StockTransaction (
    StockTransactionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockTransaction PRIMARY KEY,
    BatchId varchar(25) NOT NULL,
    TransactionType varchar(20) NOT NULL,
    QuantityChange decimal(18,3) NOT NULL,
    ReferenceType varchar(30) NOT NULL,
    ReferenceCode varchar(20) NOT NULL,
    CreatedBy varchar(20) NOT NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_StockTransaction_Date DEFAULT SYSDATETIME(),
    Note nvarchar(300) NULL,
    CONSTRAINT FK_StockTransaction_Batch FOREIGN KEY (BatchId) REFERENCES dbo.MedicineBatch(BatchId),
    CONSTRAINT FK_StockTransaction_Employee FOREIGN KEY (CreatedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_StockTransaction_Type CHECK (TransactionType IN ('IMPORT','SALE','RETURN','STOCKTAKE','DISPOSAL','CANCEL_SALE')),
    CONSTRAINT CK_StockTransaction_Qty CHECK (QuantityChange <> 0)
);
GO

/* 9. STOCKTAKE */
CREATE TABLE dbo.Stocktake (
    StocktakeId varchar(20) NOT NULL CONSTRAINT PK_Stocktake PRIMARY KEY,
    CreatedBy varchar(20) NOT NULL,
    ApprovedBy varchar(20) NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Stocktake_CreatedAt DEFAULT SYSDATETIME(),
    ApprovedAt datetime2(0) NULL,
    Status varchar(20) NOT NULL CONSTRAINT DF_Stocktake_Status DEFAULT 'PENDING',
    Note nvarchar(500) NULL,
    CONSTRAINT FK_Stocktake_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT FK_Stocktake_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_Stocktake_Status CHECK (Status IN ('PENDING','APPROVED','REJECTED'))
);
GO

CREATE TABLE dbo.StocktakeDetail (
    StocktakeDetailId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_StocktakeDetail PRIMARY KEY,
    StocktakeId varchar(20) NOT NULL,
    BatchId varchar(25) NOT NULL,
    SystemQuantity decimal(18,3) NOT NULL,
    ActualQuantity decimal(18,3) NOT NULL,
    DifferenceQuantity AS (ActualQuantity - SystemQuantity) PERSISTED,
    DifferenceReason nvarchar(300) NULL,
    CONSTRAINT FK_StocktakeDetail_Stocktake FOREIGN KEY (StocktakeId) REFERENCES dbo.Stocktake(StocktakeId),
    CONSTRAINT FK_StocktakeDetail_Batch FOREIGN KEY (BatchId) REFERENCES dbo.MedicineBatch(BatchId),
    CONSTRAINT UQ_StocktakeDetail UNIQUE (StocktakeId, BatchId),
    CONSTRAINT CK_StocktakeDetail_Qty CHECK (SystemQuantity >= 0 AND ActualQuantity >= 0)
);
GO

/* 10. DISPOSAL */
CREATE TABLE dbo.Disposal (
    DisposalId varchar(20) NOT NULL CONSTRAINT PK_Disposal PRIMARY KEY,
    CreatedBy varchar(20) NOT NULL,
    ApprovedBy varchar(20) NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Disposal_CreatedAt DEFAULT SYSDATETIME(),
    ApprovedAt datetime2(0) NULL,
    Status varchar(20) NOT NULL CONSTRAINT DF_Disposal_Status DEFAULT 'PENDING',
    Reason nvarchar(500) NOT NULL,
    TotalLoss decimal(18,2) NOT NULL CONSTRAINT DF_Disposal_TotalLoss DEFAULT 0,
    CONSTRAINT FK_Disposal_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT FK_Disposal_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_Disposal_Status CHECK (Status IN ('PENDING','APPROVED','REJECTED')),
    CONSTRAINT CK_Disposal_Loss CHECK (TotalLoss >= 0)
);
GO

CREATE TABLE dbo.DisposalDetail (
    DisposalDetailId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DisposalDetail PRIMARY KEY,
    DisposalId varchar(20) NOT NULL,
    BatchId varchar(25) NOT NULL,
    Quantity decimal(18,3) NOT NULL,
    PurchasePriceBase decimal(18,4) NOT NULL,
    LossAmount AS (Quantity * PurchasePriceBase) PERSISTED,
    CONSTRAINT FK_DisposalDetail_Disposal FOREIGN KEY (DisposalId) REFERENCES dbo.Disposal(DisposalId),
    CONSTRAINT FK_DisposalDetail_Batch FOREIGN KEY (BatchId) REFERENCES dbo.MedicineBatch(BatchId),
    CONSTRAINT CK_DisposalDetail_Qty CHECK (Quantity > 0 AND PurchasePriceBase >= 0)
);
GO

/* 11. RETURNS */
CREATE TABLE dbo.ReturnReceipt (
    ReturnId varchar(20) NOT NULL CONSTRAINT PK_ReturnReceipt PRIMARY KEY,
    OriginalInvoiceId varchar(20) NOT NULL,
    ShiftId varchar(20) NOT NULL,
    EmployeeId varchar(20) NOT NULL,
    ReturnDate datetime2(0) NOT NULL CONSTRAINT DF_ReturnReceipt_Date DEFAULT SYSDATETIME(),
    Reason nvarchar(500) NOT NULL,
    RefundMethod varchar(20) NOT NULL,
    TotalRefund decimal(18,2) NOT NULL,
    PointsRecovered int NOT NULL CONSTRAINT DF_ReturnReceipt_Points DEFAULT 0,
    Status varchar(20) NOT NULL CONSTRAINT DF_ReturnReceipt_Status DEFAULT 'COMPLETED',
    CONSTRAINT FK_Return_Invoice FOREIGN KEY (OriginalInvoiceId) REFERENCES dbo.Invoice(InvoiceId),
    CONSTRAINT FK_Return_Shift FOREIGN KEY (ShiftId) REFERENCES dbo.WorkShift(ShiftId),
    CONSTRAINT FK_Return_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee(EmployeeId),
    CONSTRAINT CK_Return_Method CHECK (RefundMethod IN ('CASH','TRANSFER')),
    CONSTRAINT CK_Return_Amounts CHECK (TotalRefund >= 0 AND PointsRecovered >= 0),
    CONSTRAINT CK_Return_Status CHECK (Status IN ('COMPLETED','CANCELLED'))
);
GO

CREATE TABLE dbo.ReturnDetail (
    ReturnDetailId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReturnDetail PRIMARY KEY,
    ReturnId varchar(20) NOT NULL,
    InvoiceDetailId bigint NOT NULL,
    BatchId varchar(25) NOT NULL,
    Quantity decimal(18,3) NOT NULL,
    RefundUnitPrice decimal(18,2) NOT NULL,
    RefundAmount decimal(18,2) NOT NULL,
    CONSTRAINT FK_ReturnDetail_Return FOREIGN KEY (ReturnId) REFERENCES dbo.ReturnReceipt(ReturnId),
    CONSTRAINT FK_ReturnDetail_InvoiceDetail FOREIGN KEY (InvoiceDetailId) REFERENCES dbo.InvoiceDetail(InvoiceDetailId),
    CONSTRAINT FK_ReturnDetail_Batch FOREIGN KEY (BatchId) REFERENCES dbo.MedicineBatch(BatchId),
    CONSTRAINT CK_ReturnDetail_Values CHECK (Quantity > 0 AND RefundUnitPrice >= 0 AND RefundAmount >= 0)
);
GO

/* 12. AUDIT LOG */
CREATE TABLE dbo.AuditLog (
    AuditLogId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    EmployeeId varchar(20) NULL,
    ActionName varchar(100) NOT NULL,
    EntityName varchar(100) NOT NULL,
    EntityId varchar(50) NULL,
    OldValue nvarchar(max) NULL,
    NewValue nvarchar(max) NULL,
    Reason nvarchar(500) NULL,
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_AuditLog_Date DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AuditLog_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee(EmployeeId)
);
GO

/* Useful indexes */
CREATE INDEX IX_Medicine_Name ON dbo.Medicine(MedicineName);
CREATE INDEX IX_Batch_FEFO ON dbo.MedicineBatch(MedicineId, ExpiryDate) INCLUDE (PhysicalStock, BatchStatus);
CREATE INDEX IX_Batch_Expiry ON dbo.MedicineBatch(ExpiryDate, BatchStatus);
CREATE INDEX IX_Invoice_CreatedAt ON dbo.Invoice(CreatedAt);
CREATE INDEX IX_Invoice_Status ON dbo.Invoice(Status, CreatedAt);
CREATE INDEX IX_Reservation_Invoice_Status ON dbo.InventoryReservation(InvoiceId, Status);
CREATE INDEX IX_StockTransaction_Batch_Date ON dbo.StockTransaction(BatchId, CreatedAt);
CREATE INDEX IX_Prescription_PatientPhone ON dbo.Prescription(PatientPhone);
GO

/* ============================================================
   SEED DATA
   Demo password hashes are placeholders for coursework.
   Replace them with BCrypt/Argon2 hashes generated by the C# app.
   ============================================================ */

INSERT INTO dbo.Role(RoleId, RoleName, Description) VALUES
('ADMIN', N'Quản lý', N'Toàn quyền quản trị hệ thống'),
('PHARMACIST', N'Dược sĩ', N'Bán hàng, thanh toán, ca làm việc, đổi trả'),
('WAREHOUSE', N'Nhân viên kho', N'Nhập kho, kiểm kê và đề xuất xuất hủy');
GO

INSERT INTO dbo.Employee(EmployeeId, RoleId, FullName, Username, PasswordHash, Phone, Email) VALUES
('NV001','ADMIN',N'Nguyễn Minh Quản','admin','CHANGE_ME_BCRYPT_HASH_ADMIN','0901000001','admin@quaythuoc.local'),
('NV002','PHARMACIST',N'Trần Thị Dược','duocsi01','CHANGE_ME_BCRYPT_HASH_PHARMACIST','0901000002','duocsi01@quaythuoc.local'),
('NV003','WAREHOUSE',N'Lê Văn Kho','kho01','CHANGE_ME_BCRYPT_HASH_WAREHOUSE','0901000003','kho01@quaythuoc.local');
GO

INSERT INTO dbo.MedicineCategory(CategoryId, CategoryName, Description) VALUES
('CAT001',N'Giảm đau - Hạ sốt',N'Thuốc giảm đau, hạ sốt'),
('CAT002',N'Vitamin - Khoáng chất',N'Vitamin và khoáng chất'),
('CAT003',N'Tiêu hóa',N'Thuốc hỗ trợ tiêu hóa'),
('CAT004',N'Tim mạch',N'Thuốc nhóm tim mạch'),
('CAT005',N'Vật tư y tế',N'Vật tư y tế thông dụng');
GO

INSERT INTO dbo.Medicine(MedicineId, CategoryId, MedicineName, ActiveIngredient, Strength, DosageForm, BaseUnit, MinimumStock, RequiresPrescription, Barcode, Description) VALUES
('MED001','CAT001',N'Panadol Extra',N'Paracetamol + Caffeine',N'500mg + 65mg',N'Viên nén',N'Viên',100,0,'893000000001',N'Thuốc giảm đau, hạ sốt'),
('MED002','CAT001',N'Paracetamol 500mg',N'Paracetamol',N'500mg',N'Viên nén',N'Viên',200,0,'893000000002',N'Thuốc giảm đau, hạ sốt'),
('MED003','CAT002',N'Vitamin C 500mg',N'Acid ascorbic',N'500mg',N'Viên nén',N'Viên',100,0,'893000000003',N'Bổ sung vitamin C'),
('MED004','CAT003',N'Berberin',N'Berberine',N'10mg',N'Viên nén',N'Viên',100,0,'893000000004',N'Hỗ trợ điều trị tiêu chảy'),
('MED005','CAT001',N'Amoxicillin 500mg',N'Amoxicillin',N'500mg',N'Viên nang',N'Viên',100,1,'893000000005',N'Thuốc kháng sinh kê đơn');
GO

/* Unit prices are illustrative seed values. Factor is in smallest units. */
INSERT INTO dbo.MedicineUnit(MedicineId, UnitName, ConversionFactor, SalePrice, IsBaseUnit) VALUES
('MED001',N'Hộp',100,100000,0),('MED001',N'Vỉ',10,11000,0),('MED001',N'Viên',1,1210,1),
('MED002',N'Hộp',100,80000,0),('MED002',N'Vỉ',10,9000,0),('MED002',N'Viên',1,990,1),
('MED003',N'Hộp',100,90000,0),('MED003',N'Vỉ',10,10000,0),('MED003',N'Viên',1,1100,1),
('MED004',N'Hộp',100,60000,0),('MED004',N'Vỉ',10,7000,0),('MED004',N'Viên',1,770,1),
('MED005',N'Hộp',100,150000,0),('MED005',N'Vỉ',10,16000,0),('MED005',N'Viên',1,1760,1);
GO

INSERT INTO dbo.Supplier(SupplierId, SupplierName, Phone, Email, Address, TaxCode) VALUES
('NCC001',N'Công ty Dược phẩm Hà Thành','0243000001','sales@hathanh.local',N'Hà Nội','0101000001'),
('NCC002',N'Công ty Dược phẩm Minh Châu','0243000002','sales@minhchau.local',N'Hà Nội','0101000002'),
('NCC003',N'Nhà phân phối Thiết bị Y tế An Khang','0243000003','sales@ankhang.local',N'Bắc Ninh','0101000003');
GO

INSERT INTO dbo.Customer(CustomerId, FullName, Phone, Email, Address, LoyaltyPoints) VALUES
('KH001',N'Phạm Thu Hà','0912000001','ha@example.local',N'Hà Nội',120),
('KH002',N'Nguyễn Văn Nam','0912000002','nam@example.local',N'Hà Nội',50),
('KH003',N'Trần Ngọc Mai','0912000003',NULL,N'Bắc Ninh',0),
('KH004',N'Lê Hoàng Long','0912000004',NULL,N'Hà Nội',300),
('KH005',N'Đỗ Minh Anh','0912000005',NULL,N'Hà Nội',20);
GO

INSERT INTO dbo.Voucher(VoucherId, VoucherCode, Description, DiscountType, DiscountValue, MinimumOrder, MaximumDiscount, UsageLimit, UsedCount, StartDate, EndDate) VALUES
('VC001','WELCOME20',N'Giảm 20.000đ cho đơn từ 200.000đ','AMOUNT',20000,200000,20000,100,0,'2026-01-01','2027-01-01'),
('VC002','SALE5',N'Giảm 5% tối đa 30.000đ','PERCENT',5,100000,30000,200,0,'2026-01-01','2027-01-01');
GO

/* Demo opening shift for pharmacist. This is a sample OPEN shift. */
INSERT INTO dbo.WorkShift(ShiftId, EmployeeId, OpenedAt, OpeningCash, Status)
VALUES ('CA001','NV002',DATEADD(HOUR,-2,SYSDATETIME()),1000000,'OPEN');
GO

/* Sample receipts */
INSERT INTO dbo.GoodsReceipt(ReceiptId, SupplierId, CreatedBy, ReceiptDate, Status, Note, ConfirmedBy, ConfirmedAt) VALUES
('PN001','NCC001','NV003',DATEADD(DAY,-20,SYSDATETIME()),'CONFIRMED',N'Nhập hàng đầu kỳ','NV003',DATEADD(DAY,-20,SYSDATETIME())),
('PN002','NCC002','NV003',DATEADD(DAY,-10,SYSDATETIME()),'CONFIRMED',N'Nhập bổ sung','NV003',DATEADD(DAY,-10,SYSDATETIME()));
GO

INSERT INTO dbo.GoodsReceiptDetail(ReceiptId, MedicineId, UnitName, Quantity, ConversionFactor, QuantityBase, PurchaseUnitPrice, BatchNumber, ManufactureDate, ExpiryDate) VALUES
('PN001','MED001',N'Hộp',20,100,2000,70000,'PAN-2601',DATEADD(MONTH,-2,CONVERT(date,SYSDATETIME())),DATEADD(MONTH,18,CONVERT(date,SYSDATETIME()))),
('PN001','MED002',N'Hộp',30,100,3000,55000,'PAR-2601',DATEADD(MONTH,-3,CONVERT(date,SYSDATETIME())),DATEADD(MONTH,20,CONVERT(date,SYSDATETIME()))),
('PN001','MED003',N'Hộp',15,100,1500,60000,'VTC-2601',DATEADD(MONTH,-1,CONVERT(date,SYSDATETIME())),DATEADD(MONTH,24,CONVERT(date,SYSDATETIME()))),
('PN002','MED004',N'Hộp',10,100,1000,40000,'BER-2602',DATEADD(MONTH,-1,CONVERT(date,SYSDATETIME())),DATEADD(MONTH,12,CONVERT(date,SYSDATETIME()))),
('PN002','MED005',N'Hộp',10,100,1000,110000,'AMO-2602',DATEADD(MONTH,-1,CONVERT(date,SYSDATETIME())),DATEADD(MONTH,15,CONVERT(date,SYSDATETIME())));
GO

INSERT INTO dbo.MedicineBatch(BatchId, ReceiptDetailId, MedicineId, BatchNumber, ManufactureDate, ExpiryDate, PurchasePriceBase, InitialQuantity, PhysicalStock, BatchStatus)
SELECT CONCAT('LO', RIGHT(CONCAT('0000', ReceiptDetailId),4)),
       ReceiptDetailId, MedicineId, BatchNumber, ManufactureDate, ExpiryDate,
       PurchaseUnitPrice / NULLIF(ConversionFactor,0), QuantityBase, QuantityBase, 'ACTIVE'
FROM dbo.GoodsReceiptDetail;
GO

/* Stock ledger for sample imports */
INSERT INTO dbo.StockTransaction(BatchId, TransactionType, QuantityChange, ReferenceType, ReferenceCode, CreatedBy, Note)
SELECT b.BatchId, 'IMPORT', b.InitialQuantity, 'GOODS_RECEIPT', gd.ReceiptId, 'NV003', N'Tồn đầu kỳ từ phiếu nhập'
FROM dbo.MedicineBatch b
JOIN dbo.GoodsReceiptDetail gd ON gd.ReceiptDetailId = b.ReceiptDetailId;
GO

/* Sample prescription (not yet sold) */
INSERT INTO dbo.Prescription(PrescriptionId, PatientName, PatientPhone, PrescribedDate, DoctorName, MedicalFacility, CreatedBy, Status)
VALUES ('DT001',N'Nguyễn Thị Lan','0988000001',CONVERT(date,SYSDATETIME()),N'BS. Nguyễn Văn A',N'Phòng khám Minh Tâm','NV002','UNSOLD');

INSERT INTO dbo.PrescriptionDetail(PrescriptionId, MedicineId, PrescribedUnit, PrescribedQuantity, DosageInstruction) VALUES
('DT001','MED005',N'Vỉ',2,N'Uống theo hướng dẫn của bác sĩ');
GO

/* Example price history */
INSERT INTO dbo.MedicinePriceHistory(MedicineUnitId, OldPrice, NewPrice, ChangedBy, Note)
SELECT TOP (1) MedicineUnitId, 10000, SalePrice, 'NV001', N'Dữ liệu lịch sử giá mẫu'
FROM dbo.MedicineUnit WHERE MedicineId='MED001' AND UnitName=N'Vỉ';
GO

/* Quick verification queries */
SELECT * FROM dbo.Role;
SELECT MedicineId, MedicineName, RequiresPrescription, BusinessStatus FROM dbo.Medicine;
SELECT MedicineId, UnitName, ConversionFactor, SalePrice FROM dbo.MedicineUnit ORDER BY MedicineId, ConversionFactor DESC;
SELECT b.BatchId, m.MedicineName, b.BatchNumber, b.ExpiryDate, b.PhysicalStock
FROM dbo.MedicineBatch b JOIN dbo.Medicine m ON m.MedicineId=b.MedicineId
ORDER BY m.MedicineName, b.ExpiryDate;
SELECT * FROM dbo.WorkShift;
GO

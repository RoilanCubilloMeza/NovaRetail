/*
=====================================================================
  Script de creación de esquema para base de datos tipo RMH (POS)
  Generado desde el proyecto NovaRetail

  Base de datos origen: BM_POS_CEDI
  Fecha: 2026-04-20

  INSTRUCCIONES DE USO:
    1. Conéctate (o cambia el contexto) a la base de datos destino
       antes de ejecutar este script.
       Ejemplo:  USE [MiNuevaBase]  o con sqlcmd: -d MiNuevaBase
    2. No se requieren permisos sobre master.
    3. Es idempotente: puede ejecutarse varias veces sin error.

  NOTA: Todas las tablas se crean vacías EXCEPTO AVS_Parametros
        que se carga con su información de configuración.
=====================================================================
*/

-- =====================================================================
-- TABLAS DEL SISTEMA RMH (Punto de Venta)
-- Estructura exacta de BM_POS_CEDI
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- 1. Store (Tiendas/Sucursales)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Store', N'U') IS NULL
CREATE TABLE dbo.Store (
    ID                  INT             NOT NULL PRIMARY KEY,
    Name                VARCHAR(50)     NOT NULL DEFAULT '',
    StoreCode           VARCHAR(30)     NOT NULL DEFAULT '',
    Region              VARCHAR(50)     NOT NULL DEFAULT '',
    Address1            VARCHAR(50)     NOT NULL DEFAULT '',
    Address2            VARCHAR(50)     NOT NULL DEFAULT '',
    City                VARCHAR(50)     NOT NULL DEFAULT '',
    Country             VARCHAR(20)     NOT NULL DEFAULT '',
    FaxNumber           VARCHAR(30)     NOT NULL DEFAULT '',
    PhoneNumber         VARCHAR(30)     NOT NULL DEFAULT '',
    State               VARCHAR(20)     NOT NULL DEFAULT '',
    Zip                 VARCHAR(15)     NOT NULL DEFAULT '',
    ParentStoreID       INT             NOT NULL DEFAULT 0,
    ScheduleHourMask1   INT             NOT NULL DEFAULT 0,
    ScheduleHourMask2   INT             NOT NULL DEFAULT 0,
    ScheduleHourMask3   INT             NOT NULL DEFAULT 0,
    ScheduleHourMask4   INT             NOT NULL DEFAULT 0,
    ScheduleHourMask5   INT             NOT NULL DEFAULT 0,
    ScheduleHourMask6   INT             NOT NULL DEFAULT 0,
    ScheduleHourMask7   INT             NOT NULL DEFAULT 0,
    ScheduleMinute      INT             NOT NULL DEFAULT 0,
    RetryCount          INT             NOT NULL DEFAULT 0,
    RetryDelay          INT             NOT NULL DEFAULT 0,
    LastUpdated         DATETIME        NULL,
    AccountName         NVARCHAR(100)   NOT NULL DEFAULT '',
    Password            NVARCHAR(100)   NOT NULL DEFAULT '',
    Inactive            BIT             NOT NULL DEFAULT 0,
    SyncedStoreStatus   BIT             NOT NULL DEFAULT 0,
    SyncGuid            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 2. Department (Departamentos/Categorías principales)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Department', N'U') IS NULL
CREATE TABLE dbo.Department (
    HQID        INT              NOT NULL DEFAULT 0,
    ID          INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(60)     NOT NULL DEFAULT '',
    code        NVARCHAR(34)     NOT NULL DEFAULT '',
    SyncGuid    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 3. Category (Subcategorías)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
CREATE TABLE dbo.Category (
    HQID            INT              NOT NULL DEFAULT 0,
    ID              INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    DepartmentID    INT              NOT NULL DEFAULT 0,
    Name            NVARCHAR(60)     NOT NULL DEFAULT '',
    Code            NVARCHAR(34)     NOT NULL DEFAULT '',
    SyncGuid        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 4. Tax (Impuestos)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Tax', N'U') IS NULL
CREATE TABLE dbo.Tax (
    FixedAmount         MONEY           NOT NULL DEFAULT 0,
    HQID                INT             NOT NULL DEFAULT 0,
    ID                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Description         NVARCHAR(50)    NOT NULL DEFAULT '',
    Percentage          REAL            NOT NULL DEFAULT 0,
    UsePartialDollar    BIT             NOT NULL DEFAULT 0,
    ItemMaximum         MONEY           NOT NULL DEFAULT 0,
    IncludePreviousTax  BIT             NOT NULL DEFAULT 0,
    Code                NVARCHAR(34)    NOT NULL DEFAULT '',
    ItemMinimum         MONEY           NOT NULL DEFAULT 0,
    ApplyOverMinimum    BIT             NOT NULL DEFAULT 0,
    SyncGuid            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 5. Item (Artículos/Productos)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Item', N'U') IS NULL
CREATE TABLE dbo.Item (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ItemLookupCode          NVARCHAR(50)     NOT NULL DEFAULT '',
    Description             NVARCHAR(160)    NOT NULL DEFAULT '',
    ExtendedDescription     NTEXT            NULL,
    Quantity                FLOAT            NOT NULL DEFAULT 0,
    QuantityCommitted       FLOAT            NOT NULL DEFAULT 0,
    DepartmentID            INT              NOT NULL DEFAULT 0,
    CategoryID              INT              NOT NULL DEFAULT 0,
    Price                   MONEY            NOT NULL DEFAULT 0,
    PriceA                  MONEY            NOT NULL DEFAULT 0,
    PriceB                  MONEY            NOT NULL DEFAULT 0,
    PriceC                  MONEY            NOT NULL DEFAULT 0,
    TaxID                   INT              NOT NULL DEFAULT 0,
    Cost                    MONEY            NOT NULL DEFAULT 0,
    SubDescription1         NVARCHAR(60)     NOT NULL DEFAULT '',
    SubDescription2         NVARCHAR(60)     NOT NULL DEFAULT '',
    SubDescription3         NVARCHAR(60)     NOT NULL DEFAULT '',
    WebItem                 BIT              NOT NULL DEFAULT 0,
    ItemType                SMALLINT         NOT NULL DEFAULT 0,
    HQID                    INT              NOT NULL DEFAULT 0,
    LastUpdated             DATETIME         NOT NULL DEFAULT GETDATE(),
    BinLocation             NVARCHAR(40)     NOT NULL DEFAULT '',
    BuydownPrice            MONEY            NOT NULL DEFAULT 0,
    BuydownQuantity         FLOAT            NOT NULL DEFAULT 0,
    CommissionAmount        MONEY            NOT NULL DEFAULT 0,
    CommissionMaximum       MONEY            NOT NULL DEFAULT 0,
    CommissionMode          INT              NOT NULL DEFAULT 0,
    CommissionPercentProfit REAL             NOT NULL DEFAULT 0,
    CommissionPercentSale   REAL             NOT NULL DEFAULT 0,
    FoodStampable           BIT              NOT NULL DEFAULT 0,
    ItemNotDiscountable     BIT              NOT NULL DEFAULT 0,
    LastReceived            DATETIME         NULL,
    Notes                   NTEXT            NULL,
    SerialNumberCount       INT              NOT NULL DEFAULT 0,
    TareWeightPercent       FLOAT            NOT NULL DEFAULT 0,
    MessageID               INT              NOT NULL DEFAULT 0,
    SalePrice               MONEY            NOT NULL DEFAULT 0,
    SaleStartDate           DATETIME         NULL,
    SaleEndDate             DATETIME         NULL,
    QuantityDiscountID      INT              NOT NULL DEFAULT 0,
    ReorderPoint            FLOAT            NOT NULL DEFAULT 0,
    RestockLevel            FLOAT            NOT NULL DEFAULT 0,
    TareWeight              FLOAT            NOT NULL DEFAULT 0,
    SupplierID              INT              NOT NULL DEFAULT 0,
    TagAlongItem            INT              NOT NULL DEFAULT 0,
    TagAlongQuantity        FLOAT            NOT NULL DEFAULT 0,
    ParentItem              INT              NOT NULL DEFAULT 0,
    ParentQuantity          FLOAT            NOT NULL DEFAULT 0,
    BarcodeFormat           SMALLINT         NOT NULL DEFAULT 0,
    PriceLowerBound         MONEY            NOT NULL DEFAULT 0,
    PriceUpperBound         MONEY            NOT NULL DEFAULT 0,
    PictureName             NVARCHAR(100)    NOT NULL DEFAULT '',
    LastSold                DATETIME         NULL,
    UnitOfMeasure           NVARCHAR(8)      NOT NULL DEFAULT '',
    SubCategoryID           INT              NOT NULL DEFAULT 0,
    QuantityEntryNotAllowed BIT              NOT NULL DEFAULT 0,
    PriceMustBeEntered      BIT              NOT NULL DEFAULT 0,
    BlockSalesReason        NVARCHAR(60)     NOT NULL DEFAULT '',
    BlockSalesAfterDate     DATETIME         NULL,
    Weight                  FLOAT            NOT NULL DEFAULT 0,
    Taxable                 BIT              NOT NULL DEFAULT 0,
    BlockSalesBeforeDate    DATETIME         NULL,
    LastCost                MONEY            NOT NULL DEFAULT 0,
    ReplacementCost         MONEY            NOT NULL DEFAULT 0,
    BlockSalesType          INT              NOT NULL DEFAULT 0,
    BlockSalesScheduleID    INT              NOT NULL DEFAULT 0,
    SaleType                INT              NOT NULL DEFAULT 0,
    SaleScheduleID          INT              NOT NULL DEFAULT 0,
    Consignment             BIT              NOT NULL DEFAULT 0,
    Inactive                BIT              NOT NULL DEFAULT 0,
    LastCounted             DATETIME         NULL,
    DoNotOrder              BIT              NOT NULL DEFAULT 0,
    MSRP                    MONEY            NOT NULL DEFAULT 0,
    DateCreated             DATETIME         NOT NULL DEFAULT GETDATE(),
    Content                 NTEXT            NULL,
    UsuallyShip             NVARCHAR(MAX)    NOT NULL DEFAULT '',
    NumberFormat            NVARCHAR(100)    NULL,
    ItemCannotBeRet         BIT              NULL DEFAULT 0,
    ItemCannotBeSold        BIT              NULL DEFAULT 0,
    IsAutogenerated         BIT              NULL DEFAULT 0,
    IsGlobalvoucher         BIT              NOT NULL DEFAULT 0,
    DeleteZeroBalanceEntry  BIT              NULL DEFAULT 0,
    TenderID                INT              NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 6. Customer (Clientes)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
CREATE TABLE dbo.Customer (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    AccountNumber           NVARCHAR(40)     NOT NULL DEFAULT '',
    AccountTypeID           INT              NOT NULL DEFAULT 0,
    Address                 NVARCHAR(100)    NOT NULL DEFAULT '',
    Address2                NVARCHAR(100)    NOT NULL DEFAULT '',
    AssessFinanceCharges    BIT              NOT NULL DEFAULT 0,
    City                    NVARCHAR(100)    NOT NULL DEFAULT '',
    Company                 NVARCHAR(100)    NOT NULL DEFAULT '',
    Country                 NVARCHAR(40)     NOT NULL DEFAULT '',
    CustomDate1             DATETIME         NULL,
    CustomDate2             DATETIME         NULL,
    CustomDate3             DATETIME         NULL,
    CustomDate4             DATETIME         NULL,
    CustomDate5             DATETIME         NULL,
    CustomNumber1           FLOAT            NOT NULL DEFAULT 0,
    CustomNumber2           FLOAT            NOT NULL DEFAULT 0,
    CustomNumber3           FLOAT            NOT NULL DEFAULT 0,
    CustomNumber4           FLOAT            NOT NULL DEFAULT 0,
    CustomNumber5           FLOAT            NOT NULL DEFAULT 0,
    CustomText1             NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText2             NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText3             NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText4             NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText5             NVARCHAR(60)     NOT NULL DEFAULT '',
    GlobalCustomer          BIT              NOT NULL DEFAULT 0,
    HQID                    INT              NOT NULL DEFAULT 0,
    LastStartingDate        DATETIME         NULL,
    LastClosingDate         DATETIME         NULL,
    LastUpdated             DATETIME         NOT NULL DEFAULT GETDATE(),
    LimitPurchase           BIT              NOT NULL DEFAULT 0,
    LastClosingBalance      MONEY            NOT NULL DEFAULT 0,
    PrimaryShipToID         INT              NOT NULL DEFAULT 0,
    State                   NVARCHAR(40)     NOT NULL DEFAULT '',
    StoreID                 INT              NOT NULL DEFAULT 0,
    LayawayCustomer         BIT              NOT NULL DEFAULT 0,
    Employee                BIT              NOT NULL DEFAULT 0,
    FirstName               NVARCHAR(60)     NOT NULL DEFAULT '',
    LastName                NVARCHAR(100)    NOT NULL DEFAULT '',
    Zip                     NVARCHAR(30)     NOT NULL DEFAULT '',
    AccountBalance          MONEY            NOT NULL DEFAULT 0,
    CreditLimit             MONEY            NOT NULL DEFAULT 0,
    TotalSales              MONEY            NOT NULL DEFAULT 0,
    AccountOpened           DATETIME         NOT NULL DEFAULT GETDATE(),
    LastVisit               DATETIME         NOT NULL DEFAULT GETDATE(),
    TotalVisits             INT              NOT NULL DEFAULT 0,
    TotalSavings            MONEY            NOT NULL DEFAULT 0,
    CurrentDiscount         REAL             NOT NULL DEFAULT 0,
    PriceLevel              SMALLINT         NOT NULL DEFAULT 0,
    TaxExempt               BIT              NOT NULL DEFAULT 0,
    Notes                   NTEXT            NULL,
    Title                   NVARCHAR(40)     NOT NULL DEFAULT '',
    EmailAddress            NVARCHAR(510)    NOT NULL DEFAULT '',
    TaxNumber               NVARCHAR(40)     NOT NULL DEFAULT '',
    PictureName             NVARCHAR(100)    NOT NULL DEFAULT '',
    DefaultShippingServiceID INT             NOT NULL DEFAULT 0,
    PhoneNumber             NVARCHAR(60)     NOT NULL DEFAULT '',
    FaxNumber               NVARCHAR(60)     NOT NULL DEFAULT '',
    CashierID               INT              NOT NULL DEFAULT 0,
    SalesRepID              INT              NOT NULL DEFAULT 0,
    Vouchers                MONEY            NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 7. Cashier (Cajeros/Usuarios del POS)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Cashier', N'U') IS NULL
CREATE TABLE dbo.Cashier (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    HQID                    INT              NOT NULL DEFAULT 0,
    LastUpdated             DATETIME         NOT NULL DEFAULT GETDATE(),
    Number                  NVARCHAR(18)     NOT NULL DEFAULT '',
    StoreID                 INT              NOT NULL DEFAULT 0,
    Name                    NVARCHAR(100)    NOT NULL DEFAULT '',
    Password                NVARCHAR(1024)   NULL,
    FloorLimit              MONEY            NOT NULL DEFAULT 0,
    ReturnLimit             MONEY            NOT NULL DEFAULT 0,
    CashDrawerNumber        SMALLINT         NOT NULL DEFAULT 0,
    SecurityLevel           SMALLINT         NOT NULL DEFAULT 0,
    Privileges              INT              NOT NULL DEFAULT 0,
    EmailAddress            NVARCHAR(510)    NOT NULL DEFAULT '',
    FailedLogonAttempts     INT              NOT NULL DEFAULT 0,
    MaxOverShortAmount      MONEY            NOT NULL DEFAULT 0,
    MaxOverShortPercent     FLOAT            NOT NULL DEFAULT 0,
    OverShortLimitType      INT              NOT NULL DEFAULT 0,
    Telephone               NVARCHAR(60)     NOT NULL DEFAULT '',
    PasswordChangedDate     DATETIME         NULL,
    PasswordResetFlag       BIT              NOT NULL DEFAULT 0,
    Inactive                BIT              NOT NULL DEFAULT 0,
    POSTaskpad              INT              NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 8. SalesRep (Vendedores)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.SalesRep', N'U') IS NULL
CREATE TABLE dbo.SalesRep (
    HQID                INT              NOT NULL DEFAULT 0,
    LastUpdated         DATETIME         NOT NULL DEFAULT GETDATE(),
    Number              NVARCHAR(20)     NOT NULL DEFAULT '',
    StoreID             INT              NOT NULL DEFAULT 0,
    ID                  INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Name                NVARCHAR(60)     NOT NULL DEFAULT '',
    PercentOfSale       REAL             NOT NULL DEFAULT 0,
    PercentOfProfit     REAL             NOT NULL DEFAULT 0,
    FixedRate           MONEY            NOT NULL DEFAULT 0,
    EmailAddress        NVARCHAR(510)    NOT NULL DEFAULT '',
    Telephone           NVARCHAR(60)     NOT NULL DEFAULT '',
    SyncGuid            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 9. Configuration (Configuración general de la tienda)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Configuration', N'U') IS NULL
CREATE TABLE dbo.Configuration (
    ID                          INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    AccountDefaultID            INT              NOT NULL DEFAULT 0,
    AccountMonthlyClosingDay    SMALLINT         NOT NULL DEFAULT 0,
    CostUpdateMethod            INT              NOT NULL DEFAULT 0,
    HQCreationDate              DATETIME         NULL,
    LastBackupMessage           DATETIME         NULL,
    LastUpdated                 DATETIME         NOT NULL DEFAULT GETDATE(),
    LayawayDeposit              REAL             NOT NULL DEFAULT 0,
    LimitItem                   FLOAT            NOT NULL DEFAULT 0,
    LimitPurchase               MONEY            NOT NULL DEFAULT 0,
    LimitTimeFrame              INT              NOT NULL DEFAULT 0,
    LimitType                   INT              NOT NULL DEFAULT 0,
    SerialNumber                VARCHAR(50)      NULL,
    StoreCountry                NVARCHAR(40)     NOT NULL DEFAULT '',
    StoreID                     INT              NOT NULL DEFAULT 0,
    StoreState                  NVARCHAR(40)     NOT NULL DEFAULT '',
    SyncID                      INT              NOT NULL DEFAULT 0,
    TaxSystem                   INT              NOT NULL DEFAULT 0,
    VATRegistrationNumber       NVARCHAR(40)     NOT NULL DEFAULT '',
    VATDetailID                 NVARCHAR(100)    NOT NULL DEFAULT '',
    StoreName                   NVARCHAR(60)     NOT NULL DEFAULT '',
    StoreAddress1               NVARCHAR(60)     NOT NULL DEFAULT '',
    StoreAddress2               NVARCHAR(60)     NOT NULL DEFAULT '',
    StoreCity                   NVARCHAR(60)     NOT NULL DEFAULT '',
    StoreZip                    NVARCHAR(30)     NOT NULL DEFAULT '',
    StorePhone                  NVARCHAR(28)     NOT NULL DEFAULT '',
    StoreFax                    NVARCHAR(28)     NOT NULL DEFAULT '',
    StoreEmail                  NVARCHAR(510)    NOT NULL DEFAULT '',
    QuoteExpirationDays         SMALLINT         NOT NULL DEFAULT 30,
    BackOrderExpirationDays     SMALLINT         NOT NULL DEFAULT 0,
    LayawayExpirationDays       SMALLINT         NOT NULL DEFAULT 0,
    WorkOrderDueDays            SMALLINT         NOT NULL DEFAULT 0,
    LayawayFee                  MONEY            NOT NULL DEFAULT 0,
    ReceiptCount                INT              NOT NULL DEFAULT 0,
    ReceiptCount2               INT              NOT NULL DEFAULT 0,
    EDCTimeout                  INT              NOT NULL DEFAULT 0,
    WorkOrderDeposit            FLOAT            NOT NULL DEFAULT 0,
    PriceCalculationRule        INT              NOT NULL DEFAULT 0,
    Options                     INT              NOT NULL DEFAULT 0,
    TaxBasis                    INT              NOT NULL DEFAULT 0,
    TaxField                    INT              NOT NULL DEFAULT 0,
    ItemTaxID                   INT              NOT NULL DEFAULT 0,
    DefaultTenderID             INT              NOT NULL DEFAULT 0,
    Options2                    INT              NOT NULL DEFAULT 0,
    VoucherExpirationDays       INT              NOT NULL DEFAULT 0,
    EnableGlobalCustomers       BIT              NOT NULL DEFAULT 0,
    DefaultGlobalCustomer       BIT              NOT NULL DEFAULT 0,
    SoftwareValidation1         VARCHAR(128)     NULL,
    SoftwareValidation2         VARCHAR(128)     NULL,
    SoftwareValidation3         VARCHAR(128)     NULL,
    SoftwareValidation4         VARCHAR(128)     NULL,
    SoftwareValidation5         VARCHAR(128)     NULL,
    Options3                    INT              NOT NULL DEFAULT 0,
    Options4                    INT              NOT NULL DEFAULT 0,
    NextAutoAccountNumber       INT              NOT NULL DEFAULT 0,
    AccountingInterface         INT              NOT NULL DEFAULT 0,
    BillPostingAccount          NVARCHAR(200)    NOT NULL DEFAULT '',
    AccountingFilename          NVARCHAR(300)    NOT NULL DEFAULT '',
    AccountingCompany           NVARCHAR(200)    NOT NULL DEFAULT '',
    PasswordExpireCheck         BIT              NOT NULL DEFAULT 0,
    PasswordWillExpire          INT              NOT NULL DEFAULT 0,
    ReminderPeriod              INT              NOT NULL DEFAULT 0,
    KeepPasswordHistory         INT              NOT NULL DEFAULT 0,
    AccountLockedCheck          BIT              NOT NULL DEFAULT 0,
    LockOutAttempts             INT              NOT NULL DEFAULT 0,
    LockOutPeriod               INT              NOT NULL DEFAULT 0,
    ComplexityRequired          BIT              NOT NULL DEFAULT 0,
    PasswordLength              INT              NOT NULL DEFAULT 0,
    NoOfUpperCase               INT              NOT NULL DEFAULT 0,
    NoOfNumeric                 INT              NOT NULL DEFAULT 0,
    NoOfSpecialChar             INT              NOT NULL DEFAULT 0,
    DefaultDistributionMethod   INT              NOT NULL DEFAULT 0,
    UseLandedCost               BIT              NOT NULL DEFAULT 0,
    BackorderDeposit            FLOAT            NOT NULL DEFAULT 0,
    NumberFormat                NVARCHAR(50)     NOT NULL DEFAULT '',
    NextNumber                  NVARCHAR(60)     NULL,
    useAutonumberConfig         BIT              NOT NULL DEFAULT 0,
    SyncGuid                    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 10. Tender (Formas de Pago)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL
CREATE TABLE dbo.Tender (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    HQID                    INT              NOT NULL DEFAULT 0,
    PreventOverTendering    BIT              NOT NULL DEFAULT 0,
    VerificationType        INT              NOT NULL DEFAULT 0,
    VerifyViaEDC            BIT              NOT NULL DEFAULT 0,
    Description             NVARCHAR(50)     NOT NULL DEFAULT '',
    AdditionalDetailType    SMALLINT         NOT NULL DEFAULT 0,
    PrinterValidation       BIT              NOT NULL DEFAULT 0,
    ValidationLine1         NVARCHAR(40)     NOT NULL DEFAULT '',
    ValidationLine2         NVARCHAR(40)     NOT NULL DEFAULT '',
    ValidationLine3         NVARCHAR(40)     NOT NULL DEFAULT '',
    GLAccount               NVARCHAR(40)     NOT NULL DEFAULT '',
    ScanCode                SMALLINT         NOT NULL DEFAULT 0,
    RoundToValue            MONEY            NOT NULL DEFAULT 0,
    Code                    NVARCHAR(34)     NOT NULL DEFAULT '',
    MaximumAmount           MONEY            NOT NULL DEFAULT 0,
    DoNotPopCashDrawer      BIT              NOT NULL DEFAULT 0,
    CurrencyID              INT              NOT NULL DEFAULT 0,
    DisplayOrder            INT              NOT NULL DEFAULT 0,
    ValidationMask          NVARCHAR(50)     NOT NULL DEFAULT '',
    SignatureRequired        BIT              NOT NULL DEFAULT 0,
    AllowMultipleEntries    BIT              NOT NULL DEFAULT 0,
    DebitSurcharge          MONEY            NOT NULL DEFAULT 0,
    SupportCashBack         BIT              NOT NULL DEFAULT 0,
    CashBackLimit           MONEY            NOT NULL DEFAULT 0,
    CashBackFee             MONEY            NOT NULL DEFAULT 0,
    Inactive                BIT              NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 11. Batch (Lotes/Turnos)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Batch', N'U') IS NULL
CREATE TABLE dbo.Batch (
    CustomerDepositMade     MONEY            NOT NULL DEFAULT 0,
    CustomerDepositRedeemed MONEY            NOT NULL DEFAULT 0,
    LastUpdated             DATETIME         NOT NULL DEFAULT GETDATE(),
    StoreID                 INT              NOT NULL DEFAULT 0,
    BatchNumber             INT              NOT NULL PRIMARY KEY,
    Status                  SMALLINT         NOT NULL DEFAULT 0,
    RegisterID              INT              NOT NULL DEFAULT 0,
    OpeningTime             DATETIME         NULL,
    ClosingTime             DATETIME         NULL,
    OpeningTotal            MONEY            NOT NULL DEFAULT 0,
    ClosingTotal            MONEY            NOT NULL DEFAULT 0,
    Sales                   MONEY            NOT NULL DEFAULT 0,
    Returns                 MONEY            NOT NULL DEFAULT 0,
    Tax                     MONEY            NOT NULL DEFAULT 0,
    SalesPlusTax            MONEY            NOT NULL DEFAULT 0,
    Commission              MONEY            NOT NULL DEFAULT 0,
    PaidOut                 MONEY            NOT NULL DEFAULT 0,
    Dropped                 MONEY            NOT NULL DEFAULT 0,
    PaidOnAccount           MONEY            NOT NULL DEFAULT 0,
    PaidToAccount           MONEY            NOT NULL DEFAULT 0,
    CustomerCount           INT              NOT NULL DEFAULT 0,
    NoSalesCount            INT              NOT NULL DEFAULT 0,
    AbortedTransCount       INT              NOT NULL DEFAULT 0,
    TotalTendered           MONEY            NOT NULL DEFAULT 0,
    TotalChange             MONEY            NOT NULL DEFAULT 0,
    Discounts               MONEY            NOT NULL DEFAULT 0,
    CostOfGoods             MONEY            NOT NULL DEFAULT 0,
    LayawayPaid             MONEY            NOT NULL DEFAULT 0,
    LayawayClosed           MONEY            NOT NULL DEFAULT 0,
    Shipping                MONEY            NOT NULL DEFAULT 0,
    TenderRoundingError     MONEY            NOT NULL DEFAULT 0,
    DebitSurcharge          MONEY            NOT NULL DEFAULT 0,
    CashBackSurcharge       MONEY            NOT NULL DEFAULT 0,
    Vouchers                MONEY            NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 12. Transaction (Transacciones/Ventas)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.[Transaction]', N'U') IS NULL
CREATE TABLE dbo.[Transaction] (
    TransactionNumber   INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    StoreID             INT              NOT NULL DEFAULT 0,
    BatchNumber         INT              NOT NULL DEFAULT 0,
    Time                DATETIME         NOT NULL DEFAULT GETDATE(),
    CustomerID          INT              NOT NULL DEFAULT 0,
    CashierID           INT              NOT NULL DEFAULT 0,
    Total               MONEY            NOT NULL DEFAULT 0,
    SalesTax            MONEY            NOT NULL DEFAULT 0,
    Comment             NVARCHAR(510)    NOT NULL DEFAULT '',
    ReferenceNumber     NVARCHAR(100)    NOT NULL DEFAULT '',
    Status              INT              NOT NULL DEFAULT 0,
    ChannelType         INT              NOT NULL DEFAULT 0,
    RecallID            INT              NOT NULL DEFAULT 0,
    RecallType          INT              NOT NULL DEFAULT 0,
    ExchangeID          INT              NOT NULL DEFAULT 0,
    ShipToID            INT              NOT NULL DEFAULT 0,
    SyncGuid            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 13. TransactionEntry (Líneas de Transacción)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.TransactionEntry', N'U') IS NULL
CREATE TABLE dbo.TransactionEntry (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    StoreID                 INT              NOT NULL DEFAULT 0,
    TransactionNumber       INT              NOT NULL DEFAULT 0,
    ItemID                  INT              NOT NULL DEFAULT 0,
    Price                   MONEY            NOT NULL DEFAULT 0,
    FullPrice               MONEY            NOT NULL DEFAULT 0,
    PriceSource             SMALLINT         NOT NULL DEFAULT 0,
    Quantity                FLOAT            NOT NULL DEFAULT 0,
    SalesRepID              INT              NOT NULL DEFAULT 0,
    Taxable                 BIT              NOT NULL DEFAULT 0,
    DetailID                INT              NOT NULL DEFAULT 0,
    Comment                 NVARCHAR(510)    NOT NULL DEFAULT '',
    DiscountReasonCodeID    INT              NOT NULL DEFAULT 0,
    ReturnReasonCodeID      INT              NOT NULL DEFAULT 0,
    TaxChangeReasonCodeID   INT              NOT NULL DEFAULT 0,
    SalesTax                MONEY            NOT NULL DEFAULT 0,
    ItemType                INT              NOT NULL DEFAULT 0,
    ComputedQuantity        FLOAT            NULL,
    TransactionTime         DATETIME         NULL,
    Cost                    MONEY            NOT NULL DEFAULT 0,
    Commission              MONEY            NOT NULL DEFAULT 0,
    QuantityDiscountID      INT              NOT NULL DEFAULT 0,
    IsAddMoney              BIT              NOT NULL DEFAULT 0,
    VoucherID               INT              NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 14. TenderEntry (Líneas de Pago en Transacciones)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.TenderEntry', N'U') IS NULL
CREATE TABLE dbo.TenderEntry (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    BatchNumber             INT              NOT NULL DEFAULT 0,
    CreditCardExpiration    NVARCHAR(20)     NOT NULL DEFAULT '',
    OrderHistoryID          INT              NOT NULL DEFAULT 0,
    DropPayoutID            INT              NOT NULL DEFAULT 0,
    StoreID                 INT              NOT NULL DEFAULT 0,
    TransactionNumber       INT              NOT NULL DEFAULT 0,
    TenderID                INT              NOT NULL DEFAULT 0,
    PaymentID               INT              NOT NULL DEFAULT 0,
    Description             NVARCHAR(60)     NOT NULL DEFAULT '',
    CreditCardNumber        NVARCHAR(50)     NOT NULL DEFAULT '',
    CreditCardApprovalCode  NVARCHAR(40)     NOT NULL DEFAULT '',
    Amount                  MONEY            NOT NULL DEFAULT 0,
    AccountHolder           NVARCHAR(80)     NOT NULL DEFAULT '',
    RoundingError           MONEY            NOT NULL DEFAULT 0,
    AmountForeign           MONEY            NOT NULL DEFAULT 0,
    BankNumber              NVARCHAR(50)     NOT NULL DEFAULT '',
    SerialNumber            NVARCHAR(50)     NOT NULL DEFAULT '',
    State                   NVARCHAR(20)     NOT NULL DEFAULT '',
    License                 NVARCHAR(50)     NOT NULL DEFAULT '',
    BirthDate               DATETIME         NULL,
    TransitNumber           NVARCHAR(50)     NOT NULL DEFAULT '',
    VisaNetAuthorizationID  INT              NOT NULL DEFAULT 0,
    DebitSurcharge          MONEY            NOT NULL DEFAULT 0,
    CashBackSurcharge       MONEY            NOT NULL DEFAULT 0,
    IsCreateNew             BIT              NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 15. TaxEntry (Detalle de Impuestos por línea)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.TaxEntry', N'U') IS NULL
CREATE TABLE dbo.TaxEntry (
    ID                      INT                 NOT NULL IDENTITY(1,1) PRIMARY KEY,
    StoreID                 INT                 NOT NULL DEFAULT 0,
    TaxID                   INT                 NOT NULL DEFAULT 0,
    TransactionNumber       INT                 NOT NULL DEFAULT 0,
    Tax                     MONEY               NOT NULL DEFAULT 0,
    TaxableAmount           MONEY               NOT NULL DEFAULT 0,
    TransactionEntryID      INT                 NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 16. TransactionHold (Tiquetes en espera)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.TransactionHold', N'U') IS NULL
CREATE TABLE dbo.TransactionHold (
    ID                              INT              NOT NULL PRIMARY KEY,
    StoreID                         INT              NOT NULL DEFAULT 0,
    TransactionType                 SMALLINT         NOT NULL DEFAULT 0,
    HoldComment                     NVARCHAR(510)    NOT NULL DEFAULT '',
    RecallID                        INT              NOT NULL DEFAULT 0,
    Comment                         NVARCHAR(100)    NOT NULL DEFAULT '',
    PriceLevel                      SMALLINT         NOT NULL DEFAULT 0,
    DiscountMethod                  SMALLINT         NOT NULL DEFAULT 0,
    DiscountPercent                 FLOAT            NOT NULL DEFAULT 0,
    Taxable                         BIT              NOT NULL DEFAULT 0,
    CustomerID                      INT              NOT NULL DEFAULT 0,
    DeltaDeposit                    MONEY            NOT NULL DEFAULT 0,
    DepositOverride                 BIT              NOT NULL DEFAULT 0,
    DepositPrevious                 MONEY            NOT NULL DEFAULT 0,
    PaymentsPrevious                MONEY            NOT NULL DEFAULT 0,
    TaxPrevious                     MONEY            NOT NULL DEFAULT 0,
    SalesRepID                      INT              NOT NULL DEFAULT 0,
    ShipToID                        INT              NOT NULL DEFAULT 0,
    TransactionTime                 DATETIME         NOT NULL DEFAULT GETDATE(),
    ExpirationOrDueDate             DATETIME         NOT NULL DEFAULT GETDATE(),
    ReturnMode                      BIT              NOT NULL DEFAULT 0,
    ReferenceNumber                 NVARCHAR(100)    NOT NULL DEFAULT '',
    ShippingChargePurchased         MONEY            NOT NULL DEFAULT 0,
    ShippingChargeOverride          BIT              NOT NULL DEFAULT 0,
    ShippingServiceID               INT              NOT NULL DEFAULT 0,
    ShippingTrackingNumber          NVARCHAR(510)    NOT NULL DEFAULT '',
    ShippingNotes                   NTEXT            NULL,
    ReasonCodeID                    INT              NOT NULL DEFAULT 0,
    ExchangeID                      INT              NOT NULL DEFAULT 0,
    ChannelType                     INT              NOT NULL DEFAULT 0,
    DefaultDiscountReasonCodeID     INT              NOT NULL DEFAULT 0,
    DefaultReturnReasonCodeID       INT              NOT NULL DEFAULT 0,
    DefaultTaxChangeReasonCodeID    INT              NOT NULL DEFAULT 0,
    BatchNumber                     INT              NOT NULL DEFAULT 0,
    SyncGuid                        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 17. TransactionHoldEntry (Líneas de Tiquetes en espera)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.TransactionHoldEntry', N'U') IS NULL
CREATE TABLE dbo.TransactionHoldEntry (
    ID                          INT              NOT NULL PRIMARY KEY,
    EntryKey                    NVARCHAR(20)     NOT NULL DEFAULT '',
    StoreID                     INT              NOT NULL DEFAULT 0,
    TransactionHoldID           INT              NOT NULL DEFAULT 0,
    RecallID                    INT              NOT NULL DEFAULT 0,
    Description                 NVARCHAR(160)    NOT NULL DEFAULT '',
    QuantityPurchased           FLOAT            NOT NULL DEFAULT 0,
    QuantityOnOrder             FLOAT            NOT NULL DEFAULT 0,
    QuantityRTD                 FLOAT            NOT NULL DEFAULT 0,
    QuantityReserved            FLOAT            NOT NULL DEFAULT 0,
    Price                       MONEY            NOT NULL DEFAULT 0,
    FullPrice                   MONEY            NOT NULL DEFAULT 0,
    Cost                        MONEY            NOT NULL DEFAULT 0,
    PriceSource                 SMALLINT         NOT NULL DEFAULT 0,
    Comment                     NVARCHAR(510)    NOT NULL DEFAULT '',
    DetailID                    INT              NOT NULL DEFAULT 0,
    Taxable                     BIT              NOT NULL DEFAULT 0,
    ItemID                      INT              NOT NULL DEFAULT 0,
    SalesRepID                  INT              NOT NULL DEFAULT 0,
    SerialNumber1               NVARCHAR(100)    NOT NULL DEFAULT '',
    SerialNumber2               NVARCHAR(100)    NOT NULL DEFAULT '',
    SerialNumber3               NVARCHAR(100)    NOT NULL DEFAULT '',
    VoucherNumber               NVARCHAR(100)    NOT NULL DEFAULT '',
    VoucherExpirationDate       DATETIME         NULL,
    DiscountReasonCodeID        INT              NOT NULL DEFAULT 0,
    ReturnReasonCodeID          INT              NOT NULL DEFAULT 0,
    TaxChangeReasonCodeID       INT              NOT NULL DEFAULT 0,
    ItemTaxID                   INT              NOT NULL DEFAULT 0,
    ComponentQuantityReserved   FLOAT            NOT NULL DEFAULT 0,
    TransactionTime             DATETIME         NULL,
    IsAddMoney                  BIT              NOT NULL DEFAULT 0,
    VoucherID                   INT              NOT NULL DEFAULT 0,
    SyncGuid                    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

IF COL_LENGTH('dbo.TransactionHoldEntry', 'Cost') IS NULL
    ALTER TABLE dbo.TransactionHoldEntry
    ADD Cost MONEY NOT NULL CONSTRAINT DF_TransactionHoldEntry_Cost DEFAULT 0;
GO

-- ─────────────────────────────────────────────────────────────────────
-- 18. Order (Cotizaciones / Órdenes de Trabajo)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
CREATE TABLE dbo.[Order] (
    ID                              INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    StoreID                         INT              NOT NULL DEFAULT 0,
    Time                            DATETIME         NOT NULL DEFAULT GETDATE(),
    Type                            INT              NOT NULL DEFAULT 0,
    Comment                         NVARCHAR(510)    NOT NULL DEFAULT '',
    CustomerID                      INT              NOT NULL DEFAULT 0,
    ShipToID                        INT              NOT NULL DEFAULT 0,
    DepositOverride                 BIT              NOT NULL DEFAULT 0,
    Deposit                         MONEY            NOT NULL DEFAULT 0,
    Tax                             MONEY            NOT NULL DEFAULT 0,
    Total                           MONEY            NOT NULL DEFAULT 0,
    LastUpdated                     DATETIME         NOT NULL DEFAULT GETDATE(),
    ExpirationOrDueDate             DATETIME         NOT NULL DEFAULT GETDATE(),
    Taxable                         BIT              NOT NULL DEFAULT 1,
    SalesRepID                      INT              NOT NULL DEFAULT 0,
    ReferenceNumber                 NVARCHAR(100)    NOT NULL DEFAULT '',
    ShippingChargeOnOrder           MONEY            NOT NULL DEFAULT 0,
    ShippingChargeOverride          BIT              NOT NULL DEFAULT 0,
    ShippingServiceID               INT              NOT NULL DEFAULT 0,
    ShippingTrackingNumber          NVARCHAR(510)    NOT NULL DEFAULT '',
    ShippingNotes                   NTEXT            NULL,
    ReasonCodeID                    INT              NOT NULL DEFAULT 0,
    ExchangeID                      INT              NOT NULL DEFAULT 0,
    ChannelType                     INT              NOT NULL DEFAULT 0,
    DefaultDiscountReasonCodeID     INT              NOT NULL DEFAULT 0,
    DefaultReturnReasonCodeID       INT              NOT NULL DEFAULT 0,
    DefaultTaxChangeReasonCodeID    INT              NOT NULL DEFAULT 0,
    Closed                          BIT              NOT NULL DEFAULT 0,
    SyncGuid                        UNIQUEIDENTIFIER NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 19. OrderEntry (Líneas de Cotización/Orden)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.OrderEntry', N'U') IS NULL
CREATE TABLE dbo.OrderEntry (
    ID                      INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    StoreID                 INT              NOT NULL DEFAULT 0,
    OrderID                 INT              NOT NULL DEFAULT 0,
    ItemID                  INT              NOT NULL DEFAULT 0,
    FullPrice               MONEY            NOT NULL DEFAULT 0,
    PriceSource             SMALLINT         NOT NULL DEFAULT 0,
    Price                   MONEY            NOT NULL DEFAULT 0,
    QuantityOnOrder         FLOAT            NOT NULL DEFAULT 0,
    SalesRepID              INT              NOT NULL DEFAULT 0,
    Taxable                 INT              NOT NULL DEFAULT 1,
    DetailID                INT              NOT NULL DEFAULT 0,
    Description             NVARCHAR(60)     NOT NULL DEFAULT '',
    QuantityRTD             FLOAT            NOT NULL DEFAULT 0,
    LastUpdated             DATETIME         NOT NULL DEFAULT GETDATE(),
    Comment                 NVARCHAR(510)    NOT NULL DEFAULT '',
    DiscountReasonCodeID    INT              NOT NULL DEFAULT 0,
    ReturnReasonCodeID      INT              NOT NULL DEFAULT 0,
    TaxChangeReasonCodeID   INT              NOT NULL DEFAULT 0,
    TransactionTime         DATETIME         NULL,
    IsAddMoney              BIT              NOT NULL DEFAULT 0,
    VoucherID               INT              NOT NULL DEFAULT 0,
    Cost                    MONEY            NOT NULL DEFAULT 0,
    SyncGuid                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 20. Payment (Pagos/Abonos)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.Payment', N'U') IS NULL
CREATE TABLE dbo.Payment (
    ID          INT              NOT NULL PRIMARY KEY,
    BatchNumber INT              NOT NULL DEFAULT 0,
    CashierID   INT              NOT NULL DEFAULT 0,
    StoreID     INT              NOT NULL DEFAULT 0,
    CustomerID  INT              NOT NULL DEFAULT 0,
    Time        DATETIME         NOT NULL DEFAULT GETDATE(),
    Amount      MONEY            NOT NULL DEFAULT 0,
    Comment     NVARCHAR(100)    NOT NULL DEFAULT '',
    SyncGuid    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 21. ReasonCode (Códigos de Razón)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ReasonCode', N'U') IS NULL
CREATE TABLE dbo.ReasonCode (
    ID          INT              NOT NULL PRIMARY KEY,
    HQID        INT              NOT NULL DEFAULT 0,
    Code        NVARCHAR(50)     NOT NULL DEFAULT '',
    Description NVARCHAR(100)    NOT NULL DEFAULT '',
    Type        INT              NOT NULL DEFAULT 0,
    StartDate   DATETIME         NULL,
    EndDate     DATETIME         NULL,
    SyncGuid    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 22. ExonerationDocumentType (Tipos de documento de exoneración)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ExonerationDocumentType', N'U') IS NULL
CREATE TABLE dbo.ExonerationDocumentType (
    Code        VARCHAR(2)       NOT NULL PRIMARY KEY,
    Description VARCHAR(255)     NOT NULL DEFAULT ''
);
GO

-- =====================================================================
-- TABLAS DE CUENTAS POR COBRAR (AR)
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- 23. AR_Account (Cuentas de Clientes)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AR_Account', N'U') IS NULL
CREATE TABLE dbo.AR_Account (
    ID                  INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
    LastUpdated         DATETIME         NOT NULL DEFAULT GETDATE(),
    Number              NVARCHAR(40)     NOT NULL DEFAULT '',
    Name                NVARCHAR(100)    NOT NULL DEFAULT '',
    ExtCode             NVARCHAR(40)     NOT NULL DEFAULT '',
    Title               NVARCHAR(40)     NOT NULL DEFAULT '',
    FirstName           NVARCHAR(60)     NOT NULL DEFAULT '',
    MiddleName          NVARCHAR(60)     NOT NULL DEFAULT '',
    LastName            NVARCHAR(100)    NOT NULL DEFAULT '',
    Suffix              NVARCHAR(40)     NOT NULL DEFAULT '',
    Company             NVARCHAR(100)    NOT NULL DEFAULT '',
    JobTitle            NVARCHAR(100)    NOT NULL DEFAULT '',
    Type                TINYINT          NOT NULL DEFAULT 0,
    Role                INT              NOT NULL DEFAULT 0,
    Status              TINYINT          NOT NULL DEFAULT 0,
    BillingAccount      INT              NOT NULL DEFAULT 0,
    GroupID             INT              NOT NULL DEFAULT 0,
    PayTermsID          INT              NOT NULL DEFAULT 0,
    FinChargeID         INT              NOT NULL DEFAULT 0,
    CurrencyID          INT              NOT NULL DEFAULT 0,
    ManagerID           INT              NOT NULL DEFAULT 0,
    CountryID           INT              NOT NULL DEFAULT 0,
    RegionID            INT              NOT NULL DEFAULT 0,
    LocationID          INT              NOT NULL DEFAULT 0,
    Address1            NVARCHAR(100)    NOT NULL DEFAULT '',
    Address2            NVARCHAR(100)    NOT NULL DEFAULT '',
    City                NVARCHAR(100)    NOT NULL DEFAULT '',
    State               NVARCHAR(40)     NOT NULL DEFAULT '',
    Zip                 NVARCHAR(40)     NOT NULL DEFAULT '',
    Country             NVARCHAR(60)     NOT NULL DEFAULT '',
    PhoneNumber         NVARCHAR(60)     NOT NULL DEFAULT '',
    MobileNumber        NVARCHAR(60)     NOT NULL DEFAULT '',
    FaxNumber           NVARCHAR(60)     NOT NULL DEFAULT '',
    EMail               NVARCHAR(510)    NOT NULL DEFAULT '',
    HomePage            NVARCHAR(510)    NOT NULL DEFAULT '',
    IMAddress           NVARCHAR(100)    NOT NULL DEFAULT '',
    CreditLimit         MONEY            NOT NULL DEFAULT 0,
    CreditLimitCheck    TINYINT          NOT NULL DEFAULT 0,
    StatementType       INT              NOT NULL DEFAULT 0,
    StatementDelivery   TINYINT          NOT NULL DEFAULT 0,
    StatementAddress    NVARCHAR(510)    NOT NULL DEFAULT '',
    ApplicationMethod   TINYINT          NOT NULL DEFAULT 0,
    ClosingDate         DATETIME         NULL,
    ClosingBalance      MONEY            NOT NULL DEFAULT 0,
    LastStatement       INT              NOT NULL DEFAULT 0,
    CustomCode1         INT              NOT NULL DEFAULT 0,
    CustomCode2         INT              NOT NULL DEFAULT 0,
    CustomCode3         INT              NOT NULL DEFAULT 0,
    CustomCode4         INT              NOT NULL DEFAULT 0,
    CustomCode5         INT              NOT NULL DEFAULT 0,
    CustomText1         NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText2         NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText3         NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText4         NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomText5         NVARCHAR(60)     NOT NULL DEFAULT '',
    CustomNumber1       FLOAT            NOT NULL DEFAULT 0,
    CustomNumber2       FLOAT            NOT NULL DEFAULT 0,
    CustomNumber3       FLOAT            NOT NULL DEFAULT 0,
    CustomNumber4       FLOAT            NOT NULL DEFAULT 0,
    CustomNumber5       FLOAT            NOT NULL DEFAULT 0,
    CustomDate1         DATETIME         NULL,
    CustomDate2         DATETIME         NULL,
    CustomDate3         DATETIME         NULL,
    CustomDate4         DATETIME         NULL,
    CustomDate5         DATETIME         NULL,
    Picture             VARBINARY(MAX)   NULL,
    Notes               NVARCHAR(MAX)    NULL,
    ExtData             NVARCHAR(MAX)    NULL,
    DateOpened          DATETIME         NOT NULL DEFAULT GETDATE(),
    LastActivity        DATETIME         NULL,
    StoreID             INT              NOT NULL DEFAULT 0,
    SyncGuid            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 24. AR_AccountBalance (Saldos de Cuentas)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AR_AccountBalance', N'U') IS NULL AND OBJECT_ID(N'dbo.AR_AccountBalance', N'V') IS NULL
CREATE TABLE dbo.AR_AccountBalance (
    ID              INT             NOT NULL PRIMARY KEY,  -- FK → AR_Account.ID
    Amount          MONEY           NOT NULL DEFAULT 0
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 25. AR_LedgerEntry (Movimientos Contables)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AR_LedgerEntry', N'U') IS NULL
CREATE TABLE dbo.AR_LedgerEntry (
    ID                      INT                 NOT NULL IDENTITY(1,1) PRIMARY KEY,
    LastUpdated             DATETIME            NOT NULL DEFAULT GETDATE(),
    AccountID               INT                 NOT NULL DEFAULT 0,
    CustomerID              INT                 NOT NULL DEFAULT 0,
    StoreID                 INT                 NOT NULL DEFAULT 0,
    LinkType                TINYINT             NOT NULL DEFAULT 0,
    LinkID                  INT                 NOT NULL DEFAULT 0,
    AuditEntryID            INT                 NOT NULL DEFAULT 0,
    DocumentType            TINYINT             NOT NULL DEFAULT 0,
    DocumentID              INT                 NOT NULL DEFAULT 0,
    PostingDate             DATETIME            NOT NULL DEFAULT GETDATE(),
    DueDate                 DATETIME            NOT NULL DEFAULT GETDATE(),
    LedgerType              TINYINT             NOT NULL DEFAULT 0,
    Reference               NVARCHAR(20)        NOT NULL DEFAULT '',
    Description             NVARCHAR(50)        NOT NULL DEFAULT '',
    CurrencyID              INT                 NOT NULL DEFAULT 0,
    CurrencyFactor          FLOAT               NOT NULL DEFAULT 1,
    Positive                BIT                 NOT NULL DEFAULT 0,
    ClosingDate             DATETIME            NULL,
    ReasonID                INT                 NOT NULL DEFAULT 0,
    HoldReasonID            INT                 NOT NULL DEFAULT 0,
    UndoReasonID            INT                 NOT NULL DEFAULT 0,
    PayMethodID             INT                 NOT NULL DEFAULT 0,
    TransactionID           INT                 NOT NULL DEFAULT 0,
    ExtReference            NVARCHAR(50)        NULL,
    Comment                 NVARCHAR(255)       NULL,
    [Open]                  INT                 NOT NULL DEFAULT 1,
    SyncGuid                UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 26. AR_LedgerEntryDetail (Detalle de Movimientos Contables)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AR_LedgerEntryDetail', N'U') IS NULL
CREATE TABLE dbo.AR_LedgerEntryDetail (
    ID                  INT                 NOT NULL IDENTITY(1,1) PRIMARY KEY,
    LedgerEntryID       INT                 NOT NULL DEFAULT 0,
    AccountID           INT                 NOT NULL DEFAULT 0,
    LedgerType          TINYINT             NOT NULL DEFAULT 0,
    DueDate             DATETIME            NOT NULL DEFAULT GETDATE(),
    PostingDate         DATETIME            NOT NULL DEFAULT GETDATE(),
    DetailType          TINYINT             NOT NULL DEFAULT 0,
    Reference           NVARCHAR(20)        NOT NULL DEFAULT '',
    Amount              MONEY               NOT NULL DEFAULT 0,
    AmountLCY           MONEY               NOT NULL DEFAULT 0,
    AmountACY           MONEY               NOT NULL DEFAULT 0,
    AuditEntryID        INT                 NOT NULL DEFAULT 0,
    AppliedEntryID      INT                 NOT NULL DEFAULT 0,
    AppliedAmount       MONEY               NOT NULL DEFAULT 0,
    UnapplyEntryID      INT                 NOT NULL DEFAULT 0,
    UnapplyReasonID     INT                 NOT NULL DEFAULT 0,
    SyncGuid            UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID()
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 27. AR_Transaction (Transacciones de CxC)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AR_Transaction', N'U') IS NULL
CREATE TABLE dbo.AR_Transaction (
    ID              INT                 NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UserID          INT                 NOT NULL DEFAULT 0,
    PostingDate     DATETIME            NOT NULL DEFAULT GETDATE(),
    CustomerID      INT                 NOT NULL DEFAULT 0,
    OrderID         INT                 NOT NULL DEFAULT 0,
    DocumentType    TINYINT             NOT NULL DEFAULT 0,
    DocumentID      INT                 NOT NULL DEFAULT 0,
    Amount          MONEY               NOT NULL DEFAULT 0,
    Balance         MONEY               NOT NULL DEFAULT 0,
    BatchNumber     INT                 NOT NULL DEFAULT 0,
    CashierID       INT                 NOT NULL DEFAULT 0,
    TenderID        INT                 NOT NULL DEFAULT 0,
    ReceivableID    INT                 NOT NULL DEFAULT 0,
    Reference       NVARCHAR(100)       NULL,
    Status          TINYINT             NOT NULL DEFAULT 0,
    PostedDate      DATETIME            NULL,
    SyncGuid        UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID()
);
GO

-- =====================================================================
-- TABLAS DE ROLES / SEGURIDAD RMH
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- 28. RMH_ApplicationRole
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.RMH_ApplicationRole', N'U') IS NULL
CREATE TABLE dbo.RMH_ApplicationRole (
    ID              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(20)    NOT NULL DEFAULT '',
    Name            NVARCHAR(50)    NOT NULL DEFAULT '',
    Privileges      INT             NOT NULL DEFAULT 0
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 29. RMH_LoginRole
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.RMH_LoginRole', N'U') IS NULL
CREATE TABLE dbo.RMH_LoginRole (
    ID              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CashierID       INT             NOT NULL DEFAULT 0,
    PosRoleID       INT             NOT NULL DEFAULT 0,
    ManagerRoleID   INT             NOT NULL DEFAULT 0
);
GO

-- =====================================================================
-- TABLAS AVS (Personalizadas del proyecto NovaRetail)
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- 30. AVS_Parametros (Parámetros de configuración)
--     *** ESTA TABLA SE CREA CON DATOS ***
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_Parametros', N'U') IS NULL
CREATE TABLE dbo.AVS_Parametros (
    CODIGO          NVARCHAR(20)    NOT NULL PRIMARY KEY,
    DESCRIPCION     NVARCHAR(255)   NULL,
    VALOR           NVARCHAR(255)   NULL
);
GO

-- Inserción de datos de parámetros (idempotente: no duplica si ya existen)
INSERT INTO dbo.AVS_Parametros (CODIGO, DESCRIPCION, VALOR)
SELECT CODIGO, DESCRIPCION, VALOR FROM (VALUES
    (N'CAT-01', N'IDs de departamentos visibles en categorias de la app', N''),
    (N'CL-01',  N'ID del Cliente de Contado por defecto',                  N'00001'),
    (N'CL-02',  N'Nombre del Cliente de Contado por defecto',              N'CLIENTE CONTADO'),
    (N'IT-01',  N'IDs de ItemType que NO son inventario (separados por coma)', N'7,5,9'),
    (N'PR-01',  N'PriceOverride PriceSource (1=PriceA, 2=PriceB, 3=PriceC)', N'1'),
    (N'TC-01',  N'Tipo de Cambio (moneda extranjera)',                     N'585.50'),
    (N'TX-01',  N'IVA Incluido o Excluido (0=Incluido, 1=Excluido)',       N'0'),
    (N'VE-01',  N'Pedir Vendedor en la venta (0=No, 1=Si)',                N'0'),
    (N'VE-02',  N'Requerir Vendedor obligatorio (0=No, 1=Si)',             N'0')
) AS src(CODIGO, DESCRIPCION, VALOR)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.AVS_Parametros p WHERE p.CODIGO = src.CODIGO
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 31. ExtTender_Settings (Configuración extendida de formas de pago)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.ExtTender_Settings', N'U') IS NULL
CREATE TABLE dbo.ExtTender_Settings (
    ID                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    SalesTenderCods     NVARCHAR(255)   NULL,
    PaymentsTenderCods  NVARCHAR(255)   NULL,
    NCTenderCods        NVARCHAR(255)   NULL,
    NCPaymentCods       NVARCHAR(255)   NULL,
    NCPaymentChargeCode NVARCHAR(50)    NULL
);
GO

INSERT INTO dbo.ExtTender_Settings (SalesTenderCods, PaymentsTenderCods, NCTenderCods, NCPaymentCods, NCPaymentChargeCode)
SELECT N'', N'', N'', N'', N''
WHERE NOT EXISTS (SELECT 1 FROM dbo.ExtTender_Settings);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 32. AVS_UserPreferences (Preferencias por usuario)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_UserPreferences', N'U') IS NULL
CREATE TABLE dbo.AVS_UserPreferences (
    UserName    NVARCHAR(100)   NOT NULL,
    PrefKey     NVARCHAR(50)    NOT NULL,
    PrefValue   NVARCHAR(500)   NOT NULL DEFAULT '',
    CONSTRAINT PK_AVS_UserPreferences PRIMARY KEY (UserName, PrefKey)
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 33. AVS_DATOS_EMISOR (Datos del Emisor Electrónico)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_DATOS_EMISOR', N'U') IS NULL
CREATE TABLE dbo.AVS_DATOS_EMISOR (
    NIF             NVARCHAR(20)    NOT NULL PRIMARY KEY,
    CedulaEmisor    NVARCHAR(20)    NULL
);
GO

-- =====================================================================
-- TABLAS INTEGRAFAST (Facturación Electrónica Costa Rica)
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- 34. AVS_INTEGRAFAST_01 (Cabecera de documento electrónico)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01', N'U') IS NULL
CREATE TABLE dbo.AVS_INTEGRAFAST_01 (
    CLAVE50                 NVARCHAR(50)    NOT NULL PRIMARY KEY,
    CLAVE20                 NVARCHAR(25)    NULL,
    TRANSACTIONNUMBER       NVARCHAR(50)    NOT NULL DEFAULT '',
    COD_SUCURSAL            NVARCHAR(10)    NULL,
    TERMINAL_POS            NVARCHAR(10)    NULL,
    COMPROBANTE_INTERNO     NVARCHAR(25)    NULL,
    COMPROBANTE_SITUACION   NVARCHAR(5)     NULL,
    COMPROBANTE_TIPO        NVARCHAR(5)     NULL,
    COD_MONEDA              NVARCHAR(10)    NULL,
    CONDICION_VENTA         NVARCHAR(5)     NULL,
    COD_CLIENTE             NVARCHAR(30)    NULL,
    NOMBRE_CLIENTE          NVARCHAR(100)   NULL,
    MEDIO_PAGO1             NVARCHAR(10)    NULL,
    MEDIO_PAGO2             NVARCHAR(10)    NULL,
    MEDIO_PAGO3             NVARCHAR(10)    NULL,
    MEDIO_PAGO4             NVARCHAR(10)    NULL,
    TIPOCAMBIO              NVARCHAR(20)    NULL,
    CEDULA_TRIBUTARIA       NVARCHAR(30)    NULL,
    EXONERA                 INT             NOT NULL DEFAULT 0,
    NC_TIPO_DOC             NVARCHAR(5)     NULL,
    NC_REFERENCIA           NVARCHAR(50)    NULL,
    NC_REFERENCIA_FECHA     DATETIME        NULL,
    NC_CODIGO               NVARCHAR(10)    NULL,
    NC_RAZON                NVARCHAR(255)   NULL,
    FECHA_TRANSAC           DATETIME        NOT NULL DEFAULT GETDATE(),
    ESTADO_HACIENDA         NVARCHAR(10)    NOT NULL DEFAULT '00',
    OBSERVACIONES           NVARCHAR(500)   NULL
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 35. AVS_INTEGRAFAST_02 (Consecutivos de documentos electrónicos)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_02', N'U') IS NULL
CREATE TABLE dbo.AVS_INTEGRAFAST_02 (
    COD_SUCURSAL        NVARCHAR(10)    NOT NULL PRIMARY KEY,
    PROVEEDOR_SISTEMA   NVARCHAR(30)    NOT NULL DEFAULT '',
    CN_FE               INT             NOT NULL DEFAULT 0,   -- Consecutivo Factura Electrónica (tipo 01)
    CN_ND               INT             NOT NULL DEFAULT 0,   -- Consecutivo Nota de Débito (tipo 02)
    CN_NC               INT             NOT NULL DEFAULT 0,   -- Consecutivo Nota de Crédito (tipo 03)
    CN_TE               INT             NOT NULL DEFAULT 0,   -- Consecutivo Tiquete Electrónico (tipo 04)
    CN_FEX              INT             NOT NULL DEFAULT 0    -- Consecutivo Factura Exportación (tipo 09)
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 36. AVS_INTEGRAFAST_05 (Detalle de líneas del documento electrónico)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_05', N'U') IS NULL
CREATE TABLE dbo.AVS_INTEGRAFAST_05 (
    ID                          INT                 NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CLAVE50                     NVARCHAR(50)        NOT NULL DEFAULT '',
    TRANSACTIONNUMBER           NVARCHAR(20)        NOT NULL DEFAULT '',
    NUM_LINEA                   INT                 NOT NULL DEFAULT 0,
    ID_PRODUCTO                 NVARCHAR(15)        NOT NULL DEFAULT '',
    CANTIDAD                    DECIMAL(18,4)       NOT NULL DEFAULT 0,
    UNIDAD_MEDIDA               NVARCHAR(15)        NOT NULL DEFAULT 'Und',
    DETALLE                     NVARCHAR(160)       NOT NULL DEFAULT '',
    PRECIO_UNITARIO             DECIMAL(18,5)       NOT NULL DEFAULT 0,
    MONTO_TOTAL                 DECIMAL(18,2)       NOT NULL DEFAULT 0,
    MONTO_DESCUENTO             DECIMAL(18,2)       NOT NULL DEFAULT 0,
    NATURALEZA_DESCUENTO        NVARCHAR(80)        NOT NULL DEFAULT '',
    SUBTOTAL                    DECIMAL(18,2)       NOT NULL DEFAULT 0,
    COD_IMPUESTO                NVARCHAR(2)         NOT NULL DEFAULT '',
    COD_IMPUESTO_BASE           NVARCHAR(2)         NULL,
    TARIFA_IMPUESTO             DECIMAL(18,5)       NOT NULL DEFAULT 0,
    MONTO_IMPUESTO              DECIMAL(18,2)       NOT NULL DEFAULT 0,
    EXONERA_TIPO_DOCUMENTO      NVARCHAR(2)         NULL,
    EXONERA_NUMERO_DOCUMENTO    NVARCHAR(40)        NULL,
    EXONERA_INSTITUCION         NVARCHAR(100)       NULL,
    EXONERA_FECHA_EMISION       NVARCHAR(25)        NULL,
    EXONERA_MONTO_IMPUESTO      DECIMAL(18,2)       NULL,
    EXONERA_PORCENTAJE_COMPRA   SMALLINT            NULL,
    EXONERA_TOTAL_LINEA         DECIMAL(18,2)       NULL,
    SyncGuid                    UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID(),
    ARTICULO                    NVARCHAR(6)         NULL,
    INCISO                      NVARCHAR(6)         NULL
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- 37. AVS_INTEGRAFAST_01_EXONERA (Detalle de exoneraciones por línea)
-- ─────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'dbo.AVS_INTEGRAFAST_01_EXONERA', N'U') IS NULL
CREATE TABLE dbo.AVS_INTEGRAFAST_01_EXONERA (
    ID                  INT                 NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CLAVE50             NVARCHAR(50)        NOT NULL DEFAULT '',
    ITEMID              INT                 NOT NULL DEFAULT 0,
    EX_TARIFA_PORC      DECIMAL(18,5)       NOT NULL DEFAULT 0,
    EX_TARIFA_MONTO     DECIMAL(18,5)       NOT NULL DEFAULT 0,
    EX_TIPODOC          NVARCHAR(2)         NULL,
    EX_NUMERODOC        NVARCHAR(17)        NULL,
    EX_INSTITUCION      NVARCHAR(100)       NULL,
    EX_FECHA            DATETIME            NULL,
    EX_MONTO            DECIMAL(18,5)       NOT NULL DEFAULT 0,
    EX_PORCENTAJE       DECIMAL(18,5)       NOT NULL DEFAULT 0,
    SyncGuid            UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID()
);
GO

-- =====================================================================
-- TABLE-VALUED PARAMETERS (TVPs) para Stored Procedures
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- TVP para items de venta (usado por spNovaRetail_CreateSale)
-- ─────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'NovaRetailSaleItemTVP' AND is_table_type = 1)
CREATE TYPE dbo.NovaRetailSaleItemTVP AS TABLE (
    RowNo                       INT             NOT NULL,
    ItemID                      INT             NOT NULL,
    Quantity                    FLOAT           NOT NULL,
    UnitPrice                   MONEY           NOT NULL,
    FullPrice                   MONEY           NOT NULL,
    Cost                        MONEY           NOT NULL DEFAULT 0,
    Commission                  MONEY           NOT NULL DEFAULT 0,
    PriceSource                 MONEY           NOT NULL DEFAULT 0,
    SalesRepID                  INT             NULL,
    Taxable                     BIT             NOT NULL DEFAULT 1,
    TaxID                       INT             NOT NULL DEFAULT 0,
    SalesTax                    MONEY           NOT NULL DEFAULT 0,
    LineComment                 NVARCHAR(255)   NULL,
    DiscountReasonCodeID        INT             NULL,
    ReturnReasonCodeID          INT             NULL,
    TaxChangeReasonCodeID       INT             NULL,
    QuantityDiscountID          INT             NULL,
    ItemType                    INT             NOT NULL DEFAULT 0,
    ComputedQuantity            FLOAT           NOT NULL DEFAULT 0,
    IsAddMoney                  BIT             NOT NULL DEFAULT 0,
    VoucherID                   INT             NULL,
    ExtendedDescription         NVARCHAR(255)   NULL,
    PromotionID                 INT             NULL,
    PromotionName               NVARCHAR(100)   NULL,
    LineDiscountAmount          MONEY           NOT NULL DEFAULT 0,
    LineDiscountPercent         DECIMAL(18,4)   NOT NULL DEFAULT 0
);
GO

-- ─────────────────────────────────────────────────────────────────────
-- TVP para tenders/pagos de venta (usado por spNovaRetail_CreateSale)
-- ─────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'NovaRetailSaleTenderTVP' AND is_table_type = 1)
CREATE TYPE dbo.NovaRetailSaleTenderTVP AS TABLE (
    RowNo                       INT             NOT NULL,
    TenderID                    INT             NOT NULL,
    PaymentID                   INT             NULL,
    Description                 NVARCHAR(50)    NULL,
    Amount                      MONEY           NOT NULL,
    AmountForeign               MONEY           NOT NULL DEFAULT 0,
    RoundingError               MONEY           NOT NULL DEFAULT 0,
    CreditCardExpiration        VARCHAR(50)     NULL,
    CreditCardNumber            VARCHAR(25)     NULL,
    CreditCardApprovalCode      VARCHAR(20)     NULL,
    AccountHolder               VARCHAR(40)     NULL,
    BankNumber                  VARCHAR(25)     NULL,
    SerialNumber                VARCHAR(25)     NULL,
    State                       VARCHAR(10)     NULL,
    License                     VARCHAR(25)     NULL,
    BirthDate                   VARCHAR(25)     NULL,
    TransitNumber               VARCHAR(25)     NULL,
    VisaNetAuthorizationID      INT             NULL,
    DebitSurcharge              MONEY           NOT NULL DEFAULT 0,
    CashBackSurcharge           MONEY           NOT NULL DEFAULT 0,
    IsCreateNew                 BIT             NOT NULL DEFAULT 0,
    MedioPagoCodigo             NVARCHAR(10)    NULL
);
GO

-- =====================================================================
-- Stored Procedure principal para registrar ventas
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE dbo.spNovaRetail_CreateSale
(
    @StoreID                INT,
    @RegisterID             INT,
    @CashierID              INT,
    @CustomerID             INT             = 0,
    @ShipToID               INT             = 0,
    @Comment                NVARCHAR(255)   = N'',
    @ReferenceNumber        NVARCHAR(50)    = N'',
    @Status                 INT             = 0,
    @ExchangeID             INT             = 0,
    @ChannelType            INT             = 0,
    @RecallID               INT             = 0,
    @RecallType             INT             = 0,
    @TransactionTime        DATETIME        = NULL,
    @TotalChange            MONEY           = 0,
    @AllowNegativeInventory BIT             = 0,
    @CurrencyCode           VARCHAR(3)      = 'CRC',
    @TipoCambio             VARCHAR(9)      = '1',
    @CondicionVenta         VARCHAR(2)      = '01',
    @CodCliente             VARCHAR(15)     = '',
    @NombreCliente          VARCHAR(60)     = '',
    @CedulaTributaria       VARCHAR(12)     = '',
    @Exonera                SMALLINT        = 0,
    @InsertarTiqueteEspera  BIT             = 0,
    @CLAVE50                VARCHAR(50)     = '',
    @CLAVE20                VARCHAR(20)     = '',
    @COD_SUCURSAL           VARCHAR(3)      = '',
    @TERMINAL_POS           VARCHAR(5)      = '',
    @COMPROBANTE_INTERNO    VARCHAR(10)     = '',
    @COMPROBANTE_SITUACION  VARCHAR(1)      = '',
    @COMPROBANTE_TIPO       VARCHAR(2)      = '',
    @NC_TIPO_DOC            VARCHAR(2)      = '',
    @NC_REFERENCIA          VARCHAR(50)     = '',
    @NC_REFERENCIA_FECHA    DATETIME        = NULL,
    @NC_CODIGO              VARCHAR(2)      = '',
    @NC_RAZON               VARCHAR(180)    = '',
    @TR_REP                 VARCHAR(12)     = '',
    @Items                  dbo.NovaRetailSaleItemTVP   READONLY,
    @Tenders                dbo.NovaRetailSaleTenderTVP READONLY,
    @TransactionNumber      INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT OFF;

    DECLARE
        @Now            DATETIME,
        @TranDate       DATE,
        @BatchNumber    INT,
        @SubTotal       MONEY = 0,
        @Discounts      MONEY = 0,
        @SalesTax       MONEY = 0,
        @Total          MONEY = 0,
        @CostOfGoods    MONEY = 0,
        @Commission     MONEY = 0,
        @TenderTotal    MONEY = 0,
        @PaidToAccount  MONEY = 0,
        @PaidOnAccount  MONEY = 0,
        @IsReturn       BIT   = 0;

    SET @TransactionNumber = 0;

    IF @Status = 2 OR @COMPROBANTE_TIPO = '03'
        SET @IsReturn = 1;

    BEGIN TRY

        SET @Now      = ISNULL(@TransactionTime, GETDATE());
        SET @TranDate = CAST(@Now AS DATE);

        IF NOT EXISTS (SELECT 1 FROM @Items)
            THROW 51001, 'La venta no contiene lineas de detalle.', 1;

        IF NOT EXISTS (SELECT 1 FROM @Tenders)
            THROW 51002, 'La venta no contiene formas de pago.', 1;

        IF EXISTS (SELECT 1 FROM @Items WHERE Quantity <= 0)
            THROW 51003, 'Hay lineas con cantidad menor o igual a cero.', 1;

        IF EXISTS (SELECT 1 FROM @Items WHERE UnitPrice < 0)
            THROW 51004, 'Hay lineas con precio unitario negativo.', 1;

        IF EXISTS (SELECT 1 FROM @Tenders WHERE Amount <= 0)
            THROW 51005, 'Hay formas de pago con monto menor o igual a cero.', 1;

        SELECT TOP (1)
            @BatchNumber = B.BatchNumber
        FROM dbo.Batch B WITH (UPDLOCK, HOLDLOCK)
        WHERE B.StoreID    = @StoreID
          AND B.RegisterID = @RegisterID
          AND B.Status     IN (0, 2, 4, 6)
        ORDER BY B.BatchNumber DESC;

        IF @BatchNumber IS NULL
            THROW 51006, 'No existe un Batch abierto para el StoreID/RegisterID indicado.', 1;

        IF EXISTS
        (
            SELECT 1
            FROM   @Items I
            LEFT JOIN dbo.Item IT ON IT.ID = I.ItemID
            WHERE  IT.ID IS NULL
        )
            THROW 51007, 'Uno o mas ItemID no existen en dbo.Item.', 1;

        IF OBJECT_ID('dbo.Tender', 'U') IS NOT NULL
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM   @Tenders T
                LEFT JOIN dbo.Tender TD ON TD.ID = T.TenderID
                WHERE  TD.ID IS NULL
            )
                THROW 51008, 'Uno o mas TenderID no existen en el catalogo dbo.Tender.', 1;
        END;

        IF EXISTS (SELECT 1 FROM @Items WHERE Taxable = 1 AND ISNULL(TaxID, 0) = 0)
            THROW 51010, 'Hay items gravados sin TaxID.', 1;

        IF OBJECT_ID('dbo.Tax', 'U') IS NOT NULL
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM   @Items I
                LEFT JOIN dbo.Tax TX ON TX.ID = I.TaxID
                WHERE  I.Taxable = 1 AND I.TaxID IS NOT NULL AND TX.ID IS NULL
            )
                THROW 51011, 'Uno o mas TaxID no existen en dbo.Tax.', 1;
        END;

        IF @AllowNegativeInventory = 0 AND @IsReturn = 0
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM
                (
                    SELECT ItemID, SUM(Quantity) AS Qty
                    FROM @Items
                    GROUP BY ItemID
                ) R
                INNER JOIN dbo.Item IT WITH (UPDLOCK, HOLDLOCK) ON IT.ID = R.ItemID
                WHERE ISNULL(IT.Quantity, 0) < R.Qty
            )
                THROW 51013, 'Stock insuficiente para uno o mas articulos.', 1;
        END;

        SELECT
            @SubTotal    = SUM(CALC.SubTotal),
            @Discounts   = SUM(CALC.DiscountAmount),
            @SalesTax    = SUM(CALC.SalesTax),
            @CostOfGoods = SUM(ISNULL(I.Cost, 0) * I.Quantity),
            @Commission  = SUM(ISNULL(I.Commission, 0))
        FROM @Items I
        CROSS APPLY
        (
            SELECT
                GrossAmount = ROUND(ISNULL(I.FullPrice, I.UnitPrice) * I.Quantity, 2),
                DiscountAmount = CASE
                    WHEN ROUND(ISNULL(I.LineDiscountAmount, 0), 2) = 0
                     AND ISNULL(I.FullPrice, I.UnitPrice) > I.UnitPrice
                        THEN ROUND((ISNULL(I.FullPrice, I.UnitPrice) - I.UnitPrice) * I.Quantity, 2)
                    ELSE ROUND(ISNULL(I.LineDiscountAmount, 0), 2)
                END,
                SalesTax = ROUND(ISNULL(I.SalesTax, 0), 2)
        ) DISC
        CROSS APPLY
        (
            SELECT
                DiscountAmount = DISC.DiscountAmount,
                SalesTax = DISC.SalesTax,
                SubTotal = ROUND(DISC.GrossAmount - DISC.DiscountAmount, 2)
        ) CALC;

        SET @Total = @SubTotal + @SalesTax;

        SELECT @TenderTotal = SUM(T.Amount) FROM @Tenders T;

        IF ROUND(ISNULL(@TenderTotal, 0), 2) < ROUND(ISNULL(@Total, 0), 2)
            THROW 51014, 'El total de formas de pago es insuficiente para cubrir el total de la venta.', 1;

        SELECT
            @PaidToAccount = SUM(CASE WHEN T.TenderID IN (7, 22) THEN T.Amount ELSE 0 END),
            @PaidOnAccount = SUM(CASE WHEN T.TenderID NOT IN (7, 22) THEN T.Amount ELSE 0 END)
        FROM @Tenders T;

        BEGIN TRAN;

        INSERT INTO dbo.[Transaction]
        (
            ShipToID, StoreID, BatchNumber, [Time], CustomerID, CashierID,
            Total, SalesTax, Comment, ReferenceNumber, [Status],
            ExchangeID, ChannelType, RecallID, RecallType, SyncGuid
        )
        VALUES
        (
            @ShipToID, @StoreID, @BatchNumber, @Now, @CustomerID, @CashierID,
            @Total, @SalesTax, @Comment, @ReferenceNumber, @Status,
            @ExchangeID, @ChannelType, @RecallID, @RecallType, NEWID()
        );

        SET @TransactionNumber = CONVERT(INT, SCOPE_IDENTITY());

        DECLARE @InsertedEntries TABLE (EntryID INT NOT NULL, DetailID INT NOT NULL);

        INSERT INTO dbo.TransactionEntry
        (
            Commission, Cost, FullPrice, StoreID, TransactionNumber,
            ItemID, Price, PriceSource, Quantity, SalesRepID,
            Taxable, DetailID, Comment,
            DiscountReasonCodeID, ReturnReasonCodeID, TaxChangeReasonCodeID,
            SalesTax, QuantityDiscountID, ItemType, ComputedQuantity,
            TransactionTime, IsAddMoney, VoucherID, SyncGuid
        )
        OUTPUT INSERTED.ID, INSERTED.DetailID
        INTO @InsertedEntries (EntryID, DetailID)
        SELECT
            SRC.Commission, SRC.Cost, ISNULL(SRC.FullPrice, SRC.UnitPrice),
            @StoreID, @TransactionNumber, SRC.ItemID, SRC.UnitPrice,
            SRC.PriceSource, SRC.Quantity, SRC.SalesRepID,
            SRC.Taxable, SRC.RowNo - 1,
            ISNULL(NULLIF(LTRIM(RTRIM(SRC.LineComment)), ''), ''),
            SRC.DiscountReasonCodeID, SRC.ReturnReasonCodeID, SRC.TaxChangeReasonCodeID,
            SRC.SalesTax, SRC.QuantityDiscountID, SRC.ItemType, SRC.ComputedQuantity,
            @Now, SRC.IsAddMoney, SRC.VoucherID, NEWID()
        FROM @Items SRC
        ORDER BY SRC.RowNo;

        ;WITH Q AS (SELECT ItemID, SUM(Quantity) AS Qty FROM @Items GROUP BY ItemID)
        UPDATE IT
           SET IT.Quantity    = CASE WHEN @IsReturn = 1
                                     THEN ISNULL(IT.Quantity, 0) + Q.Qty
                                     ELSE ISNULL(IT.Quantity, 0) - Q.Qty
                                END,
               IT.LastSold    = @Now,
               IT.LastUpdated = @Now
        FROM dbo.Item IT INNER JOIN Q ON Q.ItemID = IT.ID;

        INSERT INTO dbo.TenderEntry
        (
            BatchNumber, CreditCardExpiration, OrderHistoryID, DropPayoutID, StoreID,
            TransactionNumber, TenderID, PaymentID, Description, CreditCardNumber,
            CreditCardApprovalCode, Amount, AccountHolder, RoundingError, AmountForeign,
            BankNumber, SerialNumber, [State], License, BirthDate, TransitNumber,
            VisaNetAuthorizationID, DebitSurcharge, CashBackSurcharge, IsCreateNew, SyncGuid
        )
        SELECT
            @BatchNumber, ISNULL(T.CreditCardExpiration,''), 0, 0, @StoreID,
            @TransactionNumber, T.TenderID, T.PaymentID, T.Description, ISNULL(T.CreditCardNumber,''),
            ISNULL(T.CreditCardApprovalCode,''), T.Amount, ISNULL(T.AccountHolder,''),
            ISNULL(T.RoundingError,0), ISNULL(T.AmountForeign, T.Amount),
            ISNULL(T.BankNumber,''), ISNULL(T.SerialNumber,''), ISNULL(T.[State],''),
            ISNULL(T.License,''), T.BirthDate, ISNULL(T.TransitNumber,''),
            ISNULL(T.VisaNetAuthorizationID,0), ISNULL(T.DebitSurcharge,0),
            ISNULL(T.CashBackSurcharge,0), ISNULL(T.IsCreateNew,0), NEWID()
        FROM @Tenders T
        ORDER BY T.RowNo;

        UPDATE B
           SET B.LastUpdated   = @Now,
               B.Sales         = ISNULL(B.Sales,0)         + @SubTotal,
               B.Tax           = ISNULL(B.Tax,0)           + @SalesTax,
               B.SalesPlusTax  = ISNULL(B.SalesPlusTax,0)  + @Total,
               B.Commission    = ISNULL(B.Commission,0)    + @Commission,
               B.PaidOnAccount = ISNULL(B.PaidOnAccount,0) + @PaidOnAccount,
               B.PaidToAccount = ISNULL(B.PaidToAccount,0) + @PaidToAccount,
               B.CustomerCount = ISNULL(B.CustomerCount,0) + 1,
               B.TotalTendered = ISNULL(B.TotalTendered,0) + @TenderTotal,
               B.TotalChange   = ISNULL(B.TotalChange,0)   + @TotalChange,
               B.Discounts     = ISNULL(B.Discounts,0)     + @Discounts,
               B.CostOfGoods   = ISNULL(B.CostOfGoods,0)   + @CostOfGoods
        FROM dbo.Batch B
        WHERE B.BatchNumber = @BatchNumber;

        COMMIT TRAN;

        SELECT
            CAST(1 AS BIT)       AS Ok,
            N'Venta registrada.' AS [Message],
            @TransactionNumber   AS TransactionNumber,
            @BatchNumber         AS BatchNumber,
            @SubTotal            AS SubTotal,
            @Discounts           AS Discounts,
            @SalesTax            AS SalesTax,
            @Total               AS Total,
            @TenderTotal         AS TenderTotal,
            NULL                 AS ErrorNumber,
            N''                  AS ErrorProcedure,
            NULL                 AS ErrorLine;

    END TRY
    BEGIN CATCH

        IF @@TRANCOUNT > 0 ROLLBACK TRAN;

        SELECT
            CAST(0 AS BIT)                 AS Ok,
            ERROR_MESSAGE()                AS [Message],
            0                              AS TransactionNumber,
            NULL                           AS BatchNumber,
            NULL                           AS SubTotal,
            NULL                           AS Discounts,
            NULL                           AS SalesTax,
            NULL                           AS Total,
            NULL                           AS TenderTotal,
            ERROR_NUMBER()                 AS ErrorNumber,
            ISNULL(ERROR_PROCEDURE(), N'') AS ErrorProcedure,
            ERROR_LINE()                   AS ErrorLine;

    END CATCH
END;
GO

-- =====================================================================
-- Stored Procedures auxiliares para Cuentas por Cobrar (abonos)
-- =====================================================================
CREATE OR ALTER PROCEDURE dbo.OFF_AR_LEDGERENTRY_INSERT
(
    @LastUpdated     DATETIME,
    @AccountID       INT,
    @CustomerID      INT,
    @StoreID         INT,
    @LinkType        TINYINT,
    @LinkID          INT,
    @AuditEntryID    INT,
    @DocumentType    TINYINT,
    @DocumentID      INT,
    @PostingDate     DATETIME,
    @DueDate         DATETIME,
    @LedgerType      TINYINT,
    @Reference       NVARCHAR(20),
    @Description     NVARCHAR(50),
    @CurrencyID      INT,
    @CurrencyFactor  FLOAT,
    @Positive        BIT,
    @ClosingDate     DATETIME,
    @ReasonID        INT,
    @HoldReasonID    INT,
    @UndoReasonID    INT,
    @PayMethodID     INT,
    @TransactionID   INT,
    @ExtReference    NVARCHAR(50),
    @Comment         NVARCHAR(255)
)
AS
BEGIN
    INSERT INTO dbo.AR_LedgerEntry
    (
        LastUpdated,
        AccountID,
        CustomerID,
        StoreID,
        LinkType,
        LinkID,
        DocumentType,
        DocumentID,
        PostingDate,
        DueDate,
        LedgerType,
        Reference,
        Description,
        CurrencyID,
        CurrencyFactor,
        Positive,
        ClosingDate,
        ReasonID,
        HoldReasonID,
        UndoReasonID,
        Comment,
        AuditEntryID,
        PayMethodID,
        TransactionID,
        ExtReference
    )
    VALUES
    (
        GETDATE(),
        @AccountID,
        @CustomerID,
        @StoreID,
        @LinkType,
        @LinkID,
        @DocumentType,
        @DocumentID,
        @PostingDate,
        @DueDate,
        @LedgerType,
        @Reference,
        @Description,
        @CurrencyID,
        @CurrencyFactor,
        @Positive,
        @ClosingDate,
        @ReasonID,
        @HoldReasonID,
        @UndoReasonID,
        @Comment,
        @AuditEntryID,
        @PayMethodID,
        @TransactionID,
        @ExtReference
    );

    DECLARE @ID INT = SCOPE_IDENTITY();
    SELECT ID, SyncGuid FROM dbo.AR_LedgerEntry WHERE ID = @ID;
END;
GO

CREATE OR ALTER PROCEDURE dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT
(
    @LedgerEntryID    INT,
    @AccountID        INT,
    @LedgerType       TINYINT,
    @DueDate          DATETIME,
    @PostingDate      DATETIME,
    @DetailType       TINYINT,
    @Reference        NVARCHAR(20),
    @Amount           MONEY,
    @AmountLCY        MONEY,
    @AmountACY        MONEY,
    @AuditEntryID     INT,
    @AppliedEntryID   INT,
    @AppliedAmount    MONEY,
    @UnapplyEntryID   INT,
    @UnapplyReasonID  INT,
    @ISCLOSING        BIT = 0
)
AS
BEGIN
    INSERT INTO dbo.AR_LedgerEntryDetail
    (
        LedgerEntryID,
        AccountID,
        LedgerType,
        DueDate,
        PostingDate,
        DetailType,
        Reference,
        Amount,
        AmountLCY,
        AmountACY,
        AuditEntryID,
        AppliedEntryID,
        AppliedAmount,
        UnapplyEntryID,
        UnapplyReasonID
    )
    VALUES
    (
        @LedgerEntryID,
        @AccountID,
        @LedgerType,
        @DueDate,
        @PostingDate,
        @DetailType,
        @Reference,
        @Amount,
        @AmountLCY,
        @AmountACY,
        @AuditEntryID,
        @AppliedEntryID,
        @AppliedAmount,
        @UnapplyEntryID,
        @UnapplyReasonID
    );

    IF (@ISCLOSING = 1)
    BEGIN
        UPDATE dbo.AR_LedgerEntry
        SET ClosingDate = GETDATE()
        WHERE ID = @LedgerEntryID;
    END;

    UPDATE dbo.AR_LedgerEntry
    SET LastUpdated = GETDATE()
    WHERE ID = @LedgerEntryID;

    SELECT CONVERT(INT, SCOPE_IDENTITY()) AS [SCOPE];
END;
GO


PRINT '============================================================='
PRINT ' Script ejecutado correctamente.'
PRINT ' Base de datos creada con todas las tablas limpias.'
PRINT ' AVS_Parametros cargada con datos de configuración.'
PRINT '============================================================='
GO
-- =====================================================================
-- EXTENSIONES NOVARETAIL
-- =====================================================================

IF OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NovaRetail_ActionLog
    (
        ID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_NovaRetail_ActionLog PRIMARY KEY,
        ActionDate DATETIME NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_ActionDate DEFAULT (GETDATE()),
        ActionType NVARCHAR(40) NOT NULL,
        EntityType NVARCHAR(40) NOT NULL,
        EntityID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_EntityID DEFAULT (0),
        CashierID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_CashierID DEFAULT (0),
        CashierName NVARCHAR(120) NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_CashierName DEFAULT (''),
        StoreID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_StoreID DEFAULT (0),
        RegisterID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_RegisterID DEFAULT (0),
        Amount MONEY NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_Amount DEFAULT (0),
        Detail NVARCHAR(1000) NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_Detail DEFAULT ('')
    );
END
GO

IF OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'ActionDate') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD ActionDate DATETIME NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_ActionDate_Upgrade DEFAULT (GETDATE()) WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'ActionType') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD ActionType NVARCHAR(40) NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_ActionType_Upgrade DEFAULT ('') WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'EntityType') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD EntityType NVARCHAR(40) NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_EntityType_Upgrade DEFAULT ('') WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'EntityID') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD EntityID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_EntityID_Upgrade DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'CashierID') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD CashierID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_CashierID_Upgrade DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'CashierName') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD CashierName NVARCHAR(120) NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_CashierName_Upgrade DEFAULT ('') WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'StoreID') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD StoreID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_StoreID_Upgrade DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'RegisterID') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD RegisterID INT NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_RegisterID_Upgrade DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'Amount') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD Amount MONEY NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_Amount_Upgrade DEFAULT (0) WITH VALUES;

    IF COL_LENGTH(N'dbo.NovaRetail_ActionLog', N'Detail') IS NULL
        ALTER TABLE dbo.NovaRetail_ActionLog ADD Detail NVARCHAR(1000) NOT NULL CONSTRAINT DF_NovaRetail_ActionLog_Detail_Upgrade DEFAULT ('') WITH VALUES;
END
GO

IF OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_NovaRetail_ActionLog_ActionDate'
         AND object_id = OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U')
   )
BEGIN
    CREATE INDEX IX_NovaRetail_ActionLog_ActionDate
        ON dbo.NovaRetail_ActionLog(ActionDate DESC);
END
GO

IF OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_NovaRetail_ActionLog_ActionType'
         AND object_id = OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U')
   )
BEGIN
    CREATE INDEX IX_NovaRetail_ActionLog_ActionType
        ON dbo.NovaRetail_ActionLog(ActionType, ActionDate DESC);
END
GO

IF OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_NovaRetail_ActionLog_StoreDate'
         AND object_id = OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U')
   )
BEGIN
    CREATE INDEX IX_NovaRetail_ActionLog_StoreDate
        ON dbo.NovaRetail_ActionLog(StoreID, ActionDate DESC);
END
GO

PRINT ' Extensiones NovaRetail de dashboard y auditoria aplicadas.'
GO

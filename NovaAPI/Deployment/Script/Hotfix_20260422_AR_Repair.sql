SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @Now DATETIME;
DECLARE @Tolerance DECIMAL(18, 4);
DECLARE @Invoice105507Total DECIMAL(18, 4);
DECLARE @AppliedInvoice105507Total DECIMAL(18, 4);
DECLARE @Invoice105519Total DECIMAL(18, 4);
DECLARE @Invoice105520Total DECIMAL(18, 4);
DECLARE @LedgerInvoice105507ID INT;
DECLARE @LedgerNC105509ID INT;
DECLARE @AccountID105507 INT;

SET @Now = GETDATE();
SET @Tolerance = 0.01;

SELECT @Invoice105507Total = ROUND(SUM(te.Amount), 4)
FROM dbo.TenderEntry te
WHERE te.TransactionNumber = 105507;

SELECT @Invoice105519Total = ROUND(SUM(te.Amount), 4)
FROM dbo.TenderEntry te
WHERE te.TransactionNumber = 105519;

SELECT @Invoice105520Total = ROUND(SUM(te.Amount), 4)
FROM dbo.TenderEntry te
WHERE te.TransactionNumber = 105520;

SET @AppliedInvoice105507Total = NULL;
IF @Invoice105507Total IS NOT NULL
    SET @AppliedInvoice105507Total = 0 - @Invoice105507Total;

IF @Invoice105507Total IS NOT NULL
BEGIN
    UPDATE dbo.[Transaction]
    SET Total = @Invoice105507Total
    WHERE TransactionNumber = 105507
      AND ABS(ISNULL(Total, 0) - @Invoice105507Total) > @Tolerance;

    UPDATE d
    SET d.Amount = @Invoice105507Total,
        d.AmountLCY = @Invoice105507Total,
        d.AmountACY = @Invoice105507Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = 'TR:105507'
      AND d.AppliedEntryID = 0
      AND ABS(ISNULL(d.Amount, 0) - @Invoice105507Total) > @Tolerance;
END;

IF @Invoice105519Total IS NOT NULL
BEGIN
    UPDATE dbo.[Transaction]
    SET Total = @Invoice105519Total
    WHERE TransactionNumber = 105519
      AND ABS(ISNULL(Total, 0) - @Invoice105519Total) > @Tolerance;

    UPDATE d
    SET d.Amount = @Invoice105519Total,
        d.AmountLCY = @Invoice105519Total,
        d.AmountACY = @Invoice105519Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = 'TR:105519'
      AND d.AppliedEntryID = 0
      AND ABS(ISNULL(d.Amount, 0) - @Invoice105519Total) > @Tolerance;

    UPDATE d
    SET d.Amount = -@Invoice105519Total,
        d.AmountLCY = -@Invoice105519Total,
        d.AmountACY = -@Invoice105519Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = '105519'
      AND le.Description = 'Abono a cuenta'
      AND d.AppliedEntryID = 0
      AND ABS(ISNULL(d.Amount, 0) + @Invoice105519Total) > @Tolerance;

    UPDATE d
    SET d.AppliedAmount = -@Invoice105519Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = '105519'
      AND le.Description = 'Abono a cuenta'
      AND d.AppliedEntryID > 0
      AND ABS(ISNULL(d.AppliedAmount, 0) + @Invoice105519Total) > @Tolerance;
END;

IF @Invoice105520Total IS NOT NULL
BEGIN
    UPDATE dbo.[Transaction]
    SET Total = @Invoice105520Total
    WHERE TransactionNumber = 105520
      AND ABS(ISNULL(Total, 0) - @Invoice105520Total) > @Tolerance;

    UPDATE d
    SET d.Amount = @Invoice105520Total,
        d.AmountLCY = @Invoice105520Total,
        d.AmountACY = @Invoice105520Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = 'TR:105520'
      AND d.AppliedEntryID = 0
      AND ABS(ISNULL(d.Amount, 0) - @Invoice105520Total) > @Tolerance;

    UPDATE d
    SET d.Amount = -@Invoice105520Total,
        d.AmountLCY = -@Invoice105520Total,
        d.AmountACY = -@Invoice105520Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = '105520'
      AND le.Description = 'Abono a cuenta'
      AND d.AppliedEntryID = 0
      AND ABS(ISNULL(d.Amount, 0) + @Invoice105520Total) > @Tolerance;

    UPDATE d
    SET d.AppliedAmount = -@Invoice105520Total
    FROM dbo.AR_LedgerEntryDetail d
    INNER JOIN dbo.AR_LedgerEntry le
        ON le.ID = d.LedgerEntryID
    WHERE le.Reference = '105520'
      AND le.Description = 'Abono a cuenta'
      AND d.AppliedEntryID > 0
      AND ABS(ISNULL(d.AppliedAmount, 0) + @Invoice105520Total) > @Tolerance;
END;

UPDATE le
SET DocumentType = CASE WHEN le.ID = 100280 THEN 3 ELSE le.DocumentType END,
    DocumentID = CASE
        WHEN le.Reference LIKE 'TR:%' THEN CAST(REPLACE(le.Reference, 'TR:', '') AS INT)
        ELSE le.DocumentID
    END,
    LedgerType = CASE
        WHEN le.ID = 100280 THEN 4
        WHEN le.ID IN (100278, 100288, 100290, 100289, 100291) THEN 3
        ELSE le.LedgerType
    END,
    LinkType = CASE WHEN le.LinkType = 0 THEN 1 ELSE le.LinkType END,
    LinkID = CASE WHEN ISNULL(le.LinkID, 0) = 0 THEN le.CustomerID ELSE le.LinkID END,
    Comment = CASE
        WHEN le.ID = 100280 AND ISNULL(NULLIF(le.Comment, ''), '') = '' THEN 'NC aplicada a TR:105507'
        ELSE le.Comment
    END,
    LastUpdated = @Now
FROM dbo.AR_LedgerEntry le
WHERE le.ID IN (100278, 100280, 100288, 100289, 100290, 100291);

SELECT TOP 1 @LedgerInvoice105507ID = le.ID
FROM dbo.AR_LedgerEntry le
WHERE le.Reference = 'TR:105507'
ORDER BY le.ID DESC;

SELECT TOP 1 @LedgerNC105509ID = le.ID
FROM dbo.AR_LedgerEntry le
WHERE le.Reference = 'TR:105509'
ORDER BY le.ID DESC;

SELECT TOP 1 @AccountID105507 = le.AccountID
FROM dbo.AR_LedgerEntry le
WHERE le.ID = @LedgerInvoice105507ID;

IF @LedgerInvoice105507ID > 0
   AND @LedgerNC105509ID > 0
   AND @AccountID105507 > 0
   AND @AppliedInvoice105507Total IS NOT NULL
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.AR_LedgerEntryDetail d
        WHERE d.LedgerEntryID = @LedgerNC105509ID
          AND d.AppliedEntryID = @LedgerInvoice105507ID
    )
    BEGIN
        EXEC dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT
            @LedgerEntryID = @LedgerNC105509ID,
            @AccountID = @AccountID105507,
            @LedgerType = 4,
            @DueDate = @Now,
            @PostingDate = @Now,
            @DetailType = 0,
            @Reference = 'TR:105509',
            @Amount = 0,
            @AmountLCY = 0,
            @AmountACY = 0,
            @AuditEntryID = 0,
            @AppliedEntryID = @LedgerInvoice105507ID,
            @AppliedAmount = @AppliedInvoice105507Total,
            @UnapplyEntryID = 0,
            @UnapplyReasonID = 0,
            @ISCLOSING = 0;
    END;
END;

;WITH LedgerBalances AS
(
    SELECT
        le.ID,
        BaseAmount = ISNULL(
            (
                SELECT SUM(d.Amount)
                FROM dbo.AR_LedgerEntryDetail d
                WHERE d.LedgerEntryID = le.ID
                  AND d.AppliedEntryID = 0
            ), 0),
        AppliedToEntry = ISNULL(
            (
                SELECT SUM(d.AppliedAmount)
                FROM dbo.AR_LedgerEntryDetail d
                WHERE d.AppliedEntryID = le.ID
            ), 0),
        AppliedByEntry = ISNULL(
            (
                SELECT SUM(ABS(d.AppliedAmount))
                FROM dbo.AR_LedgerEntryDetail d
                WHERE d.LedgerEntryID = le.ID
                  AND d.AppliedEntryID > 0
            ), 0)
    FROM dbo.AR_LedgerEntry le
    WHERE le.ID IN (100278, 100280, 100288, 100289, 100290, 100291)
),
LedgerRemaining AS
(
    SELECT
        lb.ID,
        Remaining = CASE
            WHEN lb.BaseAmount < 0 THEN ABS(lb.BaseAmount) - lb.AppliedByEntry
            ELSE lb.BaseAmount + lb.AppliedToEntry
        END
    FROM LedgerBalances lb
)
UPDATE le
SET ClosingDate = CASE
        WHEN ABS(lr.Remaining) > @Tolerance THEN NULL
        ELSE ISNULL(le.ClosingDate, @Now)
    END,
    LastUpdated = @Now
FROM dbo.AR_LedgerEntry le
INNER JOIN LedgerRemaining lr
    ON lr.ID = le.ID;

COMMIT;

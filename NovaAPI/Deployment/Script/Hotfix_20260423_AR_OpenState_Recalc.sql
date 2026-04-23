SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @Now DATETIME = GETDATE();
DECLARE @Tolerance DECIMAL(18, 4) = 0.01;

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
    WHERE EXISTS
    (
        SELECT 1
        FROM dbo.AR_LedgerEntryDetail d
        WHERE d.LedgerEntryID = le.ID
    )
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
    ON lr.ID = le.ID
WHERE (
        ABS(lr.Remaining) <= @Tolerance
        AND le.ClosingDate IS NULL
      )
   OR (
        ABS(lr.Remaining) > @Tolerance
        AND le.ClosingDate IS NOT NULL
      );

COMMIT;

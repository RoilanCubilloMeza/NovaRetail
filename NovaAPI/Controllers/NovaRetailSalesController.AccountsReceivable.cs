using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
        private sealed class LedgerBalanceSnapshot
        {
            public decimal BaseAmount { get; set; }
            public decimal AppliedToEntry { get; set; }
        }

        private sealed class SaleTotalsSnapshot
        {
            public decimal SubTotal { get; set; }
            public decimal Discounts { get; set; }
            public decimal SalesTax { get; set; }
            public decimal Total { get; set; }
        }

        private const int LedgerReferenceMaxLength = 20;
        private const int LedgerExternalReferenceMaxLength = 50;

        private static string NormalizeTransactionReference(string reference)
        {
            var value = (reference ?? string.Empty).Trim();
            if (value.StartsWith("TR:", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(3).Trim();

            return value;
        }

        private static string BuildLedgerReference(string transactionReference)
        {
            var normalized = NormalizeTransactionReference(transactionReference);
            var reference = string.IsNullOrWhiteSpace(normalized)
                ? string.Empty
                : "TR:" + normalized;
            return TruncateLedgerReference(reference);
        }

        private static string TruncateLedgerReference(string reference)
        {
            var value = (reference ?? string.Empty).Trim();
            return value.Length <= LedgerReferenceMaxLength
                ? value
                : value.Substring(0, LedgerReferenceMaxLength);
        }

        private static string TruncateLedgerExternalReference(string reference)
        {
            var value = NormalizeTransactionReference(reference);
            return value.Length <= LedgerExternalReferenceMaxLength
                ? value
                : value.Substring(0, LedgerExternalReferenceMaxLength);
        }

        private static List<string> BuildCreditNoteReferenceCandidates(NovaRetailCreateSaleRequest request)
        {
            var values = new[]
            {
                request?.TR_REP,
                request?.ReferenceNumber,
                request?.NC_REFERENCIA
            };

            return values
                .Select(NormalizeTransactionReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static SaleTotalsSnapshot CalculateRequestedSaleTotals(NovaRetailCreateSaleRequest request)
        {
            var totals = new SaleTotalsSnapshot();
            if (request?.Items == null || request.Items.Count == 0)
                return totals;

            foreach (var item in request.Items)
            {
                var qty = item.Quantity <= 0 ? 1m : item.Quantity;
                var unitPrice = item.UnitPrice;
                var fullPrice = item.FullPrice ?? unitPrice;
                var grossAmount = Math.Round(fullPrice * qty, 2, MidpointRounding.AwayFromZero);
                var discountAmount = Math.Round(item.LineDiscountAmount, 2, MidpointRounding.AwayFromZero);

                if (discountAmount == 0m && fullPrice > unitPrice)
                    discountAmount = Math.Round((fullPrice - unitPrice) * qty, 2, MidpointRounding.AwayFromZero);

                var subTotal = Math.Round(grossAmount - discountAmount, 2, MidpointRounding.AwayFromZero);
                var salesTax = Math.Round(item.SalesTax, 2, MidpointRounding.AwayFromZero);

                totals.SubTotal += subTotal;
                totals.Discounts += discountAmount;
                totals.SalesTax += salesTax;
                totals.Total += subTotal + salesTax;
            }

            totals.SubTotal = Math.Round(totals.SubTotal, 2, MidpointRounding.AwayFromZero);
            totals.Discounts = Math.Round(totals.Discounts, 2, MidpointRounding.AwayFromZero);
            totals.SalesTax = Math.Round(totals.SalesTax, 2, MidpointRounding.AwayFromZero);
            totals.Total = Math.Round(totals.Total, 2, MidpointRounding.AwayFromZero);

            return totals;
        }

        private static SaleTotalsSnapshot SyncPersistedTransactionTotals(SqlConnection cn, int transactionNumber, NovaRetailCreateSaleRequest request)
        {
            var totals = CalculateRequestedSaleTotals(request);
            if (transactionNumber <= 0 || totals.Total <= 0m)
                return totals;

            using (var cmd = new SqlCommand(@"
UPDATE dbo.[Transaction]
SET Total = @Total,
    SalesTax = @SalesTax
WHERE TransactionNumber = @TransactionNumber
  AND
  (
      ABS(ISNULL(Total, 0) - @Total) > @Tolerance
      OR ABS(ISNULL(SalesTax, 0) - @SalesTax) > @Tolerance
  );", cn))
            {
                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                cmd.Parameters.AddWithValue("@Total", totals.Total);
                cmd.Parameters.AddWithValue("@SalesTax", totals.SalesTax);
                cmd.Parameters.AddWithValue("@Tolerance", LedgerClosingTolerance);
                cmd.ExecuteNonQuery();
            }

            return totals;
        }

        private static void SyncPersistedTransactionEntryValues(SqlConnection cn, int transactionNumber, NovaRetailCreateSaleRequest request)
        {
            if (transactionNumber <= 0 || request?.Items == null || request.Items.Count == 0)
                return;

            var syncRows = new DataTable();
            syncRows.Columns.Add("DetailID", typeof(int));
            syncRows.Columns.Add("Price", typeof(decimal));
            syncRows.Columns.Add("FullPrice", typeof(decimal));
            syncRows.Columns.Add("Cost", typeof(decimal));

            foreach (var item in request.Items)
            {
                if (item == null || item.RowNo <= 0)
                    continue;

                var displayPrice = item.DisplayPrice ?? item.UnitPrice;
                var displayFullPrice = item.DisplayFullPrice ?? item.FullPrice ?? displayPrice;

                if (displayPrice < 0m || displayFullPrice < 0m)
                    continue;

                syncRows.Rows.Add(item.RowNo - 1, displayPrice, displayFullPrice, item.Cost);
            }

            if (syncRows.Rows.Count == 0)
                return;

            using (var createCmd = new SqlCommand(@"
CREATE TABLE #NovaRetailEntrySync
(
    DetailID INT NOT NULL PRIMARY KEY,
    Price DECIMAL(19, 4) NOT NULL,
    FullPrice DECIMAL(19, 4) NOT NULL,
    Cost DECIMAL(19, 4) NOT NULL
);", cn))
            {
                createCmd.ExecuteNonQuery();
            }

            try
            {
                using (var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.TableLock, null))
                {
                    bulk.DestinationTableName = "#NovaRetailEntrySync";
                    bulk.BulkCopyTimeout = 30;
                    bulk.ColumnMappings.Add("DetailID", "DetailID");
                    bulk.ColumnMappings.Add("Price", "Price");
                    bulk.ColumnMappings.Add("FullPrice", "FullPrice");
                    bulk.ColumnMappings.Add("Cost", "Cost");
                    bulk.WriteToServer(syncRows);
                }

                using (var cmd = new SqlCommand(@"
UPDATE TE
   SET TE.Price = S.Price,
       TE.FullPrice = S.FullPrice,
       TE.Cost = CASE
           WHEN S.Cost > 0 THEN S.Cost
           WHEN ISNULL(TE.Cost, 0) = 0 THEN ISNULL(IT.Cost, 0)
           ELSE TE.Cost
       END
FROM dbo.TransactionEntry TE
LEFT JOIN dbo.Item IT ON IT.ID = TE.ItemID
INNER JOIN #NovaRetailEntrySync S ON S.DetailID = ISNULL(TE.DetailID, -1)
WHERE TE.TransactionNumber = @TransactionNumber
  AND
  (
      ABS(ISNULL(TE.Price, 0) - S.Price) > @Tolerance
      OR ABS(ISNULL(TE.FullPrice, 0) - S.FullPrice) > @Tolerance
      OR (S.Cost > 0 AND ABS(ISNULL(TE.Cost, 0) - S.Cost) > @Tolerance)
      OR (S.Cost <= 0 AND ISNULL(TE.Cost, 0) = 0 AND ISNULL(IT.Cost, 0) <> 0)
  );", cn))
                {
                    cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                    cmd.Parameters.AddWithValue("@Tolerance", LedgerClosingTolerance);
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                using (var dropCmd = new SqlCommand("IF OBJECT_ID('tempdb..#NovaRetailEntrySync') IS NOT NULL DROP TABLE #NovaRetailEntrySync;", cn))
                {
                    dropCmd.ExecuteNonQuery();
                }
            }
        }

        private static decimal LoadPersistedTransactionTotal(SqlConnection cn, SqlTransaction tx, int transactionNumber, decimal fallback)
        {
            if (transactionNumber <= 0)
                return fallback;

            using (var cmd = new SqlCommand("SELECT TOP 1 Total FROM dbo.[Transaction] WHERE TransactionNumber = @TransactionNumber", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return fallback;

                return Math.Abs(Convert.ToDecimal(result));
            }
        }

        private static void AddInParameters(SqlCommand cmd, string prefix, IEnumerable<string> values, List<string> names)
        {
            foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var name = "@" + prefix + names.Count.ToString(CultureInfo.InvariantCulture);
                names.Add(name);
                cmd.Parameters.AddWithValue(name, value);
            }
        }

        private static void AddIntInParameters(SqlCommand cmd, string prefix, IEnumerable<int> values, List<string> names)
        {
            foreach (var value in values.Where(v => v > 0).Distinct())
            {
                var name = "@" + prefix + names.Count.ToString(CultureInfo.InvariantCulture);
                names.Add(name);
                cmd.Parameters.AddWithValue(name, value);
            }
        }

        private static List<int> ExtractTransactionNumberCandidates(IEnumerable<string> transactionReferences)
        {
            return (transactionReferences ?? Enumerable.Empty<string>())
                .Select(NormalizeTransactionReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value =>
                {
                    int transactionNumber;
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out transactionNumber)
                        ? transactionNumber
                        : 0;
                })
                .Where(value => value > 0)
                .Distinct()
                .ToList();
        }

        private static List<int> ResolveFiscalTransactionNumberCandidates(SqlConnection cn, SqlTransaction tx, IEnumerable<string> transactionReferences)
        {
            var references = (transactionReferences ?? Enumerable.Empty<string>())
                .Select(NormalizeTransactionReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var transactionNumbers = ExtractTransactionNumberCandidates(references);
            if (references.Count == 0)
                return transactionNumbers;

            var referenceNames = new List<string>();
            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cmd.Transaction = tx;
                cmd.CommandTimeout = 15;
                AddInParameters(cmd, "Ref", references, referenceNames);

                var parts = new List<string>();
                foreach (var name in referenceNames)
                {
                    parts.Add("SELECT TRY_CONVERT(INT, TRANSACTIONNUMBER) AS TransactionNumber FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = " + name);
                    parts.Add("SELECT TRY_CONVERT(INT, TRANSACTIONNUMBER) AS TransactionNumber FROM dbo.AVS_INTEGRAFAST_01 WHERE CLAVE50 = " + name);
                    parts.Add("SELECT TRY_CONVERT(INT, TRANSACTIONNUMBER) AS TransactionNumber FROM dbo.AVS_INTEGRAFAST_01 WHERE CLAVE20 = " + name);
                    parts.Add("SELECT TRY_CONVERT(INT, TRANSACTIONNUMBER) AS TransactionNumber FROM dbo.AVS_INTEGRAFAST_01 WHERE COMPROBANTE_INTERNO = " + name);
                }

                if (parts.Count == 0)
                    return transactionNumbers;

                cmd.CommandText = @"
SELECT DISTINCT TransactionNumber
FROM
(
    " + string.Join("\r\n    UNION ALL\r\n    ", parts) + @"
) s
WHERE TransactionNumber IS NOT NULL;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var value = reader["TransactionNumber"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TransactionNumber"]);
                        if (value > 0)
                            transactionNumbers.Add(value);
                    }
                }
            }

            return transactionNumbers
                .Where(value => value > 0)
                .Distinct()
                .ToList();
        }

        private static int ResolveReferencedLedgerEntryIDByTransactionNumber(SqlConnection cn, SqlTransaction tx, int accountID, IEnumerable<int> transactionNumbers)
        {
            var numbers = (transactionNumbers ?? Enumerable.Empty<int>())
                .Where(value => value > 0)
                .Distinct()
                .ToList();

            if (accountID <= 0 || numbers.Count == 0)
                return 0;

            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cmd.Transaction = tx;
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@AccountID", accountID);

                var names = new List<string>();
                for (var index = 0; index < numbers.Count; index++)
                {
                    var name = "@Txn" + index.ToString(CultureInfo.InvariantCulture);
                    names.Add(name);
                    cmd.Parameters.AddWithValue(name, numbers[index]);
                }

                cmd.CommandText = @"
SELECT TOP 1 le.ID
FROM dbo.AR_LedgerEntry le
WHERE le.AccountID = @AccountID
  AND le.DocumentID IN (" + string.Join(", ", names) + @")
  AND le.DocumentType IN (1, 2, 3, 4)
  AND EXISTS
  (
      SELECT 1
      FROM dbo.AR_LedgerEntryDetail d
      WHERE d.LedgerEntryID = le.ID
        AND d.AppliedEntryID = 0
        AND d.Amount > 0
  )
ORDER BY CASE WHEN le.[Open] = 1 THEN 0 ELSE 1 END,
         le.ID DESC;";

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static bool ShouldUseWideReferenceLookup(IEnumerable<string> normalizedReferences)
        {
            return (normalizedReferences ?? Enumerable.Empty<string>())
                .Any(value =>
                {
                    var reference = NormalizeTransactionReference(value);
                    if (string.IsNullOrWhiteSpace(reference))
                        return false;

                    if (reference.Length >= 20)
                        return true;

                    return reference.Any(c => !char.IsDigit(c));
                });
        }

        private static int ResolveReferencedLedgerEntryID(SqlConnection cn, SqlTransaction tx, int accountID, IEnumerable<string> transactionReferences)
        {
            var normalizedReferences = (transactionReferences ?? Enumerable.Empty<string>())
                .Select(NormalizeTransactionReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (accountID <= 0 || normalizedReferences.Count == 0)
                return 0;

            var directMatch = ResolveReferencedLedgerEntryIDByTransactionNumber(cn, tx, accountID, ExtractTransactionNumberCandidates(normalizedReferences));
            if (directMatch > 0)
                return directMatch;

            if (!ShouldUseWideReferenceLookup(normalizedReferences))
                return 0;

            var ledgerReferences = normalizedReferences
                .Select(BuildLedgerReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var referenceNames = new List<string>();
            var ledgerReferenceNames = new List<string>();
            var documentIdNames = new List<string>();

            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cmd.Transaction = tx;
                cmd.CommandTimeout = 20;
                AddInParameters(cmd, "Ref", normalizedReferences, referenceNames);
                AddInParameters(cmd, "LedgerRef", ledgerReferences, ledgerReferenceNames);
                cmd.Parameters.AddWithValue("@AccountID", accountID);
                AddIntInParameters(cmd, "DocID", ResolveFiscalTransactionNumberCandidates(cn, tx, normalizedReferences), documentIdNames);

                var candidateQueries = new List<string>();
                if (ledgerReferenceNames.Count > 0)
                {
                    candidateQueries.Add(@"
SELECT le.ID, le.[Open]
FROM dbo.AR_LedgerEntry le
WHERE le.AccountID = @AccountID
  AND le.Reference IN (" + string.Join(", ", ledgerReferenceNames) + @")
  AND le.DocumentType IN (1, 2, 3, 4)
  AND EXISTS
  (
      SELECT 1
      FROM dbo.AR_LedgerEntryDetail d
      WHERE d.LedgerEntryID = le.ID
        AND d.AppliedEntryID = 0
        AND d.Amount > 0
  )");
                }

                if (referenceNames.Count > 0)
                {
                    candidateQueries.Add(@"
SELECT le.ID, le.[Open]
FROM dbo.AR_LedgerEntry le
WHERE le.AccountID = @AccountID
  AND le.ExtReference IN (" + string.Join(", ", referenceNames) + @")
  AND le.DocumentType IN (1, 2, 3, 4)
  AND EXISTS
  (
      SELECT 1
      FROM dbo.AR_LedgerEntryDetail d
      WHERE d.LedgerEntryID = le.ID
        AND d.AppliedEntryID = 0
        AND d.Amount > 0
  )");
                }

                if (documentIdNames.Count > 0)
                {
                    candidateQueries.Add(@"
SELECT le.ID, le.[Open]
FROM dbo.AR_LedgerEntry le
WHERE le.AccountID = @AccountID
  AND le.DocumentID IN (" + string.Join(", ", documentIdNames) + @")
  AND le.DocumentType IN (1, 2, 3, 4)
  AND EXISTS
  (
      SELECT 1
      FROM dbo.AR_LedgerEntryDetail d
      WHERE d.LedgerEntryID = le.ID
        AND d.AppliedEntryID = 0
        AND d.Amount > 0
  )");
                }

                if (candidateQueries.Count == 0)
                    return 0;

                cmd.CommandText = @"
SELECT TOP 1 ID
FROM
(
    " + string.Join("\r\n    UNION ALL\r\n    ", candidateQueries) + @"
) Candidates
ORDER BY CASE WHEN [Open] = 1 THEN 0 ELSE 1 END,
         ID DESC;";

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
        }

        private static string ResolveReferencedLedgerAccountNumber(SqlConnection cn, IEnumerable<string> transactionReferences)
        {
            var normalizedReferences = (transactionReferences ?? Enumerable.Empty<string>())
                .Select(NormalizeTransactionReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedReferences.Count == 0)
                return string.Empty;

            if (!ShouldUseWideReferenceLookup(normalizedReferences))
                return string.Empty;

            var ledgerReferences = normalizedReferences
                .Select(BuildLedgerReference)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var referenceNames = new List<string>();
            var ledgerReferenceNames = new List<string>();
            var documentIdNames = new List<string>();

            using (var cmd = new SqlCommand())
            {
                cmd.Connection = cn;
                cmd.CommandTimeout = 20;
                AddInParameters(cmd, "Ref", normalizedReferences, referenceNames);
                AddInParameters(cmd, "LedgerRef", ledgerReferences, ledgerReferenceNames);
                AddIntInParameters(cmd, "DocID", ResolveFiscalTransactionNumberCandidates(cn, null, normalizedReferences), documentIdNames);

                var candidateQueries = new List<string>();
                if (ledgerReferenceNames.Count > 0)
                {
                    candidateQueries.Add(@"
SELECT le.ID, le.[Open], a.Number
FROM dbo.AR_LedgerEntry le
INNER JOIN dbo.AR_Account a ON a.ID = le.AccountID
WHERE le.Reference IN (" + string.Join(", ", ledgerReferenceNames) + @")
  AND le.DocumentType IN (1, 2, 3, 4)");
                }

                if (referenceNames.Count > 0)
                {
                    candidateQueries.Add(@"
SELECT le.ID, le.[Open], a.Number
FROM dbo.AR_LedgerEntry le
INNER JOIN dbo.AR_Account a ON a.ID = le.AccountID
WHERE le.ExtReference IN (" + string.Join(", ", referenceNames) + @")
  AND le.DocumentType IN (1, 2, 3, 4)");
                }

                if (documentIdNames.Count > 0)
                {
                    candidateQueries.Add(@"
SELECT le.ID, le.[Open], a.Number
FROM dbo.AR_LedgerEntry le
INNER JOIN dbo.AR_Account a ON a.ID = le.AccountID
WHERE le.DocumentID IN (" + string.Join(", ", documentIdNames) + @")
  AND le.DocumentType IN (1, 2, 3, 4)");
                }

                if (candidateQueries.Count == 0)
                    return string.Empty;

                cmd.CommandText = @"
SELECT TOP 1 Number
FROM
(
    " + string.Join("\r\n    UNION ALL\r\n    ", candidateQueries) + @"
) Candidates
ORDER BY CASE WHEN [Open] = 1 THEN 0 ELSE 1 END,
         ID DESC;";

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result);
            }
        }

        private static LedgerBalanceSnapshot LoadLedgerApplicationTargetSnapshot(SqlConnection cn, SqlTransaction tx, int ledgerEntryID)
        {
            using (var cmd = new SqlCommand(@"
SELECT
    BaseAmount = ISNULL(
        (
            SELECT SUM(d.Amount)
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.LedgerEntryID = @LedgerEntryID
              AND d.AppliedEntryID = 0
        ), 0),
    AppliedToEntry = ISNULL(
        (
            SELECT SUM(d.AppliedAmount)
            FROM dbo.AR_LedgerEntryDetail d
            WHERE d.AppliedEntryID = @LedgerEntryID
              AND d.AppliedEntryID > 0
        ), 0);", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return new LedgerBalanceSnapshot();

                    return new LedgerBalanceSnapshot
                    {
                        BaseAmount = reader["BaseAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["BaseAmount"]),
                        AppliedToEntry = reader["AppliedToEntry"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["AppliedToEntry"])
                    };
                }
            }
        }

        private static void SetLedgerEntryOpenState(SqlConnection cn, SqlTransaction tx, int ledgerEntryID, bool isOpen, DateTime now)
        {
            using (var cmd = new SqlCommand(@"
UPDATE dbo.AR_LedgerEntry
SET ClosingDate = CASE WHEN @Open = 1 THEN NULL ELSE ISNULL(ClosingDate, @Now) END,
    LastUpdated = @Now
WHERE ID = @LedgerEntryID;", cn))
            {
                if (tx != null)
                    cmd.Transaction = tx;

                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                cmd.Parameters.AddWithValue("@Open", isOpen ? 1 : 0);
                cmd.Parameters.AddWithValue("@Now", now);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertLedgerApplicationDetail(
            SqlConnection cn,
            SqlTransaction tx,
            int ledgerEntryID,
            int accountID,
            int ledgerType,
            DateTime postingDate,
            string reference,
            int appliedEntryID,
            decimal appliedAmount)
        {
            using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn, tx))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60;

                cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                cmd.Parameters.AddWithValue("@AccountID", accountID);
                cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                cmd.Parameters.AddWithValue("@DueDate", postingDate);
                cmd.Parameters.AddWithValue("@PostingDate", postingDate);
                cmd.Parameters.AddWithValue("@DetailType", (byte)0);
                cmd.Parameters.AddWithValue("@Reference", reference);
                cmd.Parameters.AddWithValue("@Amount", 0m);
                cmd.Parameters.AddWithValue("@AmountLCY", 0m);
                cmd.Parameters.AddWithValue("@AmountACY", 0m);
                cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                cmd.Parameters.AddWithValue("@AppliedEntryID", appliedEntryID);
                cmd.Parameters.AddWithValue("@AppliedAmount", appliedAmount);
                cmd.Parameters.AddWithValue("@UnapplyEntryID", 0);
                cmd.Parameters.AddWithValue("@UnapplyReasonID", 0);
                cmd.Parameters.AddWithValue("@ISCLOSING", false);
                cmd.ExecuteNonQuery();
            }
        }

        private static bool IsCreditTender(IEnumerable<NovaRetailSaleTenderDto> tenders, SqlConnection cn)
        {
            var list = (tenders ?? Enumerable.Empty<NovaRetailSaleTenderDto>())
                .Where(tender => tender != null)
                .OrderBy(tender => tender.RowNo)
                .Take(4)
                .ToList();

            if (list.Count == 0)
                return false;

            if (list.Any(tender => string.Equals((tender.MedioPagoCodigo ?? string.Empty).Trim(), "99", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (list.Any(tender => IsCreditDescription(tender.Description)))
                return true;

            if (list.All(tender => !string.IsNullOrWhiteSpace(tender.MedioPagoCodigo)))
                return false;

            return ResolveIntegraFastMedioPagos(cn, list)
                .Any(code => string.Equals(code, "99", StringComparison.OrdinalIgnoreCase));
        }

        private static void TryCreateARTransaction(NovaRetailCreateSaleRequest request, NovaRetailCreateSaleResponse response)
        {
            var perfAr = Stopwatch.StartNew();
            response.AccountsReceivableEntryCreated = false;
            response.AccountsReceivableApplied = false;
            response.AccountsReceivableAppliedAmount = 0m;

            if (response.TransactionNumber <= 0 ||
                (string.IsNullOrWhiteSpace(request.CodCliente) &&
                 string.IsNullOrWhiteSpace(request.CreditAccountNumber) &&
                 request.CustomerID <= 0))
                return;

            var total = Math.Abs(response.Total ?? 0m);
            if (total <= 0m)
                return;

            var isNC = !string.IsNullOrWhiteSpace(request.NC_REFERENCIA);
            var isCreditSale = string.Equals(request.CondicionVenta, "02", StringComparison.OrdinalIgnoreCase) && !isNC;
            var isCreditNC = false;

            if (!isCreditSale && !isNC)
                return;

            var connectionString = GetConnectionString();

            using (var cn = new SqlConnection(connectionString))
            {
                var perfStep = Stopwatch.StartNew();
                cn.Open();
                LogPerformance($"AR TryCreateARTransaction connection open {perfStep.ElapsedMilliseconds} ms");

                perfStep.Restart();
                NormalizeFiscalCustomerIdentity(cn, request);
                LogPerformance($"AR NormalizeFiscalCustomerIdentity {perfStep.ElapsedMilliseconds} ms");

                perfStep.Restart();
                isCreditNC = isNC && IsCreditTender(request.Tenders, cn);
                LogPerformance($"AR DetectCreditNC {perfStep.ElapsedMilliseconds} ms isCreditNC={isCreditNC}");

                if (!isCreditSale && !isCreditNC)
                    return;

                perfStep.Restart();
                var referenceCandidates = BuildCreditNoteReferenceCandidates(request);
                var customerAccountNumber = request.CreditAccountNumber;
                if (string.IsNullOrWhiteSpace(customerAccountNumber) && request.CustomerID > 0)
                {
                    using (var cmd = new SqlCommand("SELECT TOP 1 AccountNumber FROM dbo.Customer WHERE ID = @CustomerID", cn))
                    {
                        cmd.Parameters.AddWithValue("@CustomerID", request.CustomerID);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(result)))
                            customerAccountNumber = Convert.ToString(result);
                    }
                }
                LogPerformance($"AR Resolve customer account seed {perfStep.ElapsedMilliseconds} ms account={customerAccountNumber ?? string.Empty}");

                if (string.IsNullOrWhiteSpace(customerAccountNumber) && isCreditNC)
                {
                    perfStep.Restart();
                    customerAccountNumber = ResolveReferencedLedgerAccountNumber(cn, referenceCandidates);
                    LogPerformance($"AR ResolveReferencedLedgerAccountNumber {perfStep.ElapsedMilliseconds} ms account={customerAccountNumber ?? string.Empty}");
                }

                if (string.IsNullOrWhiteSpace(customerAccountNumber))
                    customerAccountNumber = request.CodCliente;

                if (string.IsNullOrWhiteSpace(customerAccountNumber))
                    return;

                var accountID = 0;
                var customerID = request.CustomerID;
                perfStep.Restart();
                using (var cmd = new SqlCommand(@"
SELECT TOP 1
    a.ID AS AccountID,
    ISNULL(c.ID, 0) AS CustomerID
FROM dbo.AR_Account a
LEFT JOIN dbo.Customer c ON c.AccountNumber = a.Number
WHERE a.Number = @Number;", cn))
                {
                    cmd.Parameters.AddWithValue("@Number", customerAccountNumber);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            accountID = reader["AccountID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AccountID"]);
                            if (customerID <= 0)
                                customerID = reader["CustomerID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomerID"]);
                        }
                    }
                }
                LogPerformance($"AR Account lookup {perfStep.ElapsedMilliseconds} ms accountID={accountID} customerID={customerID}");

                if (accountID <= 0 && isCreditNC)
                {
                    perfStep.Restart();
                    var referencedAccountNumber = ResolveReferencedLedgerAccountNumber(cn, referenceCandidates);
                    LogPerformance($"AR ResolveReferencedLedgerAccountNumber fallback {perfStep.ElapsedMilliseconds} ms account={referencedAccountNumber ?? string.Empty}");
                    if (!string.IsNullOrWhiteSpace(referencedAccountNumber) &&
                        !string.Equals(referencedAccountNumber, customerAccountNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        customerAccountNumber = referencedAccountNumber;
                        using (var cmd = new SqlCommand(@"
SELECT TOP 1
    a.ID AS AccountID,
    ISNULL(c.ID, 0) AS CustomerID
FROM dbo.AR_Account a
LEFT JOIN dbo.Customer c ON c.AccountNumber = a.Number
WHERE a.Number = @Number;", cn))
                        {
                            cmd.Parameters.AddWithValue("@Number", customerAccountNumber);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    accountID = reader["AccountID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["AccountID"]);
                                    if (customerID <= 0)
                                        customerID = reader["CustomerID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomerID"]);
                                }
                            }
                        }
                    }
                }

                if (accountID <= 0)
                    return;

                using (var tx = cn.BeginTransaction())
                {
                    var now = DateTime.Now;
                    var dueDate = now.AddDays(30);
                    var reference = BuildLedgerReference(response.TransactionNumber.ToString(CultureInfo.InvariantCulture));

                    perfStep.Restart();
                    total = LoadPersistedTransactionTotal(cn, tx, response.TransactionNumber, total);
                    LogPerformance($"AR LoadPersistedTransactionTotal {perfStep.ElapsedMilliseconds} ms total={total:N2}");
                    if (total <= 0m)
                    {
                        tx.Rollback();
                        return;
                    }

                    const byte documentType = 3;
                    var ledgerType = (byte)(isCreditSale ? 3 : 4);
                    var amountACY = isCreditSale ? total : -total;

                    var ledgerEntryID = 0;
                    perfStep.Restart();
                    using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRY_INSERT", cn, tx))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 60;

                        cmd.Parameters.AddWithValue("@LastUpdated", now);
                        cmd.Parameters.AddWithValue("@AccountID", accountID);
                        cmd.Parameters.AddWithValue("@CustomerID", customerID);
                        cmd.Parameters.AddWithValue("@StoreID", request.StoreID);
                        cmd.Parameters.AddWithValue("@LinkType", (byte)1);
                        cmd.Parameters.AddWithValue("@LinkID", customerID);
                        cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                        cmd.Parameters.AddWithValue("@DocumentType", documentType);
                        cmd.Parameters.AddWithValue("@DocumentID", response.TransactionNumber);
                        cmd.Parameters.AddWithValue("@PostingDate", now);
                        cmd.Parameters.AddWithValue("@DueDate", dueDate);
                        cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                        cmd.Parameters.AddWithValue("@Reference", reference);
                        cmd.Parameters.AddWithValue("@Description", isCreditSale ? "Venta a crédito" : "Nota de crédito");
                        cmd.Parameters.AddWithValue("@CurrencyID", 0);
                        cmd.Parameters.AddWithValue("@CurrencyFactor", 1.0);
                        cmd.Parameters.AddWithValue("@Positive", true);
                        cmd.Parameters.AddWithValue("@ClosingDate", DBNull.Value);
                        cmd.Parameters.AddWithValue("@ReasonID", 0);
                        cmd.Parameters.AddWithValue("@HoldReasonID", 0);
                        cmd.Parameters.AddWithValue("@UndoReasonID", 0);
                        cmd.Parameters.AddWithValue("@PayMethodID", 0);
                        cmd.Parameters.AddWithValue("@TransactionID", 0);
                        var appliedReference = isCreditNC
                            ? TruncateLedgerExternalReference(referenceCandidates.FirstOrDefault())
                            : response.TransactionNumber.ToString(CultureInfo.InvariantCulture);
                        cmd.Parameters.AddWithValue("@ExtReference", appliedReference);
                        cmd.Parameters.AddWithValue("@Comment", isCreditNC ? ("NC aplicada a " + BuildLedgerReference(appliedReference)) : string.Empty);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                                ledgerEntryID = Convert.ToInt32(reader["ID"]);
                        }
                    }
                    LogPerformance($"AR OFF_AR_LEDGERENTRY_INSERT {perfStep.ElapsedMilliseconds} ms ledgerEntryID={ledgerEntryID}");

                    if (ledgerEntryID <= 0)
                    {
                        tx.Rollback();
                        return;
                    }

                    response.AccountsReceivableEntryCreated = true;

                    perfStep.Restart();
                    using (var cmd = new SqlCommand("dbo.OFF_AR_LEDGERENTRYDETAIL_INSERT", cn, tx))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 60;

                        cmd.Parameters.AddWithValue("@LedgerEntryID", ledgerEntryID);
                        cmd.Parameters.AddWithValue("@AccountID", accountID);
                        cmd.Parameters.AddWithValue("@LedgerType", ledgerType);
                        cmd.Parameters.AddWithValue("@DueDate", dueDate);
                        cmd.Parameters.AddWithValue("@PostingDate", now);
                        cmd.Parameters.AddWithValue("@DetailType", (byte)0);
                        cmd.Parameters.AddWithValue("@Reference", reference);
                        cmd.Parameters.AddWithValue("@Amount", amountACY);
                        cmd.Parameters.AddWithValue("@AmountLCY", amountACY);
                        cmd.Parameters.AddWithValue("@AmountACY", amountACY);
                        cmd.Parameters.AddWithValue("@AuditEntryID", 0);
                        cmd.Parameters.AddWithValue("@AppliedEntryID", 0);
                        cmd.Parameters.AddWithValue("@AppliedAmount", 0m);
                        cmd.Parameters.AddWithValue("@UnapplyEntryID", 0);
                        cmd.Parameters.AddWithValue("@UnapplyReasonID", 0);
                        cmd.Parameters.AddWithValue("@ISCLOSING", false);
                        cmd.ExecuteNonQuery();
                    }
                    LogPerformance($"AR OFF_AR_LEDGERENTRYDETAIL_INSERT {perfStep.ElapsedMilliseconds} ms");

                    if (isCreditNC)
                    {
                        perfStep.Restart();
                        var referencedLedgerEntryID = ResolveReferencedLedgerEntryID(cn, tx, accountID, referenceCandidates);
                        LogPerformance($"AR ResolveReferencedLedgerEntryID {perfStep.ElapsedMilliseconds} ms referencedLedgerEntryID={referencedLedgerEntryID}");
                        if (referencedLedgerEntryID > 0)
                        {
                            perfStep.Restart();
                            var targetSnapshot = LoadLedgerApplicationTargetSnapshot(cn, tx, referencedLedgerEntryID);
                            var sourceRemaining = Math.Abs(amountACY);
                            var targetBalance = Math.Max(0m, targetSnapshot.BaseAmount + targetSnapshot.AppliedToEntry);
                            var amountToApply = Math.Min(sourceRemaining, targetBalance);
                            LogPerformance($"AR LoadLedgerTargetBalance {perfStep.ElapsedMilliseconds} ms amountToApply={amountToApply:N2}");

                            if (amountToApply > LedgerClosingTolerance)
                            {
                                perfStep.Restart();
                                InsertLedgerApplicationDetail(
                                    cn,
                                    tx,
                                    ledgerEntryID,
                                    accountID,
                                    ledgerType,
                                    now,
                                    reference,
                                    referencedLedgerEntryID,
                                    -amountToApply);

                                var sourceRemainingAfterApply = Math.Max(0m, sourceRemaining - amountToApply);
                                var targetBalanceAfterApply = Math.Max(0m, targetBalance - amountToApply);
                                SetLedgerEntryOpenState(cn, tx, ledgerEntryID, sourceRemainingAfterApply > LedgerClosingTolerance, now);
                                SetLedgerEntryOpenState(cn, tx, referencedLedgerEntryID, targetBalanceAfterApply > LedgerClosingTolerance, now);
                                LogPerformance($"AR InsertLedgerApplicationDetail+Refresh {perfStep.ElapsedMilliseconds} ms");
                                response.AccountsReceivableApplied = true;
                                response.AccountsReceivableAppliedAmount = Math.Round(amountToApply, 2, MidpointRounding.AwayFromZero);
                            }
                        }
                    }

                    tx.Commit();
                }
            }

            LogPerformance($"AR TryCreateARTransaction finished {perfAr.ElapsedMilliseconds} ms created={response.AccountsReceivableEntryCreated} applied={response.AccountsReceivableApplied}");
        }
    }
}

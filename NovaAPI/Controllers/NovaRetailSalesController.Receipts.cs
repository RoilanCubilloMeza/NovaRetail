using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
        private static NovaRetailInvoiceHistoryEntryDto LoadSaleReceipt(SqlConnection cn, int transactionNumber)
        {
            var entry = LoadSaleReceiptHeader(cn, transactionNumber);
            if (entry == null)
                return null;

            LoadSaleReceiptFiscalHeader(cn, entry);
            LoadSaleReceiptPaymentsAndRegister(cn, entry);
            LoadSaleReceiptDetails(cn, entry);

            // AVS_RECEIPT_SALE_DETAILS uses an INNER JOIN to TransactionEntryExt.
            // Older sales without that extension still need printable detail.
            if (entry.Lines.Count == 0)
                LoadSaleReceiptDetailsFallback(cn, entry);

            LoadSaleReceiptTaxes(cn, entry);

            if (entry.SubtotalColones == 0m && entry.Lines.Count > 0)
                entry.SubtotalColones = ReceiptRoundMoney(entry.Lines.Sum(line => line.FullPriceColones * line.Quantity));

            if (entry.TaxColones == 0m && entry.TaxBreakdowns.Count > 0)
                entry.TaxColones = ReceiptRoundMoney(entry.TaxBreakdowns.Sum(tax => tax.TaxAmount));

            if (entry.TenderTotalColones == 0m)
                entry.TenderTotalColones = entry.TotalColones;

            return entry;
        }

        private static void LoadSaleReceiptPaymentsAndRegister(SqlConnection cn, NovaRetailInvoiceHistoryEntryDto entry)
        {
            const string registerSql = @"
SELECT TOP 1 ISNULL(b.RegisterID, 0)
FROM dbo.[Transaction] t
LEFT JOIN dbo.Batch b ON b.BatchNumber = t.BatchNumber AND b.StoreID = t.StoreID
WHERE t.TransactionNumber = @TransactionNumber";

            using (var cmd = new SqlCommand(registerSql, cn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@TransactionNumber", SqlDbType.Int).Value = entry.TransactionNumber;
                var value = cmd.ExecuteScalar();
                if (value != null && value != DBNull.Value)
                    entry.RegisterNumber = Convert.ToInt32(value);
            }

            const string paymentSql = @"
SELECT TOP 2
    COALESCE(NULLIF(te.Description, ''), NULLIF(tn.Description, ''), 'Pago') AS TenderDescription,
    CAST(ISNULL(te.Amount, 0) AS decimal(18, 4)) AS Amount
FROM dbo.TenderEntry te
LEFT JOIN dbo.Tender tn ON tn.ID = te.TenderID
WHERE te.TransactionNumber = @TransactionNumber
ORDER BY te.ID";

            using (var cmd = new SqlCommand(paymentSql, cn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@TransactionNumber", SqlDbType.Int).Value = entry.TransactionNumber;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        entry.TenderDescription = ReceiptFirstNotBlank(
                            ReceiptReadString(reader, "TenderDescription"),
                            entry.TenderDescription);
                        entry.TenderTotalColones = ReceiptRoundMoney(ReceiptReadDecimal(reader, "Amount"));
                    }

                    if (reader.Read())
                    {
                        entry.SecondTenderDescription = ReceiptReadString(reader, "TenderDescription");
                        entry.SecondTenderAmountColones = ReceiptRoundMoney(ReceiptReadDecimal(reader, "Amount"));
                    }
                }
            }
        }

        private static NovaRetailInvoiceHistoryEntryDto LoadSaleReceiptHeader(SqlConnection cn, int transactionNumber)
        {
            using (var cmd = CreateReceiptProcedureCommand(cn, "dbo.AVS_RECEIPT_SALE_HEADER", "@TransactionNumber", transactionNumber))
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read())
                    return null;

                var customerName = string.Join(" ", new[]
                {
                    ReceiptReadString(reader, "CustomerFirsName"),
                    ReceiptReadString(reader, "CustomerLastName")
                }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();

                var storeAddress = string.Join(", ", new[]
                {
                    ReceiptReadString(reader, "StoreAddress1"),
                    ReceiptReadString(reader, "StoreAddress2")
                }.Where(value => !string.IsNullOrWhiteSpace(value)));

                return new NovaRetailInvoiceHistoryEntryDto
                {
                    TransactionNumber = ReceiptReadInt(reader, "TransactionNumber", transactionNumber),
                    Date = ReceiptReadDateTime(reader, "Time", DateTime.Now),
                    ClientId = ReceiptFirstNotBlank(
                        ReceiptReadString(reader, "CustomerTaxNumber"),
                        ReceiptReadString(reader, "Customer")),
                    ClientName = ReceiptFirstNotBlank(
                        customerName,
                        ReceiptReadString(reader, "CustomerCompany"),
                        "CLIENTE CONTADO"),
                    ClientEmail = ReceiptReadString(reader, "CustomerEmailAddres"),
                    CashierName = ReceiptReadString(reader, "CashierName"),
                    RegisterNumber = ReceiptReadInt(reader, "StoreID", 1),
                    StoreName = ReceiptReadString(reader, "StoreName"),
                    StoreAddress = storeAddress,
                    StorePhone = ReceiptReadString(reader, "StorePhone"),
                    TaxColones = ReceiptRoundMoney(ReceiptReadDecimal(reader, "SalesTax")),
                    TotalColones = ReceiptRoundMoney(ReceiptReadDecimal(reader, "Total")),
                    CreditAccountNumber = ReceiptReadString(reader, "Customer")
                };
            }
        }

        private static void LoadSaleReceiptFiscalHeader(SqlConnection cn, NovaRetailInvoiceHistoryEntryDto entry)
        {
            using (var cmd = CreateReceiptProcedureCommand(cn, "dbo.AVS_RECEIPT_SALE_INTEGRAFAST_HEADER", "@TRNumber", entry.TransactionNumber))
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read())
                    return;

                entry.Clave50 = ReceiptReadString(reader, "CLAVE50");
                entry.Consecutivo = ReceiptFirstNotBlank(
                    ReceiptReadString(reader, "CLAVE20"),
                    ReceiptReadString(reader, "COMPROBANTE_INTERNO"));
                entry.ComprobanteTipo = ResolveReceiptDocumentTypeCode(ReceiptReadString(reader, "COMPROBANTE_TIPO"));
                entry.Date = ReceiptReadDateTime(reader, "FECHA_TRANSAC", entry.Date);
                entry.CurrencyCode = ReceiptFirstNotBlank(ReceiptReadString(reader, "COD_MONEDA"), "CRC").ToUpperInvariant();
                entry.ClientId = ReceiptFirstNotBlank(
                    ReceiptReadString(reader, "CustomerTaxNumber"),
                    ReceiptReadString(reader, "COD_CLIENTE"),
                    entry.ClientId);
                entry.ClientName = ReceiptFirstNotBlank(
                    ReceiptReadString(reader, "NOMBRE_CLIENTE"),
                    ReceiptReadString(reader, "CustomerFullName"),
                    ReceiptReadString(reader, "CustomerCompany"),
                    entry.ClientName);
                entry.ClientEmail = ReceiptFirstNotBlank(ReceiptReadString(reader, "EMAIL"), entry.ClientEmail);
                entry.CreditAccountNumber = ReceiptFirstNotBlank(
                    ReceiptReadString(reader, "CustomerAccountNumber"),
                    entry.CreditAccountNumber);
                entry.TenderDescription = ResolveReceiptTenderDescription(ReceiptReadString(reader, "MEDIO_PAGO1"));
                entry.SecondTenderDescription = ResolveReceiptTenderDescription(ReceiptReadString(reader, "MEDIO_PAGO2"));
            }
        }

        private static void LoadSaleReceiptDetails(SqlConnection cn, NovaRetailInvoiceHistoryEntryDto entry)
        {
            decimal grossSubtotal = 0m;
            decimal discountTotal = 0m;

            using (var cmd = CreateReceiptProcedureCommand(cn, "dbo.AVS_RECEIPT_SALE_DETAILS", "@TransactionNumber", entry.TransactionNumber))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var quantity = ReceiptReadDecimal(reader, "Quantity");
                    var fullPrice = ReceiptReadDecimal(reader, "ItemPrice");
                    var unitPrice = ReceiptReadDecimal(reader, "UnitPrice");
                    var lineSubtotal = ReceiptReadDecimal(reader, "SubTotal", unitPrice * quantity);
                    var taxAmount = ReceiptReadDecimal(reader, "IVA");
                    var discountPercent = NormalizeReceiptDiscountPercentage(ReceiptReadDecimal(reader, "Discount"));

                    grossSubtotal += fullPrice * quantity;
                    discountTotal += Math.Max(0m, (fullPrice * quantity) - lineSubtotal);

                    entry.Lines.Add(new NovaRetailInvoiceHistoryLineDto
                    {
                        LineNumber = entry.Lines.Count + 1,
                        TaxID = ReceiptReadInt(reader, "TaxID"),
                        DisplayName = ReceiptFirstNotBlank(
                            ReceiptReadString(reader, "ItemDescription"),
                            ReceiptReadString(reader, "Comment"),
                            "Articulo"),
                        Code = ReceiptReadString(reader, "ItemLookupCode"),
                        Quantity = quantity,
                        TaxPercentage = ReceiptReadDecimal(reader, "TaxPercentage"),
                        FullPriceColones = ReceiptRoundMoney(fullPrice),
                        UnitPriceColones = ReceiptRoundMoney(unitPrice),
                        LineTotalColones = ReceiptRoundMoney(lineSubtotal),
                        TaxAmountColones = ReceiptRoundMoney(taxAmount),
                        HasDiscount = discountPercent > 0m || fullPrice > unitPrice,
                        DiscountPercent = discountPercent > 0m
                            ? discountPercent
                            : CalculateReceiptDiscountPercentage(fullPrice, unitPrice),
                        HasOverridePrice = fullPrice > unitPrice
                    });
                }
            }

            if (entry.Lines.Count > 0)
            {
                entry.SubtotalColones = ReceiptRoundMoney(grossSubtotal);
                entry.DiscountColones = ReceiptRoundMoney(discountTotal);
            }
        }

        private static void LoadSaleReceiptDetailsFallback(SqlConnection cn, NovaRetailInvoiceHistoryEntryDto entry)
        {
            const string sql = @"
SELECT
    ROW_NUMBER() OVER (ORDER BY te.ID) AS LineNumber,
    ISNULL(te.ItemID, 0) AS ItemID,
    ISNULL(tx.TaxID, ISNULL(i.TaxID, 0)) AS TaxID,
    COALESCE(
        NULLIF(xte.XmlData.value('(ExtendedProperties/Property[@Name=""ExtendedDescription""])[1]', 'nvarchar(max)'), ''),
        NULLIF(i.Description, ''),
        NULLIF(te.Comment, ''),
        'Articulo') AS DisplayName,
    ISNULL(NULLIF(i.ItemLookupCode, ''), CAST(te.ItemID AS NVARCHAR(50))) AS Code,
    CAST(ISNULL(te.Quantity, 0) AS decimal(18, 4)) AS Quantity,
    CAST(ISNULL(tax.Percentage, 0) AS decimal(18, 4)) AS TaxPercentage,
    CAST(ISNULL(te.FullPrice, te.Price) AS decimal(18, 4)) AS FullPrice,
    CAST(ISNULL(te.Price, 0) AS decimal(18, 4)) AS UnitPriceColones,
    CAST(ISNULL(tx.TaxableAmount, ISNULL(te.Price, 0) * ISNULL(te.Quantity, 0)) AS decimal(18, 4)) AS LineTotalColones,
    CAST(ISNULL(tx.Tax, 0) AS decimal(18, 4)) AS TaxAmountColones
FROM dbo.TransactionEntry te
LEFT JOIN dbo.TransactionEntryExt xte ON xte.EntryId = te.ID
LEFT JOIN dbo.Item i ON i.ID = te.ItemID
LEFT JOIN dbo.TaxEntry tx ON tx.TransactionEntryID = te.ID AND tx.TransactionNumber = te.TransactionNumber
LEFT JOIN dbo.Tax tax ON tax.ID = ISNULL(tx.TaxID, i.TaxID)
WHERE te.TransactionNumber = @TransactionNumber
ORDER BY te.ID";

            decimal grossSubtotal = 0m;
            decimal discountTotal = 0m;

            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.Add("@TransactionNumber", SqlDbType.Int).Value = entry.TransactionNumber;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var quantity = ReceiptReadDecimal(reader, "Quantity");
                        var fullPrice = ReceiptReadDecimal(reader, "FullPrice");
                        var unitPrice = ReceiptReadDecimal(reader, "UnitPriceColones");
                        var lineTotal = ReceiptReadDecimal(reader, "LineTotalColones", unitPrice * quantity);
                        var discountPercent = CalculateReceiptDiscountPercentage(fullPrice, unitPrice);

                        grossSubtotal += fullPrice * quantity;
                        discountTotal += Math.Max(0m, (fullPrice * quantity) - lineTotal);

                        entry.Lines.Add(new NovaRetailInvoiceHistoryLineDto
                        {
                            LineNumber = ReceiptReadInt(reader, "LineNumber", entry.Lines.Count + 1),
                            ItemID = ReceiptReadInt(reader, "ItemID"),
                            TaxID = ReceiptReadInt(reader, "TaxID"),
                            DisplayName = ReceiptReadString(reader, "DisplayName"),
                            Code = ReceiptReadString(reader, "Code"),
                            Quantity = quantity,
                            TaxPercentage = ReceiptReadDecimal(reader, "TaxPercentage"),
                            FullPriceColones = ReceiptRoundMoney(fullPrice),
                            UnitPriceColones = ReceiptRoundMoney(unitPrice),
                            LineTotalColones = ReceiptRoundMoney(lineTotal),
                            TaxAmountColones = ReceiptRoundMoney(ReceiptReadDecimal(reader, "TaxAmountColones")),
                            HasDiscount = discountPercent > 0m,
                            DiscountPercent = discountPercent,
                            HasOverridePrice = fullPrice > unitPrice
                        });
                    }
                }
            }

            if (entry.Lines.Count > 0)
            {
                entry.SubtotalColones = ReceiptRoundMoney(grossSubtotal);
                entry.DiscountColones = ReceiptRoundMoney(discountTotal);
            }
        }

        private static void LoadSaleReceiptTaxes(SqlConnection cn, NovaRetailInvoiceHistoryEntryDto entry)
        {
            using (var cmd = CreateReceiptProcedureCommand(cn, "dbo.AVS_RECEIPT_SALE_TAXES", "@TransactionNumber", entry.TransactionNumber))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var description = ReceiptReadString(reader, "TaxDescription");
                    entry.TaxBreakdowns.Add(new NovaRetailReceiptTaxDto
                    {
                        Description = ReceiptFirstNotBlank(description, "Impuesto"),
                        Percentage = ParseReceiptPercentage(description),
                        TaxAmount = ReceiptRoundMoney(ReceiptReadDecimal(reader, "Tax"))
                    });
                }
            }

            if (entry.TaxBreakdowns.Count > 0)
                entry.TaxColones = ReceiptRoundMoney(entry.TaxBreakdowns.Sum(tax => tax.TaxAmount));
        }

        private static SqlCommand CreateReceiptProcedureCommand(SqlConnection cn, string procedureName, string parameterName, int transactionNumber)
        {
            var cmd = new SqlCommand(procedureName, cn)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 60
            };
            cmd.Parameters.Add(parameterName, SqlDbType.Int).Value = transactionNumber;
            return cmd;
        }

        private static string ResolveReceiptDocumentTypeCode(string description)
        {
            var value = (description ?? string.Empty).Trim().ToUpperInvariant();
            if (value == "01" || value.Contains("FACTURA ELECTR"))
                return "01";
            if (value == "02" || value.Contains("DEBITO") || value.Contains("DÉBITO"))
                return "02";
            if (value == "03" || value.Contains("CREDITO") || value.Contains("CRÉDITO"))
                return "03";
            if (value == "04" || value.Contains("TIQUETE"))
                return "04";
            if (value == "09" || value.Contains("EXPORTACION") || value.Contains("EXPORTACIÓN"))
                return "09";
            return "04";
        }

        private static string ResolveReceiptTenderDescription(string code)
        {
            switch ((code ?? string.Empty).Trim())
            {
                case "01": return "Efectivo";
                case "02": return "Tarjeta";
                case "03": return "Cheque";
                case "04": return "Transferencia / deposito bancario";
                case "05": return "Recaudado por terceros";
                case "06": return "SINPE Movil";
                case "07": return "Plataforma digital";
                case "99": return "Otros";
                default: return string.Empty;
            }
        }

        private static decimal NormalizeReceiptDiscountPercentage(decimal value)
        {
            var percentage = value > 0m && value <= 1m ? value * 100m : value;
            return ReceiptRoundMoney(Math.Max(0m, percentage));
        }

        private static decimal CalculateReceiptDiscountPercentage(decimal fullPrice, decimal unitPrice)
        {
            if (fullPrice <= 0m || fullPrice <= unitPrice)
                return 0m;
            return ReceiptRoundMoney(((fullPrice - unitPrice) / fullPrice) * 100m);
        }

        private static decimal ParseReceiptPercentage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            var digits = new string(value.Where(character => char.IsDigit(character) || character == '.' || character == ',').ToArray());
            decimal percentage;
            if (decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out percentage))
                return percentage;
            if (decimal.TryParse(digits, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CR"), out percentage))
                return percentage;
            return 0m;
        }

        private static decimal ReceiptRoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string ReceiptFirstNotBlank(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static int ReceiptFindOrdinal(SqlDataReader reader, string columnName)
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (string.Equals(reader.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        private static string ReceiptReadString(SqlDataReader reader, string columnName)
        {
            var ordinal = ReceiptFindOrdinal(reader, columnName);
            return ordinal < 0 || reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)).Trim();
        }

        private static int ReceiptReadInt(SqlDataReader reader, string columnName, int fallback = 0)
        {
            var ordinal = ReceiptFindOrdinal(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;

            int value;
            return int.TryParse(Convert.ToString(reader.GetValue(ordinal)), out value) ? value : fallback;
        }

        private static decimal ReceiptReadDecimal(SqlDataReader reader, string columnName, decimal fallback = 0m)
        {
            var ordinal = ReceiptFindOrdinal(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;

            try
            {
                return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static DateTime ReceiptReadDateTime(SqlDataReader reader, string columnName, DateTime fallback)
        {
            var ordinal = ReceiptFindOrdinal(reader, columnName);
            if (ordinal < 0 || reader.IsDBNull(ordinal))
                return fallback;

            DateTime value;
            return DateTime.TryParse(Convert.ToString(reader.GetValue(ordinal)), out value) ? value : fallback;
        }
    }
}

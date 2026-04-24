using System;
using System.Collections.Generic;
using System.Data;
using NovaAPI.Models;

namespace NovaAPI.Services
{
    internal static class NovaRetailSalesSqlMapper
    {
        public static DataTable ToItemsTable(IEnumerable<NovaRetailSaleItemDto> items)
        {
            var dt = new DataTable();
            dt.Columns.Add("RowNo", typeof(int));
            dt.Columns.Add("ItemID", typeof(int));
            dt.Columns.Add("Quantity", typeof(decimal));
            dt.Columns.Add("UnitPrice", typeof(decimal));
            dt.Columns.Add("FullPrice", typeof(decimal));
            dt.Columns.Add("Cost", typeof(decimal));
            dt.Columns.Add("Commission", typeof(decimal));
            dt.Columns.Add("PriceSource", typeof(int));
            dt.Columns.Add("SalesRepID", typeof(int));
            dt.Columns.Add("Taxable", typeof(bool));
            dt.Columns.Add("TaxID", typeof(int));
            dt.Columns.Add("SalesTax", typeof(decimal));
            dt.Columns.Add("LineComment", typeof(string));
            dt.Columns.Add("DiscountReasonCodeID", typeof(int));
            dt.Columns.Add("ReturnReasonCodeID", typeof(int));
            dt.Columns.Add("TaxChangeReasonCodeID", typeof(int));
            dt.Columns.Add("QuantityDiscountID", typeof(int));
            dt.Columns.Add("ItemType", typeof(int));
            dt.Columns.Add("ComputedQuantity", typeof(decimal));
            dt.Columns.Add("IsAddMoney", typeof(bool));
            dt.Columns.Add("VoucherID", typeof(int));
            dt.Columns.Add("ExtendedDescription", typeof(string));
            dt.Columns.Add("PromotionID", typeof(int));
            dt.Columns.Add("PromotionName", typeof(string));
            dt.Columns.Add("LineDiscountAmount", typeof(decimal));
            dt.Columns.Add("LineDiscountPercent", typeof(decimal));

            foreach (var item in items)
            {
                dt.Rows.Add(
                    item.RowNo,
                    item.ItemID,
                    item.Quantity,
                    item.UnitPrice,
                    item.FullPrice.HasValue ? (object)item.FullPrice.Value : DBNull.Value,
                    item.Cost,
                    item.Commission,
                    item.PriceSource,
                    item.SalesRepID,
                    item.Taxable,
                    item.TaxID.HasValue ? (object)item.TaxID.Value : DBNull.Value,
                    item.SalesTax,
                    item.LineComment ?? string.Empty,
                    item.DiscountReasonCodeID,
                    item.ReturnReasonCodeID,
                    item.TaxChangeReasonCodeID,
                    item.QuantityDiscountID,
                    item.ItemType,
                    item.ComputedQuantity,
                    item.IsAddMoney,
                    item.VoucherID,
                    string.IsNullOrWhiteSpace(item.ExtendedDescription) ? (object)DBNull.Value : item.ExtendedDescription,
                    item.PromotionID.HasValue ? (object)item.PromotionID.Value : DBNull.Value,
                    string.IsNullOrWhiteSpace(item.PromotionName) ? (object)DBNull.Value : item.PromotionName,
                    item.LineDiscountAmount,
                    item.LineDiscountPercent);
            }

            return dt;
        }

        public static DataTable ToTendersTable(IEnumerable<NovaRetailSaleTenderDto> tenders)
        {
            var dt = new DataTable();
            dt.Columns.Add("RowNo", typeof(int));
            dt.Columns.Add("TenderID", typeof(int));
            dt.Columns.Add("PaymentID", typeof(int));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("Amount", typeof(decimal));
            dt.Columns.Add("AmountForeign", typeof(decimal));
            dt.Columns.Add("RoundingError", typeof(decimal));
            dt.Columns.Add("CreditCardExpiration", typeof(string));
            dt.Columns.Add("CreditCardNumber", typeof(string));
            dt.Columns.Add("CreditCardApprovalCode", typeof(string));
            dt.Columns.Add("AccountHolder", typeof(string));
            dt.Columns.Add("BankNumber", typeof(string));
            dt.Columns.Add("SerialNumber", typeof(string));
            dt.Columns.Add("State", typeof(string));
            dt.Columns.Add("License", typeof(string));
            dt.Columns.Add("BirthDate", typeof(DateTime));
            dt.Columns.Add("TransitNumber", typeof(string));
            dt.Columns.Add("VisaNetAuthorizationID", typeof(int));
            dt.Columns.Add("DebitSurcharge", typeof(decimal));
            dt.Columns.Add("CashBackSurcharge", typeof(decimal));
            dt.Columns.Add("IsCreateNew", typeof(bool));
            dt.Columns.Add("MedioPagoCodigo", typeof(string));

            foreach (var tender in tenders)
            {
                dt.Rows.Add(
                    tender.RowNo,
                    tender.TenderID,
                    tender.PaymentID,
                    tender.Description ?? string.Empty,
                    tender.Amount,
                    tender.AmountForeign.HasValue ? (object)tender.AmountForeign.Value : DBNull.Value,
                    tender.RoundingError,
                    string.IsNullOrWhiteSpace(tender.CreditCardExpiration) ? (object)DBNull.Value : tender.CreditCardExpiration,
                    string.IsNullOrWhiteSpace(tender.CreditCardNumber) ? (object)DBNull.Value : tender.CreditCardNumber,
                    string.IsNullOrWhiteSpace(tender.CreditCardApprovalCode) ? (object)DBNull.Value : tender.CreditCardApprovalCode,
                    string.IsNullOrWhiteSpace(tender.AccountHolder) ? (object)DBNull.Value : tender.AccountHolder,
                    string.IsNullOrWhiteSpace(tender.BankNumber) ? (object)DBNull.Value : tender.BankNumber,
                    string.IsNullOrWhiteSpace(tender.SerialNumber) ? (object)DBNull.Value : tender.SerialNumber,
                    string.IsNullOrWhiteSpace(tender.State) ? (object)DBNull.Value : tender.State,
                    string.IsNullOrWhiteSpace(tender.License) ? (object)DBNull.Value : tender.License,
                    tender.BirthDate.HasValue ? (object)tender.BirthDate.Value : DBNull.Value,
                    string.IsNullOrWhiteSpace(tender.TransitNumber) ? (object)DBNull.Value : tender.TransitNumber,
                    tender.VisaNetAuthorizationID,
                    tender.DebitSurcharge,
                    tender.CashBackSurcharge,
                    tender.IsCreateNew,
                    string.IsNullOrWhiteSpace(tender.MedioPagoCodigo) ? (object)DBNull.Value : tender.MedioPagoCodigo);
            }

            return dt;
        }

        public static string GetString(IDataRecord reader, string columnName, string defaultValue)
        {
            if (!HasColumn(reader, columnName))
                return defaultValue;

            var value = reader[columnName];
            return value == DBNull.Value ? defaultValue : Convert.ToString(value);
        }

        public static int GetInt(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return 0;

            var value = reader[columnName];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        public static int? GetNullableInt(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return null;

            var value = reader[columnName];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        public static decimal? GetNullableDecimal(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return null;

            var value = reader[columnName];
            return value == DBNull.Value ? (decimal?)null : Convert.ToDecimal(value);
        }

        public static bool GetBoolean(IDataRecord reader, string columnName)
        {
            if (!HasColumn(reader, columnName))
                return false;

            var value = reader[columnName];
            return value != DBNull.Value && Convert.ToBoolean(value);
        }

        private static bool HasColumn(IDataRecord reader, string columnName)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}

using System;
using System.Data;
using System.Data.SqlClient;

namespace NovaAPI.Services
{
    internal static class NovaRetailAuditLogger
    {
        public static bool TableExists(SqlConnection cn, SqlTransaction tx = null)
        {
            using (var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.NovaRetail_ActionLog', N'U') IS NULL THEN 0 ELSE 1 END", cn))
            {
                cmd.Transaction = tx;
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        public static void Log(
            SqlConnection cn,
            string actionType,
            string entityType,
            int entityID,
            int cashierID,
            int storeID,
            int registerID,
            decimal amount,
            string detail,
            SqlTransaction tx = null)
        {
            if (cn == null || string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(entityType))
                return;

            try
            {
                if (!TableExists(cn, tx))
                    return;

                var cashierName = ResolveCashierName(cn, tx, cashierID);

                using (var cmd = new SqlCommand(@"
INSERT INTO dbo.NovaRetail_ActionLog
    (ActionDate, ActionType, EntityType, EntityID, CashierID, CashierName, StoreID, RegisterID, Amount, Detail)
VALUES
    (GETDATE(), @ActionType, @EntityType, @EntityID, @CashierID, @CashierName, @StoreID, @RegisterID, @Amount, @Detail);", cn))
                {
                    cmd.Transaction = tx;
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.Add("@ActionType", SqlDbType.NVarChar, 40).Value = Truncate(actionType, 40);
                    cmd.Parameters.Add("@EntityType", SqlDbType.NVarChar, 40).Value = Truncate(entityType, 40);
                    cmd.Parameters.AddWithValue("@EntityID", entityID);
                    cmd.Parameters.AddWithValue("@CashierID", cashierID);
                    cmd.Parameters.Add("@CashierName", SqlDbType.NVarChar, 120).Value = Truncate(cashierName, 120);
                    cmd.Parameters.AddWithValue("@StoreID", storeID);
                    cmd.Parameters.AddWithValue("@RegisterID", registerID);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.Add("@Detail", SqlDbType.NVarChar, 1000).Value = Truncate(detail ?? string.Empty, 1000);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Auditoria no debe bloquear ventas, abonos ni cierres operativos.
            }
        }

        private static string ResolveCashierName(SqlConnection cn, SqlTransaction tx, int cashierID)
        {
            if (cashierID <= 0)
                return string.Empty;

            using (var cmd = new SqlCommand("SELECT TOP 1 ISNULL(Name, '') FROM dbo.Cashier WHERE ID = @CashierID", cn))
            {
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("@CashierID", cashierID);
                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result);
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            value = value ?? string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}

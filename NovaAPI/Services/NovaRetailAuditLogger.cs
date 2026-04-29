using System;
using System.Data;
using System.Data.SqlClient;

namespace NovaAPI.Services
{
    internal static class NovaRetailAuditLogger
    {
        private static readonly string ErrorLogPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nova_audit_error.log");

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
            catch (Exception ex)
            {
                // Auditoria no debe bloquear ventas, abonos ni cierres operativos.
                LogAuditError(ex);
            }
        }

        private static string ResolveCashierName(SqlConnection cn, SqlTransaction tx, int cashierID)
        {
            if (cashierID <= 0)
                return string.Empty;

            try
            {
                var idColumn = GetFirstExistingColumn(cn, tx, "Cashier", "ID", "CashierID");
                var nameColumn = GetFirstExistingColumn(cn, tx, "Cashier", "Name", "CashierName", "FullName", "NombreCompleto", "Description", "Number", "Login", "UserName", "Username");

                if (string.IsNullOrWhiteSpace(idColumn) || string.IsNullOrWhiteSpace(nameColumn))
                    return string.Empty;

                using (var cmd = new SqlCommand(
                    $"SELECT TOP 1 ISNULL(CONVERT(NVARCHAR(120), [{nameColumn}]), '') FROM dbo.Cashier WHERE [{idColumn}] = @CashierID", cn))
                {
                    cmd.Transaction = tx;
                    cmd.Parameters.AddWithValue("@CashierID", cashierID);
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result);
                }
            }
            catch (Exception ex)
            {
                // Si no se puede resolver el cajero, igual debe registrarse la accion.
                LogAuditError(ex);
                return string.Empty;
            }
        }

        private static string GetFirstExistingColumn(SqlConnection cn, SqlTransaction tx, string tableName, params string[] candidates)
        {
            using (var cmd = new SqlCommand(@"
SELECT TOP 1 c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@TableName)
  AND c.name IN (" + BuildColumnParameterList(candidates.Length) + @")
ORDER BY CASE c.name
" + BuildColumnOrderCases(candidates) + @"
    ELSE 999
END;", cn))
            {
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("@TableName", "dbo." + tableName);
                for (var i = 0; i < candidates.Length; i++)
                {
                    cmd.Parameters.Add("@Column" + i, SqlDbType.NVarChar, 128).Value = candidates[i];
                }

                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : Convert.ToString(value);
            }
        }

        private static string BuildColumnParameterList(int count)
        {
            var parameters = new string[count];
            for (var i = 0; i < count; i++)
            {
                parameters[i] = "@Column" + i;
            }

            return string.Join(", ", parameters);
        }

        private static string BuildColumnOrderCases(string[] candidates)
        {
            var cases = string.Empty;
            for (var i = 0; i < candidates.Length; i++)
            {
                cases += $"    WHEN @Column{i} THEN {i}\r\n";
            }

            return cases;
        }

        private static void LogAuditError(Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(ErrorLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
            }
            catch
            {
                // No dejamos que un error escribiendo el log bloquee operaciones del POS.
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            value = value ?? string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}

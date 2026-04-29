using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public class LoginController : ApiController
    {
        readonly string rmhConnectionString = AppConfig.ConnectionString("RMHPOS");
     

        private Cliente_App AuthenticateUser(int ID_CLIENTE, string LOGIN, string CLAVE, string TOKEN)
        {
            if (string.IsNullOrWhiteSpace(LOGIN))
                return null;

            try
            {
                using (var connection = new SqlConnection(rmhConnectionString))
                {
                    connection.Open();
                    var columns = GetCashierColumns(connection);
                    var loginColumn = GetFirstExistingColumn(columns, "Number", "Login", "UserName", "Username", "Code", "CashierNumber", "Name");
                    var passwordColumn = GetFirstExistingColumn(columns, "Password", "Clave", "Pass", "UserPassword");
                    var idColumn = GetFirstExistingColumn(columns, "ID", "CashierID");
                    var nameColumn = GetFirstExistingColumn(columns, "Name", "FullName", "Description", "Login", "Number");
                    var storeColumn = GetFirstExistingColumn(columns, "StoreID", "ID_STORE", "Store");
                    var securityLevelColumn = GetFirstExistingColumn(columns, "SecurityLevel");
                    var privilegesColumn = GetFirstExistingColumn(columns, "Privileges");

                    if (string.IsNullOrWhiteSpace(loginColumn))
                        return null;

                    var sql = "SELECT TOP 1 " +
                              BuildSelectColumn(idColumn, "ID_CLIENTE", "1") + ", " +
                              BuildSelectColumn(loginColumn, "US_LOGIN", "''") + ", " +
                              BuildSelectColumn(nameColumn, "US_NOMBRE", "''") + ", " +
                              BuildSelectColumn(storeColumn, "US_ID_STORE", "0") + ", " +
                              BuildSelectColumn(securityLevelColumn, "US_SECURITY_LEVEL", "0") + ", " +
                              BuildSelectColumn(privilegesColumn, "US_PRIVILEGES", "0") + ", " +
                              (string.IsNullOrWhiteSpace(passwordColumn)
                                  ? "'' AS [US_PWD_STORED]"
                                  : "ISNULL(LTRIM(RTRIM(CONVERT(NVARCHAR(500), [" + passwordColumn + "]))), '') AS [US_PWD_STORED]") +
                              " FROM [Cashier] WHERE LTRIM(RTRIM(CONVERT(NVARCHAR(100), [" + loginColumn + "]))) = @login";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@login", LOGIN.Trim());

                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                return null;

                            var storedPassword = ReadString(reader, "US_PWD_STORED", string.Empty);
                            var inputPassword = (CLAVE ?? string.Empty).Trim();

                            if (!PasswordMatches(inputPassword, storedPassword))
                                return null;

                            var clienteApp = new Cliente_App
                            {
                                ID_CLIENTE = ReadInt(reader, "ID_CLIENTE", ID_CLIENTE),
                                US_LOGIN = ReadString(reader, "US_LOGIN", LOGIN.Trim()),
                                US_NOMBRE = ReadString(reader, "US_NOMBRE", LOGIN.Trim()),
                                US_CLAVE = CLAVE,
                                US_ID_STORE = ReadNullableInt(reader, "US_ID_STORE"),
                                US_ESTADO = 1,
                                US_SECURITY_LEVEL = (short)ReadInt(reader, "US_SECURITY_LEVEL", 0),
                                US_PRIVILEGES = ReadInt(reader, "US_PRIVILEGES", 0)
                            };

                            reader.Close();
                            FillRoleInfo(connection, clienteApp.ID_CLIENTE, clienteApp);
                            return clienteApp;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        static List<string> GetCashierColumns(SqlConnection connection)
        {
            var columns = new List<string>();
            using (var command = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Cashier'", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    columns.Add(Convert.ToString(reader[0]));
            }

            return columns;
        }

        static string GetFirstExistingColumn(IEnumerable<string> columns, params string[] candidates)
        {
            return candidates.FirstOrDefault(candidate =>
                columns.Any(column => string.Equals(column, candidate, StringComparison.OrdinalIgnoreCase)));
        }

        static string BuildSelectColumn(string sourceColumn, string alias, string fallbackSql)
        {
            return string.IsNullOrWhiteSpace(sourceColumn)
                ? fallbackSql + " AS [" + alias + "]"
                : "[" + sourceColumn + "] AS [" + alias + "]";
        }

        static string ReadString(SqlDataReader reader, string columnName, string fallback)
        {
            var value = reader[columnName];
            return value == DBNull.Value ? fallback : Convert.ToString(value);
        }

        static int ReadInt(SqlDataReader reader, string columnName, int fallback)
        {
            var value = reader[columnName];
            if (value == DBNull.Value)
                return fallback;

            int parsed;
            return int.TryParse(Convert.ToString(value), out parsed) ? parsed : fallback;
        }

        static int? ReadNullableInt(SqlDataReader reader, string columnName)
        {
            var value = reader[columnName];
            if (value == DBNull.Value)
                return null;

            int parsed;
            return int.TryParse(Convert.ToString(value), out parsed) ? parsed : (int?)null;
        }

        static void FillRoleInfo(SqlConnection connection, int cashierId, Cliente_App cliente)
        {
            // Intenta PosRoleID primero, luego ManagerRoleID
            const string sql =
                "SELECT TOP 1 ar.Code, ar.Name, ar.Privileges " +
                "FROM [RMH_LoginRole] lr " +
                "INNER JOIN [RMH_ApplicationRole] ar " +
                "    ON ar.ID = CASE WHEN lr.PosRoleID > 0 THEN lr.PosRoleID ELSE lr.ManagerRoleID END " +
                "WHERE lr.CashierID = @cashierId";

            try
            {
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@cashierId", cashierId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cliente.US_ROLE_CODE = ReadString(reader, "Code", string.Empty);
                            cliente.US_ROLE_NAME = ReadString(reader, "Name", string.Empty);
                            cliente.US_ROLE_PRIVILEGES = ReadString(reader, "Privileges", string.Empty);
                        }
                    }
                }
            }
            catch
            {
                // Si las tablas de roles no existen, el login sigue funcionando sin rol asignado
            }
        }

        static bool PasswordMatches(string inputPassword, string storedPassword)
        {
            // Sin contraseña: acceso libre
            if (string.IsNullOrEmpty(storedPassword))
                return true;

            if (string.IsNullOrEmpty(inputPassword))
                return false;

            // Solo se aceptan contraseñas cifradas con AES por RMH Store Manager
            string decrypted;
            if (TryDecryptRmhPassword(storedPassword, out decrypted))
                return string.Equals(inputPassword, decrypted, StringComparison.Ordinal);

            return false;
        }

        // Default encryption key used by RMH.APP.Core.Cryptographer
        private static readonly byte[] RmhAesKey = BuildRmhKey("eddef4dd187aa3a3660c76ec9");

        static byte[] BuildRmhKey(string secret)
        {
            var utf16 = Encoding.Unicode.GetBytes(secret);
            var key = new byte[32];
            Array.Copy(utf16, 0, key, 0, 32);
            return key;
        }

        static bool TryDecryptRmhPassword(string encryptedBase64, out string decrypted)
        {
            decrypted = null;
            try
            {
                var allBytes = Convert.FromBase64String(encryptedBase64);
                if (allBytes.Length < 32)
                    return false;

                var iv = new byte[16];
                var cipher = new byte[allBytes.Length - 16];
                Array.Copy(allBytes, 0, iv, 0, 16);
                Array.Copy(allBytes, 16, cipher, 0, cipher.Length);

                using (var aes = new RijndaelManaged())
                {
                    aes.KeySize = 256;
                    aes.Key = RmhAesKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                        decrypted = Encoding.Unicode.GetString(plainBytes);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }


        [Route("api/Login")]
        [HttpPost]
        public Cliente_App Post([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LOGIN))
                return null;

            return AuthenticateUser(request.ID_CLIENTE, request.LOGIN, request.CLAVE ?? string.Empty, request.TOKEN ?? string.Empty);
        }

        [Route("api/Login/PostUpdate")]
        [HttpPost]
        public HttpResponseMessage PostUpdate(Cliente_App Cliente)
        {
            HttpResponseMessage msg = null;
            try
            {
                using (var wsCliente = new wsSecurityMain.FacturaMeCrContractClient())
                {
                var resultado = wsCliente.RegistraCliente_App(Cliente.ID_CLIENTE, Cliente.US_LOGIN, Cliente.US_TOKEN
                    , Cliente.DEV_MODELO, Cliente.DEV_NAME, Cliente.DEV_VERSION, Cliente.DEV_SERIAL_PHONE);

                msg = Request.CreateResponse(HttpStatusCode.OK, resultado);
                }
            }
            catch
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error interno al actualizar registro.");
            }

            return msg;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Web.Http;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// Controlador de autenticación de cajeros.
    /// Consulta la tabla <c>Cashier</c> de la BD RMH POS con detección automática de columnas
    /// para soportar distintas versiones del esquema.
    /// </summary>
    public class LoginController : ApiController
    {
        readonly string rmhConnectionString = ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString;
     

        [HttpGet]
        public Cliente_App Get(int ID_CLIENTE, string LOGIN, string CLAVE, string TOKEN)
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

                    if (string.IsNullOrWhiteSpace(loginColumn))
                        return null;

                    var sql = "SELECT TOP 1 " +
                              BuildSelectColumn(idColumn, "ID_CLIENTE", "1") + ", " +
                              BuildSelectColumn(loginColumn, "US_LOGIN", "''") + ", " +
                              BuildSelectColumn(nameColumn, "US_NOMBRE", "''") + ", " +
                              BuildSelectColumn(storeColumn, "US_ID_STORE", "0") +
                              " FROM [Cashier] WHERE LTRIM(RTRIM(CONVERT(NVARCHAR(100), [" + loginColumn + "]))) = @login";

                    if (!string.IsNullOrWhiteSpace(passwordColumn))
                        sql += " AND ISNULL(LTRIM(RTRIM(CONVERT(NVARCHAR(100), [" + passwordColumn + "]))), '') = @password";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@login", LOGIN.Trim());
                        if (!string.IsNullOrWhiteSpace(passwordColumn))
                            command.Parameters.AddWithValue("@password", (CLAVE ?? string.Empty).Trim());

                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                return null;

                            return new Cliente_App
                            {
                                ID_CLIENTE = ReadInt(reader, "ID_CLIENTE", ID_CLIENTE),
                                US_LOGIN = ReadString(reader, "US_LOGIN", LOGIN.Trim()),
                                US_NOMBRE = ReadString(reader, "US_NOMBRE", LOGIN.Trim()),
                                US_CLAVE = CLAVE,
                                US_ID_STORE = ReadNullableInt(reader, "US_ID_STORE"),
                                US_ESTADO = 1
                            };
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


        [Route("api/Login")]
        [HttpPost]
        public Cliente_App Post([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LOGIN))
                return null;

            return Get(request.ID_CLIENTE, request.LOGIN, request.CLAVE ?? string.Empty, request.TOKEN ?? string.Empty);
        }

        [Route("api/Login/PostUpdate")]
        [HttpPost]
        public HttpResponseMessage PostUpdate(Cliente_App Cliente)
        {
            HttpResponseMessage msg = null;
            string registroActual = "";
            try
            {
                using (var wsCliente = new wsSecurityMain.FacturaMeCrContractClient())
                {
                var resultado = wsCliente.RegistraCliente_App(Cliente.ID_CLIENTE, Cliente.US_LOGIN, Cliente.US_TOKEN
                    , Cliente.DEV_MODELO, Cliente.DEV_NAME, Cliente.DEV_VERSION, Cliente.DEV_SERIAL_PHONE);

                msg = Request.CreateResponse(HttpStatusCode.OK, resultado);
                }
            }
            catch (Exception ex)
            {
                msg = Request.CreateResponse(HttpStatusCode.InternalServerError, "Error: " + registroActual + " / " + ex.Message.ToString());
            }

            return msg;
        }
    }
}

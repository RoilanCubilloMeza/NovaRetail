using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// CRUD para usuarios del sistema (tabla Cashier) y sus roles (RMH_LoginRole / RMH_ApplicationRole).
    /// </summary>
    public class UsuariosController : ApiController
    {
        private readonly string _cs = ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString;

        // ───── GET api/Usuarios ─────

        [HttpGet]
        public IHttpActionResult Get(string q = null, string estado = null)
        {
            var list = new List<UsuarioDto>();
            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    var columns = GetCashierColumns(cn);
                    var idCol = GetFirst(columns, "ID", "CashierID");
                    var loginCol = GetFirst(columns, "Number", "Login", "UserName", "Username", "Code", "CashierNumber", "Name");
                    var nameCol = GetFirst(columns, "Name", "FullName", "Description", "Login", "Number");
                    var secCol = GetFirst(columns, "SecurityLevel");
                    var privCol = GetFirst(columns, "Privileges");
                    var storeCol = GetFirst(columns, "StoreID", "ID_STORE", "Store");

                    var whereParts = new List<string>();
                    var cmd = new SqlCommand();

                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        var searchParts = new List<string>();
                        var searchValue = "%" + q.Trim() + "%";

                        if (!string.IsNullOrWhiteSpace(loginCol))
                            searchParts.Add("[" + loginCol + "] LIKE @q");

                        if (!string.IsNullOrWhiteSpace(nameCol) && !string.Equals(nameCol, loginCol, StringComparison.OrdinalIgnoreCase))
                            searchParts.Add("[" + nameCol + "] LIKE @q");

                        if (!string.IsNullOrWhiteSpace(idCol))
                            searchParts.Add("CAST([" + idCol + "] AS NVARCHAR(30)) LIKE @q");

                        if (searchParts.Count > 0)
                        {
                            whereParts.Add("(" + string.Join(" OR ", searchParts) + ")");
                            cmd.Parameters.AddWithValue("@q", searchValue);
                        }
                    }

                    var estadoValue = (estado ?? string.Empty).Trim().ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(secCol))
                    {
                        if (estadoValue == "activo" || estadoValue == "activos")
                            whereParts.Add("[" + secCol + "] > 0");
                        else if (estadoValue == "inactivo" || estadoValue == "inactivos")
                            whereParts.Add("[" + secCol + "] <= 0");
                    }

                    var sql =
                        "SELECT " +
                        Col(idCol, "CashierID", "0") + ", " +
                        Col(loginCol, "Login", "''") + ", " +
                        Col(nameCol, "CashierName", "''") + ", " +
                        Col(secCol, "SecurityLevel", "0") + ", " +
                        Col(privCol, "Privileges", "0") + ", " +
                        Col(storeCol, "StoreID", "0") +
                        " FROM [Cashier]" +
                        (whereParts.Count > 0 ? " WHERE " + string.Join(" AND ", whereParts) : string.Empty) +
                        " ORDER BY " + (idCol ?? "1");

                    cmd.Connection = cn;
                    cmd.CommandText = sql;

                    using (cmd)
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var cashierId = ToInt(r["CashierID"]);
                            list.Add(new UsuarioDto
                            {
                                Id = cashierId,
                                NombreUsuario = r["Login"].ToString().Trim(),
                                NombreCompleto = r["CashierName"].ToString().Trim(),
                                SecurityLevel = (short)ToInt(r["SecurityLevel"]),
                                Privileges = ToInt(r["Privileges"]),
                                StoreID = ToInt(r["StoreID"])
                            });
                        }
                    }

                    // Cargar roles para todos los usuarios
                    FillRoles(cn, list);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(list);
        }

        // ───── GET api/Usuarios/Roles ─────

        [Route("api/Usuarios/Roles")]
        [HttpGet]
        public IHttpActionResult GetRoles()
        {
            var list = new List<RolDto>();
            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT ID, ISNULL(Code,'') AS Code, ISNULL(Name,'') AS Name, ISNULL(Privileges,'') AS Privileges FROM [RMH_ApplicationRole] ORDER BY Name", cn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new RolDto
                            {
                                Id = Convert.ToInt32(r["ID"]),
                                Code = r["Code"].ToString(),
                                Name = r["Name"].ToString(),
                                Privileges = r["Privileges"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(list);
        }

        // ───── PUT api/Usuarios ─────

        [HttpPut]
        public IHttpActionResult Put([FromBody] UsuarioUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return BadRequest("ID de usuario es requerido.");

            try
            {
                using (var cn = new SqlConnection(_cs))
                {
                    cn.Open();
                    var columns = GetCashierColumns(cn);
                    var nameCol = GetFirst(columns, "Name", "FullName", "Description");
                    var secCol = GetFirst(columns, "SecurityLevel");

                    // Actualizar nombre y security level en Cashier
                    if (!string.IsNullOrWhiteSpace(nameCol))
                    {
                        var setParts = new List<string>();
                        var cmd = new SqlCommand();

                        if (!string.IsNullOrWhiteSpace(nameCol))
                        {
                            setParts.Add("[" + nameCol + "] = @name");
                            cmd.Parameters.AddWithValue("@name", (dto.NombreCompleto ?? string.Empty).Trim());
                        }

                        if (!string.IsNullOrWhiteSpace(secCol))
                        {
                            setParts.Add("[" + secCol + "] = @sec");
                            cmd.Parameters.AddWithValue("@sec", dto.SecurityLevel);
                        }

                        if (setParts.Count > 0)
                        {
                            cmd.Connection = cn;
                            cmd.CommandText = "UPDATE [Cashier] SET " + string.Join(", ", setParts) + " WHERE [ID] = @id";
                            cmd.Parameters.AddWithValue("@id", dto.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Actualizar rol en RMH_LoginRole
                    if (dto.RoleId > 0)
                    {
                        UpdateLoginRole(cn, dto.Id, dto.RoleId);
                    }
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(dto);
        }

        // ───── Helpers ─────

        private void FillRoles(SqlConnection cn, List<UsuarioDto> users)
        {
            if (users.Count == 0) return;

            const string sql =
                "SELECT lr.CashierID, ar.ID AS RoleID, ar.Code, ar.Name, ar.Privileges " +
                "FROM [RMH_LoginRole] lr " +
                "INNER JOIN [RMH_ApplicationRole] ar " +
                "    ON ar.ID = CASE WHEN lr.PosRoleID > 0 THEN lr.PosRoleID ELSE lr.ManagerRoleID END";

            try
            {
                var lookup = users.ToDictionary(u => u.Id);
                using (var cmd = new SqlCommand(sql, cn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var cashierId = ToInt(r["CashierID"]);
                        if (lookup.TryGetValue(cashierId, out var user))
                        {
                            user.RoleId = ToInt(r["RoleID"]);
                            user.RolCode = r["Code"].ToString();
                            user.RolName = r["Name"].ToString();
                            user.RolPrivileges = r["Privileges"].ToString();
                        }
                    }
                }
            }
            catch
            {
                // Tablas de roles no existen, continuar sin roles
            }
        }

        private void UpdateLoginRole(SqlConnection cn, int cashierId, int roleId)
        {
            const string sql =
                @"IF EXISTS (SELECT 1 FROM [RMH_LoginRole] WHERE CashierID = @cid)
                      UPDATE [RMH_LoginRole] SET PosRoleID = @rid WHERE CashierID = @cid
                  ELSE
                      INSERT INTO [RMH_LoginRole] (CashierID, PosRoleID, ManagerRoleID) VALUES (@cid, @rid, 0)";

            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@cid", cashierId);
                cmd.Parameters.AddWithValue("@rid", roleId);
                cmd.ExecuteNonQuery();
            }
        }

        static List<string> GetCashierColumns(SqlConnection cn)
        {
            var cols = new List<string>();
            using (var cmd = new SqlCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Cashier'", cn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    cols.Add(Convert.ToString(r[0]));
            }
            return cols;
        }

        static string GetFirst(IEnumerable<string> cols, params string[] candidates)
        {
            return candidates.FirstOrDefault(c =>
                cols.Any(col => string.Equals(col, c, StringComparison.OrdinalIgnoreCase)));
        }

        static string Col(string src, string alias, string fallback)
        {
            return string.IsNullOrWhiteSpace(src)
                ? fallback + " AS [" + alias + "]"
                : "[" + src + "] AS [" + alias + "]";
        }

        static int ToInt(object value)
        {
            if (value == DBNull.Value) return 0;
            int p;
            return int.TryParse(Convert.ToString(value), out p) ? p : 0;
        }
    }

    public class UsuarioDto
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; }
        public string NombreCompleto { get; set; }
        public short SecurityLevel { get; set; }
        public int Privileges { get; set; }
        public int StoreID { get; set; }
        public int RoleId { get; set; }
        public string RolCode { get; set; }
        public string RolName { get; set; }
        public string RolPrivileges { get; set; }
    }

    public class RolDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Privileges { get; set; }
    }

    public class UsuarioUpdateDto
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; }
        public short SecurityLevel { get; set; }
        public int RoleId { get; set; }
    }
}

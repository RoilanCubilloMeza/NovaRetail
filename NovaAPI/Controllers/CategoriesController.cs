using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace NovaAPI.Controllers
{
    /// <summary>
    /// GET api/Categories → categorías visibles en la UI.
    ///
    /// Prioridad:
    ///   1. Si se envía ?userName=X y ese usuario tiene preferencia en AVS_UserPreferences, se usa.
    ///   2. Si no, se busca el parámetro global CAT-01 en AVS_Parametros.
    ///   3. Si tampoco existe, devuelve los primeros 6 departamentos.
    ///
    /// El valor almacenado es una lista de IDs separados por coma: '1,5,12,8'
    /// </summary>
    [RoutePrefix("api/Categories")]
    public class CategoriesController : ApiController
    {
        private const int MaxCategories = 6;
        private const string PrefKey = "CAT-01";

        [HttpGet]
        [Route("")]
        public IHttpActionResult Get(string userName = null)
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Ok(new List<CategoryDto>());

            try
            {
                var categories = new List<CategoryDto>();

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    // 1. Detectar columnas de Department
                    var columns = new List<string>();
                    using (var cmd = new SqlCommand(
                        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Department'", cn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            columns.Add(Convert.ToString(reader[0]));
                    }

                    var idCol = FirstExisting(columns, "ID", "DepartmentID");
                    var nameCol = FirstExisting(columns, "Name", "Description", "DepartmentName");

                    if (string.IsNullOrEmpty(idCol) || string.IsNullOrEmpty(nameCol))
                        return Ok(categories);

                    // 2. Cargar todos los departamentos (ID → Name)
                    var allDepts = new List<CategoryDto>();
                    var sql = string.Format(
                        "SELECT [{0}] AS ID, ISNULL([{1}],'') AS Name FROM dbo.Department ORDER BY [{1}]",
                        idCol, nameCol);

                    using (var cmd = new SqlCommand(sql, cn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var name = (reader["Name"]?.ToString() ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            allDepts.Add(new CategoryDto
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                Name = name
                            });
                        }
                    }

                    // 3. Leer preferencia: primero por usuario, luego global
                    string paramValue = null;

                    if (!string.IsNullOrWhiteSpace(userName))
                    {
                        EnsureUserPreferencesTable(cn);
                        paramValue = ReadUserPreference(cn, userName.Trim(), PrefKey);
                    }

                    if (string.IsNullOrWhiteSpace(paramValue))
                    {
                        using (var cmd = new SqlCommand(
                            "SELECT TOP 1 LTRIM(RTRIM(VALOR)) FROM dbo.AVS_Parametros WHERE CODIGO = 'CAT-01'", cn))
                        {
                            var val = cmd.ExecuteScalar();
                            if (val != null && val != DBNull.Value)
                                paramValue = Convert.ToString(val);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(paramValue))
                    {
                        // Sin CAT-01: devolver los primeros N departamentos
                        return Ok(allDepts.Take(MaxCategories).ToList());
                    }

                    // 4. Con CAT-01: filtrar por IDs y respetar el orden del parámetro
                    var requestedIds = paramValue
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(n => n.Trim())
                        .Where(n => int.TryParse(n, out _))
                        .Select(n => int.Parse(n))
                        .Take(MaxCategories)
                        .ToList();

                    var deptMap = new Dictionary<int, CategoryDto>();
                    foreach (var d in allDepts)
                    {
                        if (!deptMap.ContainsKey(d.ID))
                            deptMap[d.ID] = d;
                    }

                    foreach (var id in requestedIds)
                    {
                        if (deptMap.TryGetValue(id, out var dept))
                        {
                            categories.Add(new CategoryDto
                            {
                                ID = dept.ID,
                                Name = dept.Name
                            });
                        }
                    }
                }

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// GET api/Categories/All → TODOS los departamentos sin filtrar (para la pantalla de configuración).
        /// </summary>
        [HttpGet]
        [Route("All")]
        public IHttpActionResult GetAll()
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Ok(new List<CategoryDto>());

            try
            {
                var departments = new List<CategoryDto>();

                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    var columns = new List<string>();
                    using (var cmd = new SqlCommand(
                        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Department'", cn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            columns.Add(Convert.ToString(reader[0]));
                    }

                    var idCol = FirstExisting(columns, "ID", "DepartmentID");
                    var nameCol = FirstExisting(columns, "Name", "Description", "DepartmentName");

                    if (string.IsNullOrEmpty(idCol) || string.IsNullOrEmpty(nameCol))
                        return Ok(departments);

                    var sql = string.Format(
                        "SELECT [{0}] AS ID, ISNULL([{1}],'') AS Name FROM dbo.Department ORDER BY [{1}]",
                        idCol, nameCol);

                    using (var cmd = new SqlCommand(sql, cn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var name = (reader["Name"]?.ToString() ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(name))
                                continue;

                            departments.Add(new CategoryDto
                            {
                                ID = Convert.ToInt32(reader["ID"]),
                                Name = name
                            });
                        }
                    }
                }

                return Ok(departments);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// GET api/Categories/Config?userName=X → devuelve los IDs seleccionados del usuario (o global).
        /// </summary>
        [HttpGet]
        [Route("Config")]
        public IHttpActionResult GetConfig(string userName = null)
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Ok(new CategoryConfigDto());

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    // Preferencia por usuario
                    if (!string.IsNullOrWhiteSpace(userName))
                    {
                        EnsureUserPreferencesTable(cn);
                        var userVal = ReadUserPreference(cn, userName.Trim(), PrefKey);
                        if (userVal != null)
                            return Ok(new CategoryConfigDto { SelectedIds = userVal });
                    }

                    // Fallback global
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 LTRIM(RTRIM(VALOR)) FROM dbo.AVS_Parametros WHERE CODIGO = 'CAT-01'", cn))
                    {
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            return Ok(new CategoryConfigDto { SelectedIds = Convert.ToString(val) });
                    }
                }

                return Ok(new CategoryConfigDto());
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// PUT api/Categories/Config → guarda los IDs seleccionados.
        /// Body: { "SelectedIds": "1,5,12", "UserName": "roilan" }
        /// Si se envía UserName, guarda como preferencia del usuario.
        /// Si no, guarda en el parámetro global CAT-01.
        /// </summary>
        [HttpPut]
        [Route("Config")]
        public IHttpActionResult PutConfig([FromBody] CategoryConfigDto dto)
        {
            var connectionString = AppConfig.ConnectionString("RMHPOS");
            if (string.IsNullOrWhiteSpace(connectionString))
                return BadRequest("No hay cadena de conexión configurada.");

            // Validar y limpiar: solo IDs numéricos, máximo 6
            var cleanValue = string.Empty;
            if (!string.IsNullOrWhiteSpace(dto?.SelectedIds))
            {
                var ids = dto.SelectedIds
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => int.TryParse(s, out _))
                    .Take(MaxCategories)
                    .ToList();

                cleanValue = string.Join(",", ids);
            }

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

                    if (!string.IsNullOrWhiteSpace(dto?.UserName))
                    {
                        // Guardar como preferencia de usuario
                        EnsureUserPreferencesTable(cn);
                        SaveUserPreference(cn, dto.UserName.Trim(), PrefKey, cleanValue);
                    }
                    else
                    {
                        // Guardar como parámetro global
                        bool exists;
                        using (var cmd = new SqlCommand(
                            "SELECT COUNT(1) FROM dbo.AVS_Parametros WHERE CODIGO = 'CAT-01'", cn))
                        {
                            exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                        }

                        if (exists)
                        {
                            using (var cmd = new SqlCommand(
                                "UPDATE dbo.AVS_Parametros SET VALOR = @valor WHERE CODIGO = 'CAT-01'", cn))
                            {
                                cmd.Parameters.AddWithValue("@valor", cleanValue);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (var cmd = new SqlCommand(
                                "INSERT INTO dbo.AVS_Parametros (CODIGO, VALOR) VALUES ('CAT-01', @valor)", cn))
                            {
                                cmd.Parameters.AddWithValue("@valor", cleanValue);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private static string FirstExisting(List<string> columns, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (columns.Exists(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
            return null;
        }

        /// <summary>Crea AVS_UserPreferences si no existe.</summary>
        private static void EnsureUserPreferencesTable(SqlConnection cn)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AVS_UserPreferences')
                BEGIN
                    CREATE TABLE dbo.AVS_UserPreferences (
                        UserName  NVARCHAR(100) NOT NULL,
                        PrefKey   NVARCHAR(50)  NOT NULL,
                        PrefValue NVARCHAR(500) NOT NULL DEFAULT '',
                        CONSTRAINT PK_AVS_UserPreferences PRIMARY KEY (UserName, PrefKey)
                    );
                END";

            using (var cmd = new SqlCommand(sql, cn))
                cmd.ExecuteNonQuery();
        }

        private static string ReadUserPreference(SqlConnection cn, string userName, string prefKey)
        {
            using (var cmd = new SqlCommand(
                "SELECT TOP 1 LTRIM(RTRIM(PrefValue)) FROM dbo.AVS_UserPreferences WHERE UserName = @user AND PrefKey = @key", cn))
            {
                cmd.Parameters.AddWithValue("@user", userName);
                cmd.Parameters.AddWithValue("@key", prefKey);
                var val = cmd.ExecuteScalar();
                if (val != null && val != DBNull.Value)
                {
                    var result = Convert.ToString(val);
                    return string.IsNullOrWhiteSpace(result) ? null : result;
                }
            }
            return null;
        }

        private static void SaveUserPreference(SqlConnection cn, string userName, string prefKey, string prefValue)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.AVS_UserPreferences WHERE UserName = @user AND PrefKey = @key)
                    UPDATE dbo.AVS_UserPreferences SET PrefValue = @val WHERE UserName = @user AND PrefKey = @key
                ELSE
                    INSERT INTO dbo.AVS_UserPreferences (UserName, PrefKey, PrefValue) VALUES (@user, @key, @val)";

            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@user", userName);
                cmd.Parameters.AddWithValue("@key", prefKey);
                cmd.Parameters.AddWithValue("@val", prefValue ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public class CategoryDto
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CategoryConfigDto
    {
        public string SelectedIds { get; set; } = string.Empty;
        public string UserName { get; set; }
    }
}

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
    /// Si existe el parámetro CAT-01 en AVS_Parametros, su VALOR es una lista
    /// de IDs de departamento separados por coma.  Ejemplo:
    ///   CODIGO = 'CAT-01'  VALOR = '1,5,12,8'
    /// Solo se devuelven los departamentos cuyos IDs coincidan, en el orden
    /// indicado por el parámetro, con un máximo de 6.
    ///
    /// Si CAT-01 NO existe, devuelve TODOS los departamentos de la tabla
    /// Department ordenados por nombre (máximo 6).
    /// </summary>
    [RoutePrefix("api/Categories")]
    public class CategoriesController : ApiController
    {
        private const int MaxCategories = 6;

        [HttpGet]
        [Route("")]
        public IHttpActionResult Get()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
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

                    // 3. Leer el parámetro CAT-01 (opcional) para filtrar/ordenar
                    string paramValue = null;
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 LTRIM(RTRIM(VALOR)) FROM dbo.AVS_Parametros WHERE CODIGO = 'CAT-01'", cn))
                    {
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            paramValue = Convert.ToString(val);
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
            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
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
        /// GET api/Categories/Config → devuelve el valor actual de CAT-01 (IDs separados por coma).
        /// </summary>
        [HttpGet]
        [Route("Config")]
        public IHttpActionResult GetConfig()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                return Ok(new CategoryConfigDto());

            try
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();

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
        /// PUT api/Categories/Config → guarda los IDs seleccionados en CAT-01.
        /// Body: { "SelectedIds": "1,5,12" }
        /// </summary>
        [HttpPut]
        [Route("Config")]
        public IHttpActionResult PutConfig([FromBody] CategoryConfigDto dto)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["RMHPOS"]?.ConnectionString;
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

                    // Verificar si CAT-01 ya existe
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
    }

    public class CategoryDto
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CategoryConfigDto
    {
        public string SelectedIds { get; set; } = string.Empty;
    }
}

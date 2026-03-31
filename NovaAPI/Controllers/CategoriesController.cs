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
    /// de nombres de departamento separados por coma.  Ejemplo:
    ///   CODIGO = 'CAT-01'  VALOR = 'Supermercado,Ferretería,Calzado,Hogar'
    /// Solo se devuelven las categorías que coinciden con Department.
    ///
    /// Si CAT-01 NO existe, devuelve TODOS los departamentos de la tabla
    /// Department ordenados por nombre.
    /// </summary>
    public class CategoriesController : ApiController
    {
        [HttpGet]
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
                        // Sin CAT-01: devolver todos los departamentos
                        return Ok(allDepts);
                    }

                    // 4. Con CAT-01: filtrar y ordenar según el parámetro
                    var requestedNames = paramValue
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(n => n.Trim())
                        .Where(n => n.Length > 0)
                        .ToList();

                    var deptMap = allDepts.ToDictionary(
                        d => d.Name,
                        d => d,
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var requested in requestedNames)
                    {
                        if (deptMap.TryGetValue(requested, out var dept))
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
}

using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web.Http;
using NovaAPI;

public class SalesRepController : ApiController
{
    readonly RMHCDataContext db;

    public SalesRepController()
    {
        db = new RMHCDataContext(ConfigurationManager.ConnectionStrings["RMHPOS"].ConnectionString);
    }

    [HttpGet]
    [Route("api/SalesRep/Get")]
    public IHttpActionResult Get()
    {
        try
        {
            var salesReps = db.spWS_GetSalesRep().ToList();
            return Ok(salesReps);
        }
        catch (Exception ex)
        {
            return Content(HttpStatusCode.InternalServerError, $"An error occurred: {ex.Message}");
        }
    }
}

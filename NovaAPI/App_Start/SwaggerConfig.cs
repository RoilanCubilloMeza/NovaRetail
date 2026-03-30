using System.Linq;
using System.Web.Http;
using WebActivatorEx;
using Swashbuckle.Application;

[assembly: PreApplicationStartMethod(typeof(NovaAPI.App_Start.SwaggerConfig), "Register")]

namespace NovaAPI.App_Start
{
    public class SwaggerConfig
    {
        public static void Register()
        {
            var config = GlobalConfiguration.Configuration;

            config
                .EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "NovaAPI");
                    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
                })
                .EnableSwaggerUi();
        }
    }
}

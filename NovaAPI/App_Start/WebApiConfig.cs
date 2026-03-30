using Newtonsoft.Json;
using System.Web.Http;

namespace NovaAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Encode non-ASCII characters (ñ, á, é, ₡, etc.) as \uXXXX escape sequences
            // so the JSON payload is pure ASCII and immune to charset/encoding mismatches
            // between the .NET Framework 4.8 API and the .NET 10 MAUI client.
            config.Formatters.JsonFormatter.SerializerSettings.StringEscapeHandling =
                StringEscapeHandling.EscapeNonAscii;
        }
    }
}

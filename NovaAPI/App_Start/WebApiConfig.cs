using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.Results;
using Newtonsoft.Json;
using NovaAPI.Security;

namespace NovaAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Global exception logger — escribe a archivo antes de que Web API devuelva 500
            config.Services.Add(typeof(IExceptionLogger), new FileExceptionLogger());

            // API key authentication handler
            config.MessageHandlers.Add(new ApiKeyHandler());

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

    internal class FileExceptionLogger : ExceptionLogger
    {
        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nova_error.log");

        public override void Log(ExceptionLoggerContext context)
        {
            try
            {
                var ex = context.Exception;
                var request = context.Request != null ? $"{context.Request.Method} {context.Request.RequestUri}" : "N/A";
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {request}\r\n{ex}\r\n\r\n";
                File.AppendAllText(LogPath, entry);
            }
            catch { }
        }
    }
}

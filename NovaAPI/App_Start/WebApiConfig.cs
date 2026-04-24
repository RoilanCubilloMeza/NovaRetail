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
            // Global exception logger writes structured JSON lines for diagnostics.
            config.Services.Add(typeof(IExceptionLogger), new FileExceptionLogger());
            config.Services.Replace(typeof(IExceptionHandler), new JsonExceptionHandler());

            // Adds X-Correlation-ID to every response and makes the value available to logs.
            config.MessageHandlers.Add(new CorrelationIdHandler());

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
                var request = context.Request;
                var logEntry = new
                {
                    timestampUtc = DateTime.UtcNow,
                    level = "Error",
                    correlationId = CorrelationIdHandler.GetCorrelationId(request),
                    method = request?.Method?.Method,
                    uri = request?.RequestUri?.ToString(),
                    exceptionType = ex.GetType().FullName,
                    message = ex.Message,
                    stackTrace = ex.ToString()
                };

                File.AppendAllText(LogPath, JsonConvert.SerializeObject(logEntry) + Environment.NewLine);
            }
            catch { }
        }
    }

    internal class JsonExceptionHandler : ExceptionHandler
    {
        public override void Handle(ExceptionHandlerContext context)
        {
            var correlationId = CorrelationIdHandler.GetCorrelationId(context.Request);
            var response = context.Request.CreateResponse(HttpStatusCode.InternalServerError, new
            {
                error = "Unexpected server error.",
                correlationId
            });

            response.Headers.Add(CorrelationIdHandler.HeaderName, correlationId);
            context.Result = new ResponseMessageResult(response);
        }
    }

    internal class CorrelationIdHandler : DelegatingHandler
    {
        public const string HeaderName = "X-Correlation-ID";
        private const string PropertyName = "NovaAPI.CorrelationId";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var correlationId = ResolveCorrelationId(request);
            request.Properties[PropertyName] = correlationId;

            var response = await base.SendAsync(request, cancellationToken);
            if (!response.Headers.Contains(HeaderName))
                response.Headers.Add(HeaderName, correlationId);

            return response;
        }

        public static string GetCorrelationId(HttpRequestMessage request)
        {
            if (request != null
                && request.Properties.TryGetValue(PropertyName, out var value)
                && value is string correlationId
                && !string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }

            return Guid.NewGuid().ToString("N");
        }

        private static string ResolveCorrelationId(HttpRequestMessage request)
        {
            if (request != null
                && request.Headers.TryGetValues(HeaderName, out var values))
            {
                foreach (var value in values)
                {
                    if (IsValidHeaderValue(value))
                        return value;
                }
            }

            return Guid.NewGuid().ToString("N");
        }

        private static bool IsValidHeaderValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
                return false;

            return !value.Contains("\r") && !value.Contains("\n");
        }
    }
}

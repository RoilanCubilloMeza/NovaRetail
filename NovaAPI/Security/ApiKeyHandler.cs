using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NovaAPI.Security
{
    /// <summary>
    /// Message handler that validates the X-Api-Key header on every request.
    /// If no API key is configured (AppConfig.ApiKey is null/empty),
    /// all requests pass through (open mode — for development).
    /// </summary>
    public class ApiKeyHandler : DelegatingHandler
    {
        private const string HeaderName = "X-Api-Key";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var expectedKey = AppConfig.ApiKey;

            // No key configured → open mode (development)
            if (string.IsNullOrEmpty(expectedKey))
                return base.SendAsync(request, cancellationToken);

            // Allow Swagger / help pages without auth
            var path = request.RequestUri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            if (path == "" || path == "/swagger" || path.StartsWith("/swagger/")
                || path == "/api" || path.StartsWith("/api/help"))
            {
                return base.SendAsync(request, cancellationToken);
            }

            // Validate X-Api-Key header
            if (request.Headers.TryGetValues(HeaderName, out var values))
            {
                foreach (var value in values)
                {
                    if (string.Equals(value, expectedKey, System.StringComparison.Ordinal))
                        return base.SendAsync(request, cancellationToken);
                }
            }

            var response = request.CreateResponse(HttpStatusCode.Unauthorized,
                new { error = "Missing or invalid API key. Set the X-Api-Key header." });
            return Task.FromResult(response);
        }
    }
}

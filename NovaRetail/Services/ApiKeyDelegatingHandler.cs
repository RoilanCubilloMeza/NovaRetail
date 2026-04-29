using NovaRetail.Data;

namespace NovaRetail.Services;

/// <summary>
/// Adds the X-Api-Key header to every outgoing HTTP request
/// when an API key is configured in ApiSettings.
/// </summary>
public sealed class ApiKeyDelegatingHandler : DelegatingHandler
{
    private readonly ApiSettings _settings;

    public ApiKeyDelegatingHandler(ApiSettings settings)
    {
        _settings = settings;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            request.Headers.TryAddWithoutValidation("X-Api-Key", _settings.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}

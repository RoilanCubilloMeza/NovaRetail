using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NovaRetail.Data;

namespace NovaRetail.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task LoginAsync_returns_null_for_blank_user()
    {
        var service = BuildService((_, _) => throw new InvalidOperationException("Should not call HTTP"));

        var result = await service.LoginAsync("   ", "secret");

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_trims_user_and_maps_successful_response()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var service = BuildService(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return TestHttp.Json(HttpStatusCode.OK, """
            {
              "ID_CLIENTE": 7,
              "US_LOGIN": "cash01",
              "US_NOMBRE": "Caja Principal",
              "US_ID_STORE": 3,
              "US_ROLE_CODE": "Admin",
              "US_ROLE_NAME": "Administrador",
              "US_SECURITY_LEVEL": 9,
              "US_PRIVILEGES": 255,
              "US_ROLE_PRIVILEGES": "ALL"
            }
            """);
        });

        var result = await service.LoginAsync("  cash01  ", " 1234 ");

        Assert.NotNull(result);
        Assert.Equal("cash01", result!.UserName);
        Assert.Equal("Caja Principal", result.DisplayName);
        Assert.Equal(7, result.ClientId);
        Assert.Equal(3, result.StoreId);
        Assert.Equal("Admin", result.RoleCode);
        Assert.Equal("ALL", result.RolePrivileges);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("http://localhost:52500/api/Login", capturedRequest.RequestUri!.ToString());

        using var payload = JsonDocument.Parse(capturedBody!);
        Assert.Equal("cash01", payload.RootElement.GetProperty("LOGIN").GetString());
        Assert.Equal("1234", payload.RootElement.GetProperty("CLAVE").GetString());
    }

    [Fact]
    public async Task LoginAsync_tries_next_base_url_after_unsuccessful_response()
    {
        var attempts = 0;
        var settings = new ApiSettings
        {
            BaseUrls = ["http://first-host", "http://second-host"]
        };

        var client = new HttpClient(new DelegateHttpMessageHandler((request, _) =>
        {
            attempts++;
            if (request.RequestUri!.Host == "first-host")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            return Task.FromResult(TestHttp.Json(HttpStatusCode.OK, """
            {
              "ID_CLIENTE": 11,
              "US_LOGIN": "cash02",
              "US_NOMBRE": "Caja 2"
            }
            """));
        }));

        var service = new ApiLoginService(new StubHttpClientFactory(client), NullLogger<ApiLoginService>.Instance, settings);

        var result = await service.LoginAsync("cash02", "abcd");

        Assert.NotNull(result);
        Assert.Equal(2, attempts);
        Assert.Equal("cash02", result!.UserName);
    }

    [Fact]
    public async Task LoginAsync_rethrows_connectivity_errors()
    {
        var service = BuildService((_, _) => throw new HttpRequestException("network down"));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.LoginAsync("cash01", "1234"));
    }

    [Fact]
    public async Task IsDatabaseConnectedAsync_returns_true_when_any_endpoint_responds_ok()
    {
        var attempts = 0;
        var settings = new ApiSettings
        {
            BaseUrls = ["http://first-host", "http://second-host"]
        };

        var client = new HttpClient(new DelegateHttpMessageHandler((request, _) =>
        {
            attempts++;
            if (request.RequestUri!.Host == "first-host")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));

        var service = new ApiLoginService(new StubHttpClientFactory(client), NullLogger<ApiLoginService>.Instance, settings);

        var connected = await service.IsDatabaseConnectedAsync();

        Assert.True(connected);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetConnectionInfoAsync_returns_fallback_when_all_endpoints_fail()
    {
        var settings = new ApiSettings
        {
            BaseUrls = ["http://offline-host"]
        };

        var client = new HttpClient(new DelegateHttpMessageHandler((_, _) => throw new HttpRequestException("offline")));
        var service = new ApiLoginService(new StubHttpClientFactory(client), NullLogger<ApiLoginService>.Instance, settings);

        var result = await service.GetConnectionInfoAsync();

        Assert.NotNull(result);
        Assert.Equal("http://offline-host", result!.ApiBaseUrl);
        Assert.False(result.IsConnected);
        Assert.Equal("BM", result.DatabaseName);
    }

    [Fact]
    public async Task GetConnectionInfoAsync_uses_first_successful_response()
    {
        var settings = new ApiSettings
        {
            BaseUrls = ["http://first-host", "http://second-host"]
        };

        var client = new HttpClient(new DelegateHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "first-host")
                return Task.FromResult(TestHttp.Json(HttpStatusCode.OK, """
                {
                  "databaseServer": "SRV-01",
                  "databaseName": "BM_POS_CEDI"
                }
                """));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }));

        var service = new ApiLoginService(new StubHttpClientFactory(client), NullLogger<ApiLoginService>.Instance, settings);

        var result = await service.GetConnectionInfoAsync();

        Assert.NotNull(result);
        Assert.True(result!.IsConnected);
        Assert.Equal("http://first-host", result.ApiBaseUrl);
        Assert.Equal("SRV-01", result.DatabaseServer);
        Assert.Equal("BM_POS_CEDI", result.DatabaseName);
    }

    private static ApiLoginService BuildService(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var client = new HttpClient(new DelegateHttpMessageHandler(handler));
        var factory = new StubHttpClientFactory(client);
        return new ApiLoginService(factory, NullLogger<ApiLoginService>.Instance, new ApiSettings());
    }
}

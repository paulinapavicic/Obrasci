using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tests.Integration;

public class SerializationSecurityIntegrationTests
    : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public SerializationSecurityIntegrationTests(
       TestWebAppFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public async Task ExportPhoto_WithoutAuthentication_RedirectsToLogin()
    {
        var photoId = Guid.NewGuid();

        var response = await _client.PostAsync(
            $"/api/serialization-demo/photos/{photoId}/export",
            content: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.NotNull(response.Headers.Location);

        Assert.Contains(
            "/Account/Login",
            response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ImportSnapshot_WhitelistedType_ReturnsOk()
    {
        using var content = CreateMultipartContent(
            """
            {
              "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
              "fileName": "integration-test.jpg",
              "exportedAtUtc": "2026-09-03T12:00:00Z"
            }
            """,
            "PhotoExportSnapshot",
            "valid-snapshot.json");

        var response = await _client.PostAsync(
            "/api/serialization-demo/import",
            content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ImportSnapshot_NonWhitelistedType_ReturnsBadRequest()
    {
        using var content = CreateMultipartContent(
            """
            {
              "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
              "fileName": "integration-test.jpg",
              "exportedAtUtc": "2026-09-03T12:00:00Z"
            }
            """,
            "ApplicationUser",
            "valid-snapshot.json");

        var response = await _client.PostAsync(
            "/api/serialization-demo/import",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            "not allowed",
            responseText,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportSnapshot_PlainTextFile_ReturnsBadRequest()
    {
        using var content = CreateMultipartContent(
            "This is not a JSON snapshot.",
            "PhotoExportSnapshot",
            "not-a-snapshot.json");

        var response = await _client.PostAsync(
            "/api/serialization-demo/import",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            "JSON",
            responseText,
            StringComparison.OrdinalIgnoreCase);
    }
 
    private static MultipartFormDataContent CreateMultipartContent(
        string fileText,
        string requestedType,
        string fileName)
    {
        var content = new MultipartFormDataContent();

        var fileContent = new StringContent(
            fileText,
            Encoding.UTF8,
            "application/json");

        content.Add(fileContent, "file", fileName);

        content.Add(
            new StringContent(requestedType),
            "requestedType");

        return content;
    }
}
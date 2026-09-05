using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Obrasci.Models;
using Obrasci.Services;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Tests.Unit;

public class PhotoSnapshotDeserializerTests
{
    [Fact]
    public async Task DeserializeWhitelistedSnapshotAsync_AllowsPhotoExportSnapshot()
    {
        const string json = """
        {
          "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
          "fileName": "test-photo.jpg",
          "exportedAtUtc": "2026-09-03T10:00:00Z"
        }
        """;

        var file = CreateFormFile(
            json,
            "valid-snapshot.json",
            "application/json");

        var snapshot = await PhotoSnapshotDeserializer
            .DeserializeWhitelistedSnapshotAsync(
                file,
                "PhotoExportSnapshot");

        Assert.NotNull(snapshot);
        Assert.IsType<PhotoExportSnapshot>(snapshot);
    }

    [Fact]
    public async Task DeserializeWhitelistedSnapshotAsync_RejectsNonWhitelistedType()
    {
        const string json = """
        {
          "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
          "fileName": "test-photo.jpg",
          "exportedAtUtc": "2026-09-03T10:00:00Z"
        }
        """;

        var file = CreateFormFile(
            json,
            "valid-snapshot.json",
            "application/json");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    "ApplicationUser"));

        Assert.Equal(
            "Type 'ApplicationUser' is not allowed for deserialization.",
            exception.Message);
    }
    [Fact]
    public async Task DeserializeWhitelistedSnapshotAsync_RejectsTypeWithDifferentCasing()
    {
        const string json = """
    {
      "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
      "fileName": "test-photo.jpg",
      "exportedAtUtc": "2026-09-03T10:00:00Z"
    }
    """;

        var file = CreateFormFile(
            json,
            "valid-snapshot.json",
            "application/json");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    "photoexportsnapshot"));

        exception.Message.Should().Be(
            "Type 'photoexportsnapshot' is not allowed for deserialization.");
    }
    [Fact]
    public async Task DeserializeWhitelistedSnapshotAsync_RejectsNullJsonPayload()
    {
        var file = CreateFormFile(
            "null",
            "null-snapshot.json",
            "application/json");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    "PhotoExportSnapshot"));

        Assert.Equal(
            "Snapshot deserialization produced no object.",
            exception.Message);
    }
    [Theory]
    [InlineData("{")]
    [InlineData("""{"photoId": }""")]
    [InlineData("""{"fileName":"unfinished""")]
    public async Task DeserializeWhitelistedSnapshotAsync_WithMalformedJson_ThrowsJsonException(
    string json)
    {
        var file = CreateFormFile(
            json,
            "invalid-snapshot.json",
            "application/json");

        await Assert.ThrowsAsync<JsonException>(
            () => PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    "PhotoExportSnapshot"));
    }
    [Fact]
    public async Task DeserializeWhitelistedSnapshotAsync_WithEmptyContent_ThrowsJsonException()
    {
        var file = CreateFormFile(
            string.Empty,
            "empty-snapshot.json",
            "application/json");

        await Assert.ThrowsAsync<JsonException>(
            () => PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    "PhotoExportSnapshot"));
    }
    [Fact]
    public async Task DeserializeWhitelistedSnapshotAsync_RejectsDisallowedRequestedTypeEvenWhenFileLooksLikeJson()
    {
        const string json = """
    {
      "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
      "fileName": "test-photo.jpg",
      "exportedAtUtc": "2026-09-03T10:00:00Z"
    }
    """;

        var file = CreateFormFile(
            json,
            "PhotoExportSnapshot.json",
            "application/json");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    "System.Object"));

        Assert.Equal(
            "Type 'System.Object' is not allowed for deserialization.",
            exception.Message);
    }
    private static IFormFile CreateFormFile(
        string content,
        string fileName,
        string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(
            stream,
            0,
            bytes.Length,
            "file",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
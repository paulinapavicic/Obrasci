using System.Text.Json;
using Obrasci.Models;

namespace Obrasci.Services;

public static class PhotoSnapshotDeserializer
{
    private static readonly HashSet<string> AllowedTypes =
    [
        nameof(PhotoExportSnapshot)
    ];

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = false
        };

    public static async Task<PhotoExportSnapshot>
        DeserializeWhitelistedSnapshotAsync(
            IFormFile file,
            string requestedType,
            CancellationToken cancellationToken = default)
    {
        if (!AllowedTypes.Contains(requestedType))
        {
            throw new InvalidDataException(
                $"Type '{requestedType}' is not allowed for deserialization.");
        }

        await using var stream = file.OpenReadStream();

        var snapshot = await JsonSerializer
            .DeserializeAsync<PhotoExportSnapshot>(
                stream,
                JsonOptions,
                cancellationToken);

        if (snapshot is null)
        {
            throw new InvalidDataException(
                "Snapshot deserialization produced no object.");
        }

        return snapshot;
    }
}
using System.Text;
using System.Text.Json;

namespace Obrasci.Services;

public static class PhotoSnapshotFileValidator
{
    private const int MaximumFileSizeBytes = 1_048_576;

    public static async Task ValidateJsonSnapshotAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        if (!fileStream.CanRead)
        {
            throw new InvalidOperationException(
                "The uploaded file cannot be read.");
        }

        if (fileStream.Length == 0)
        {
            throw new InvalidDataException(
                "The uploaded file is empty.");
        }

        if (fileStream.Length > MaximumFileSizeBytes)
        {
            throw new InvalidDataException(
                "The uploaded file exceeds the 1 MB size limit.");
        }

        var prefixLength = (int)Math.Min(4, fileStream.Length);
        var prefix = new byte[prefixLength];

        var bytesRead = await fileStream.ReadAsync(
            prefix,
            cancellationToken);

        if (bytesRead != prefixLength)
        {
            throw new InvalidDataException(
                "Unable to read the file header.");
        }

        fileStream.Position = 0;

        var firstNonWhitespace = prefix
            .Select(value => (char)value)
            .FirstOrDefault(character => !char.IsWhiteSpace(character));

        if (firstNonWhitespace is not '{' and not '[')
        {
            throw new InvalidDataException(
                "The uploaded file is not a JSON object or array.");
        }

        try
        {
            await JsonDocument.ParseAsync(
                fileStream,
                cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The uploaded file contains invalid JSON.",
                exception);
        }
    }
}
namespace Obrasci.Models;

public sealed class PhotoExportSnapshot
{
    public Guid PhotoId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Hashtags { get; init; }

    public string? ContentType { get; init; }

    public long SizeBytes { get; init; }

    public DateTime UploadedAtUtc { get; init; }

    public DateTime ExportedAtUtc { get; init; }
}
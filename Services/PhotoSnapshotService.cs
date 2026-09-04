using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Models;

namespace Obrasci.Services;

public sealed class PhotoSnapshotService : IPhotoSnapshotService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PhotoSnapshotService(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<string?> ExportPhotoAsync(Guid photoId)
    {
        var photo = await _context.Photos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == photoId);

        if (photo is null)
        {
            return null;
        }

        var snapshot = new PhotoExportSnapshot
        {
            PhotoId = photo.Id,
            FileName = photo.FileName,
            Description = photo.Description,
            Hashtags = photo.Hashtags,
            ContentType = photo.ContentType,
            SizeBytes = photo.SizeBytes,
            UploadedAtUtc = DateTime.SpecifyKind(
                photo.UploadedAt,
                DateTimeKind.Utc),
            ExportedAtUtc = DateTime.UtcNow
        };

        var exportDirectory = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "serialized");

        Directory.CreateDirectory(exportDirectory);

        var fileName = $"photo-{photo.Id:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

        var filePath = Path.Combine(exportDirectory, fileName);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await using var fileStream = File.Create(filePath);

        await JsonSerializer.SerializeAsync(
            fileStream,
            snapshot,
            jsonOptions);

        return fileName;
    }
}
namespace Obrasci.Services;

public interface IPhotoSnapshotService
{
    Task<string?> ExportPhotoAsync(Guid photoId);
}
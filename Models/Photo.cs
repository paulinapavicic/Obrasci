namespace Obrasci.Models
{
    public class Photo
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public string StoragePath { get; set; } = default!;
        public long SizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? Description { get; set; }
        public string? Hashtags { get; set; }
        public string? ContentType { get; set; }
    }
}

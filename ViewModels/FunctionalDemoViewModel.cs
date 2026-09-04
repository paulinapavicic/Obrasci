using Microsoft.AspNetCore.Mvc;
using Obrasci.Models;

namespace Obrasci.ViewModels
{
    public class FunctionalDemoViewModel
    {
        public List<Photo> SamplePhotos { get; set; } = new();
        public List<Photo> FilteredPhotos { get; set; } = new();
        public List<string> FileNames { get; set; } = new();
        public long TotalBytes { get; set; }
        public List<string> ParsedHashtags { get; set; } = new();
        public Dictionary<string, int> CountByAuthor { get; set; } = new();
        public string FilterDescription { get; set; } = string.Empty;
    }
}
    


using Microsoft.AspNetCore.Mvc;
using Obrasci.Models;
using Obrasci.Services.Functional;
using Obrasci.ViewModels;

namespace Obrasci.Controllers
{
    public class FunctionalDemoController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            var user1 = new ApplicationUser { UserName = "anela" };
            var user2 = new ApplicationUser { UserName = "marko" };

            var photos = new List<Photo>
            {
                new Photo
                {
                    Id = Guid.NewGuid(),
                    FileName = "beach.jpg",
                    Description = "Summer beach photo",
                    Hashtags = "#summer #sea",
                    SizeBytes = 1200000,
                    UploadedAt = DateTime.UtcNow.AddDays(-1),
                    User = user1
                },
                new Photo
                {
                    Id = Guid.NewGuid(),
                    FileName = "mountain.png",
                    Description = "Mountain hike",
                    Hashtags = "#nature #hiking",
                    SizeBytes = 2200000,
                    UploadedAt = DateTime.UtcNow.AddDays(-3),
                    User = user2
                },
                new Photo
                {
                    Id = Guid.NewGuid(),
                    FileName = "city-night.jpg",
                    Description = "City lights at night",
                    Hashtags = "#city #night",
                    SizeBytes = 1800000,
                    UploadedAt = DateTime.UtcNow.AddDays(-2),
                    User = user1
                },
                new Photo
                {
                    Id = Guid.NewGuid(),
                    FileName = "forest.jpg",
                    Description = "Deep forest trail",
                    Hashtags = "#nature #forest",
                    SizeBytes = 950000,
                    UploadedAt = DateTime.UtcNow.AddDays(-5),
                    User = user2
                }
            };

            Func<Photo, bool> largePhoto = p => p.SizeBytes >= 1500000;
            Func<Photo, bool> natureTag = p => (p.Hashtags ?? "").Contains("nature", StringComparison.OrdinalIgnoreCase);
            var combined = PhotoFunctional.And(largePhoto, natureTag);

            var model = new FunctionalDemoViewModel
            {
                SamplePhotos = photos,
                FilteredPhotos = PhotoFunctional.FilterBy(photos, combined).ToList(),
                FileNames = PhotoFunctional.ToFileNames(photos).ToList(),
                TotalBytes = PhotoFunctional.TotalBytes(photos),
                ParsedHashtags = PhotoFunctional.ParseHashtags("#summer, beach #sea travel").ToList(),
                CountByAuthor = PhotoFunctional.CountByAuthor(photos).ToDictionary(k => k.Key, v => v.Value),
                FilterDescription = "Size >= 1,500,000 bytes AND hashtag contains 'nature'"
            };

            return View(model);
        }
    }
}
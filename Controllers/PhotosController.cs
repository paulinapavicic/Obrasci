using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.ViewModels;

namespace Obrasci.Controllers
{
   
    public class PhotosController : Controller
    {
        //Dependency Injection 
        private readonly IPhotoService _photoService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IActionLogger _actionLogger;

        public PhotosController(IPhotoService photoService, UserManager<ApplicationUser> userManager, ApplicationDbContext context, IActionLogger actionLogger)
        {
            _photoService = photoService;
            _userManager = userManager;
            _context = context;
            _actionLogger = actionLogger;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var photos = await _photoService.GetLastAsync(10);

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                ViewBag.Package = user?.Package.ToString();
                ViewBag.DailyCount = user?.DailyUploadCount ?? 0;
            }

            return View(photos);
        }


        public IActionResult Upload()
        {
            return View();
        }

        //Strategy pattern- image processing option
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile photoFile, string? description, string? hashtags, string? processingOption)
        {
            if (!User.Identity?.IsAuthenticated ?? false)
            {
                TempData["Error"] = "You must be logged in to upload photos.";
                return RedirectToAction("Login", "Account");
            }

            if (photoFile == null || photoFile.Length == 0)
            {
                ModelState.AddModelError("photoFile", "Photo file is required.");
            }

            if (!ModelState.IsValid)
                return View();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            user = await EnsurePackageUpToDate(user);

            //Adapter- for storing photos locally
            await _photoService.UploadAsync(user, photoFile, description, hashtags, processingOption);

            await _actionLogger.LogAsync(User,
                $"Uploaded photo '{photoFile.FileName}' (processing: {processingOption})");

            return RedirectToAction(nameof(Index));
        }


        private async Task<ApplicationUser> EnsurePackageUpToDate(ApplicationUser user)
        {
            var today = DateTime.UtcNow.Date;

            if (user.PendingPackage.HasValue &&
                user.PendingPackageEffectiveDate.HasValue &&
                user.PendingPackageEffectiveDate.Value.Date <= today)
            {
                user.Package = user.PendingPackage.Value;
                user.PendingPackage = null;
                user.PendingPackageEffectiveDate = null;

                await _userManager.UpdateAsync(user);
            }

            return user;
        }



        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid id)
        {
            var photo = await _photoService.GetByIdAsync(id);
            if (photo == null)
                return NotFound();

            return View(photo);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Search(string? hashtag, long? minSize, long? maxSize,
                                        DateTime? from, DateTime? to, string? author)
        {
            var results = await _photoService.SearchAsync(hashtag, minSize, maxSize, from, to, author);
            ViewBag.Hashtag = hashtag;
            ViewBag.MinSize = minSize;
            ViewBag.MaxSize = maxSize;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Author = author;

            return View(results);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Download(Guid id)
        {
            try
            {
                var (photo, bytes) = await _photoService.GetFileAsync(id);
                var contentType = photo.ContentType ?? "application/octet-stream";
                var downloadName = photo.FileName ?? "photo";

                return File(bytes, contentType, downloadName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

      
        [AllowAnonymous]
        public async Task<IActionResult> DownloadProcessed(Guid id, string option)
        {
            try
            {
                var (photo, bytes) = await _photoService.GetProcessedFileAsync(id, option);
                var contentType = "image/jpeg";
                var downloadName = Path.GetFileNameWithoutExtension(photo.FileName)
                                   + $"_{option}.jpg";
                return File(bytes, contentType, downloadName);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }


        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            var photo = await _context.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            if (photo.UserId != user!.Id) return Forbid();

            var model = new PhotoEditViewModel
            {
                Id = photo.Id,
                Description = photo.Description,
                Hashtags = photo.Hashtags
            };
            return View(model);
        }


        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PhotoEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            var photo = await _context.Photos.FindAsync(model.Id);
            if (photo == null) return NotFound();

            if (photo.UserId != user!.Id) return Forbid();

            photo.Description = model.Description;
            photo.Hashtags = model.Hashtags;

            await _context.SaveChangesAsync();

            await _actionLogger.LogAsync(User,
                $"Edited photo {photo.Id} (description/hashtags)");

            return RedirectToAction("Details", new { id = photo.Id });
        }


    }
}

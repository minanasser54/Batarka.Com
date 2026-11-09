using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

public class GalleryController : Controller
{
    private const int MaxImageSize = 10 * 1024 * 1024;
    private const int MaxImagesPerUpload = 20;
    private const long MaxZipSize = 100L * 1024 * 1024;

    private readonly ApplicationDbContext _db;

    public GalleryController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var albums = await _db.GalleryEvents
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new GalleryEventViewModel
            {
                Id = item.Id,
                Name = item.Name,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync();

        var photos = await _db.GalleryImages
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new GalleryImageViewModel
            {
                Id = item.Id,
                GalleryEventId = item.GalleryEventId,
                OriginalFileName = item.OriginalFileName,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync();

        var albumsById = albums.ToDictionary(item => item.Id);
        foreach (var grouping in photos.GroupBy(item => item.GalleryEventId))
        {
            if (albumsById.TryGetValue(grouping.Key, out var album))
                album.Images = grouping.OrderByDescending(item => item.CreatedAt).ToList();
        }

        if (!User.IsInRole("Admin"))
            albums = albums.Where(item => item.Images.Count > 0).ToList();

        ViewData["Title"] = "معرض الصور";
        return View(albums);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Image(int id)
    {
        var image = await _db.GalleryImages
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.ImageData, item.ContentType })
            .FirstOrDefaultAsync();
        if (image == null) return NotFound();

        Response.Headers.CacheControl = "public,max-age=3600";
        return File(image.ImageData, image.ContentType);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is < 1 or > 150)
        {
            TempData["ErrorMessage"] = "اكتب اسم مناسبة من 1 إلى 150 حرفًا.";
            return RedirectToAction(nameof(Index));
        }
        if (await _db.GalleryEvents.AnyAsync(item => item.Name == name))
        {
            TempData["ErrorMessage"] = "اسم المناسبة موجود بالفعل.";
            return RedirectToAction(nameof(Index));
        }

        _db.GalleryEvents.Add(new GalleryEvent { Name = name, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم إنشاء مجموعة الصور.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(220_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 220_000_000)]
    public async Task<IActionResult> Upload(int eventId, List<IFormFile>? images)
    {
        var galleryEvent = await _db.GalleryEvents.FindAsync(eventId);
        if (galleryEvent == null) return NotFound();
        if (images == null || images.Count == 0)
        {
            TempData["ErrorMessage"] = "اختار صورة واحدة على الأقل للرفع.";
            return RedirectToAction(nameof(Index));
        }
        if (images.Count > MaxImagesPerUpload)
        {
            TempData["ErrorMessage"] = $"يمكن رفع {MaxImagesPerUpload} صورة كحد أقصى في المرة الواحدة.";
            return RedirectToAction(nameof(Index));
        }

        var accepted = 0;
        var errors = new List<string>();
        foreach (var file in images)
        {
            if (file.Length is <= 0 or > MaxImageSize)
            {
                errors.Add($"{Path.GetFileName(file.FileName)}: الحجم يجب أن يكون أقل من 10 ميجابايت.");
                continue;
            }

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var bytes = stream.ToArray();
            var contentType = DetectImageContentType(Path.GetExtension(file.FileName), bytes);
            if (contentType == null)
            {
                errors.Add($"{Path.GetFileName(file.FileName)}: نوع الصورة غير مدعوم أو محتوى الملف لا يطابق امتداده.");
                continue;
            }

            var originalName = SafeFileName(file.FileName);
            if (originalName.Length > 255) originalName = originalName[..255];
            _db.GalleryImages.Add(new GalleryImage
            {
                GalleryEventId = galleryEvent.Id,
                OriginalFileName = originalName,
                ContentType = contentType,
                FileSize = bytes.Length,
                ImageData = bytes,
                CreatedAt = DateTime.UtcNow
            });
            accepted++;
        }

        if (accepted > 0) await _db.SaveChangesAsync();
        if (errors.Count > 0)
            TempData["WarningMessage"] = $"تم رفع {accepted} صورة. " + string.Join(" | ", errors.Take(5));
        else
            TempData["SuccessMessage"] = $"تم رفع {accepted} صورة إلى مجموعة {galleryEvent.Name}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await _db.GalleryImages.FindAsync(id);
        if (image == null) return NotFound();
        _db.GalleryImages.Remove(image);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم حذف الصورة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var galleryEvent = await _db.GalleryEvents.FindAsync(id);
        if (galleryEvent == null) return NotFound();
        _db.GalleryEvents.Remove(galleryEvent);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"تم حذف مجموعة {galleryEvent.Name} وصورها.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAll()
    {
        var events = await _db.GalleryEvents.ToListAsync();
        _db.GalleryEvents.RemoveRange(events);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم حذف كل المجموعات والصور.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DownloadAll()
    {
        var totalSize = await _db.GalleryImages.SumAsync(item => (long?)item.FileSize) ?? 0;
        if (totalSize > MaxZipSize)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "حجم الصور يتجاوز الحد المسموح لتنزيل ملف واحد.");

        var images = await _db.GalleryImages
            .AsNoTracking()
            .OrderBy(item => item.GalleryEvent.Name)
            .ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.OriginalFileName, item.ImageData, EventId = item.GalleryEvent.Id, EventName = item.GalleryEvent.Name })
            .ToListAsync();
        if (images.Count == 0)
        {
            TempData["WarningMessage"] = "لا توجد صور لتنزيلها.";
            return RedirectToAction(nameof(Index));
        }

        using var archiveStream = new MemoryStream((int)Math.Min(totalSize, int.MaxValue));
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var image in images)
            {
                var folder = SafeZipSegment(image.EventName, image.EventId);
                var fileName = SafeFileName(image.OriginalFileName);
                var entry = archive.CreateEntry($"{folder}/{image.Id}-{fileName}", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(image.ImageData);
            }
        }

        return File(archiveStream.ToArray(), "application/zip", $"gallery-{DateTime.UtcNow:yyyyMMdd}.zip");
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DownloadEvent(int id)
    {
        var galleryEvent = await _db.GalleryEvents.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (galleryEvent == null) return NotFound();

        var totalSize = await _db.GalleryImages.Where(item => item.GalleryEventId == id)
            .SumAsync(item => (long?)item.FileSize) ?? 0;
        if (totalSize > MaxZipSize)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "حجم صور المناسبة يتجاوز الحد المسموح لتنزيل ملف واحد.");

        var images = await _db.GalleryImages
            .AsNoTracking()
            .Where(item => item.GalleryEventId == id)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.OriginalFileName, item.ImageData })
            .ToListAsync();
        if (images.Count == 0)
        {
            TempData["WarningMessage"] = "لا توجد صور في هذه المجموعة.";
            return RedirectToAction(nameof(Index));
        }

        using var archiveStream = new MemoryStream((int)Math.Min(totalSize, int.MaxValue));
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var image in images)
            {
                var fileName = SafeFileName(image.OriginalFileName);
                var entry = archive.CreateEntry($"{image.Id}-{fileName}", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(image.ImageData);
            }
        }

        return File(archiveStream.ToArray(), "application/zip", $"{SafeZipSegment(galleryEvent.Name, galleryEvent.Id)}.zip");
    }

    private static string? DetectImageContentType(string extension, byte[] bytes)
    {
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF ? "image/jpeg" : null;

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 8 && bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png" : null;

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            var signature = bytes.Length >= 6 ? Encoding.ASCII.GetString(bytes, 0, 6) : string.Empty;
            return signature is "GIF87a" or "GIF89a" ? "image/gif" : null;
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return bytes.Length >= 12
                && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"
                && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP"
                ? "image/webp"
                : null;
        }

        return null;
    }

    private static string SafeFileName(string fileName)
    {
        var leaf = Path.GetFileName(fileName.Replace('\\', '/'));
        var safe = new string(leaf.Where(character => !char.IsControl(character) && !Path.GetInvalidFileNameChars().Contains(character)).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "image" : safe;
    }

    private static string SafeZipSegment(string name, int id)
    {
        var safe = new string(name.Where(character => !char.IsControl(character)
            && character != '/' && character != '\\'
            && !Path.GetInvalidFileNameChars().Contains(character)).ToArray()).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? $"event-{id}" : $"{id}-{safe}";
    }
}
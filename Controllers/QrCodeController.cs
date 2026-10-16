using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;
using QRCoder;

namespace MyMvcApp10.Controllers;

[Authorize]
public class QrCodeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public QrCodeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(user.QrCodeValue))
        {
            user.QrCodeValue = Guid.NewGuid().ToString("N");
            await _userManager.UpdateAsync(user);
        }

        return RenderQr(user.QrCodeValue);
    }

    [HttpGet]
    public async Task<IActionResult> ByUser(string id)
    {
        var actor = await _userManager.GetUserAsync(User);
        if (actor == null) return Forbid();

        var target = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (target == null) return NotFound();

        // Permission check:
        // 1. Admin can view anyone's QR (students, teachers, admins)
        // 2. Teacher can view any student's QR, or their own QR
        // 3. Student can only view their own QR
        if (actor.Id == target.Id)
        {
            // Allowed self
        }
        else if (actor.IsAdmin)
        {
            // Allowed admin
        }
        else if (actor.IsTeacher && !target.IsTeacher && !target.IsAdmin)
        {
            // Allowed teacher viewing student
        }
        else
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(target.QrCodeValue))
        {
            target.QrCodeValue = Guid.NewGuid().ToString("N");
            await _userManager.UpdateAsync(target);
        }

        return RenderQr(target.QrCodeValue);
    }

    private IActionResult RenderQr(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(10);

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return File(png, "image/png");
    }
}
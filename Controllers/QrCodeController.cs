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
        if (user == null || user.IsAdmin || user.IsTeacher || string.IsNullOrWhiteSpace(user.QrCodeValue))
        {
            return NotFound();
        }

        return RenderQr(user.QrCodeValue);
    }

    [HttpGet]
    public async Task<IActionResult> ByUser(string id)
    {
        var actor = await _userManager.GetUserAsync(User);
        if (actor == null || !actor.IsAdmin) return Forbid();

        var student = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsTeacher && !x.IsAdmin);
        if (student == null || string.IsNullOrWhiteSpace(student.QrCodeValue)) return NotFound();

        return RenderQr(student.QrCodeValue);
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
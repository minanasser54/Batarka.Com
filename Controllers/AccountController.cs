using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByNameAsync(model.UserName);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة أو الحساب متوقف.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = user.Id,
                Action = "LoginSucceeded",
                TargetType = "User",
                TargetId = user.Id,
                Details = "تسجيل دخول ناجح.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "الحساب تم إيقافه مؤقتًا بسبب محاولات دخول كثيرة. حاول مرة تانية بعد قليل.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult Profile()
    {
        return RedirectToAction("Profile", "Student");
    }


    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
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
            bool requiresPasswordChange = user.MustChangePassword || (!user.IsAdmin && user.LastLoginAt == null);
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

            if (requiresPasswordChange)
            {
                return RedirectToAction(nameof(FirstLoginChangePassword));
            }

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

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> FirstLoginChangePassword()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        ViewBag.FullName = user.FullName;
        ViewBag.IsFirstLogin = user.MustChangePassword || user.LastLoginAt == null;
        return View(new FirstLoginChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirstLoginChangePassword(FirstLoginChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        ViewBag.FullName = user.FullName;
        ViewBag.IsFirstLogin = user.MustChangePassword || user.LastLoginAt == null;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        await _signInManager.RefreshSignInAsync(user);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = user.Id,
            Action = "PasswordResetOnFirstLogin",
            TargetType = "User",
            TargetId = user.Id,
            Details = "تم تعيين كلمة المرور بنجاح.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم تعيين كلمة المرور بنجاح، مرحبًا بك!";
        return RedirectToAction("Index", "Home");
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
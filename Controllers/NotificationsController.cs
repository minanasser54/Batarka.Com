using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;
using MyMvcApp10.Services;

namespace MyMvcApp10.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // 1. User Notifications Inbox
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return RedirectToAction("Login", "Account");

        var userTeamId = await _db.TeamMembers
            .Where(tm => tm.StudentId == current.Id)
            .Select(tm => (int?)tm.TeamId)
            .FirstOrDefaultAsync();

        var notifications = await _db.AppNotifications
            .AsNoTracking()
            .Where(n =>
                n.TargetType == "All" ||
                (n.TargetType == "SpecificUser" && n.TargetUserId == current.Id) ||
                (n.TargetType == "Students" && !current.IsAdmin && !current.IsTeacher) ||
                (n.TargetType == "Teachers" && current.IsTeacher) ||
                (n.TargetType == "Admins" && current.IsAdmin) ||
                (n.TargetType == "SpecificTeam" && userTeamId.HasValue && n.TargetTeamId == userTeamId.Value))
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        var readNotificationIds = await _db.UserNotificationReads
            .AsNoTracking()
            .Where(r => r.UserId == current.Id)
            .Select(r => r.NotificationId)
            .ToListAsync();

        ViewBag.ReadIds = readNotificationIds;
        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id, string? returnUrl = null)
    {
        var current = await GetCurrentUserAsync();
        if (current == null)
        {
            if (IsAjaxRequest()) return Json(new { success = false });
            return RedirectToAction("Login", "Account");
        }

        if (!await _db.UserNotificationReads.AnyAsync(r => r.NotificationId == id && r.UserId == current.Id))
        {
            _db.UserNotificationReads.Add(new UserNotificationRead
            {
                NotificationId = id,
                UserId = current.Id,
                ReadAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        if (IsAjaxRequest())
        {
            return Json(new { success = true });
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return RedirectToAction("Login", "Account");

        var notif = await _db.AppNotifications.FindAsync(id);
        if (notif == null) return RedirectToAction(nameof(Index));

        if (!await _db.UserNotificationReads.AnyAsync(r => r.NotificationId == id && r.UserId == current.Id))
        {
            _db.UserNotificationReads.Add(new UserNotificationRead
            {
                NotificationId = id,
                UserId = current.Id,
                ReadAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(notif.LinkUrl) && Url.IsLocalUrl(notif.LinkUrl))
        {
            return Redirect(notif.LinkUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return RedirectToAction(nameof(Index));

        var userTeamId = await _db.TeamMembers
            .Where(tm => tm.StudentId == current.Id)
            .Select(tm => (int?)tm.TeamId)
            .FirstOrDefaultAsync();

        var notificationIds = await _db.AppNotifications
            .Where(n =>
                n.TargetType == "All" ||
                (n.TargetType == "SpecificUser" && n.TargetUserId == current.Id) ||
                (n.TargetType == "Students" && !current.IsAdmin && !current.IsTeacher) ||
                (n.TargetType == "Teachers" && current.IsTeacher) ||
                (n.TargetType == "Admins" && current.IsAdmin) ||
                (n.TargetType == "SpecificTeam" && userTeamId.HasValue && n.TargetTeamId == userTeamId.Value))
            .Select(n => n.Id)
            .ToListAsync();

        var existingReads = await _db.UserNotificationReads
            .Where(r => r.UserId == current.Id && notificationIds.Contains(r.NotificationId))
            .Select(r => r.NotificationId)
            .ToListAsync();

        var unreadIds = notificationIds.Except(existingReads).ToList();
        foreach (var id in unreadIds)
        {
            _db.UserNotificationReads.Add(new UserNotificationRead
            {
                NotificationId = id,
                UserId = current.Id,
                ReadAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم تحديد جميع الإشعارات كمقروءة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return Json(new { count = 0 });

        var count = await _notificationService.GetUnreadCountAsync(current.Id, current.IsAdmin, current.IsTeacher);
        return Json(new { count });
    }

    // -------------------------------------------------------------
    // ADMIN NOTIFICATIONS MANAGEMENT (Requirement 13)
    // -------------------------------------------------------------

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        var notifications = await _db.AppNotifications
            .Include(n => n.CreatedBy)
            .Include(n => n.TargetUser)
            .Include(n => n.TargetTeam)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        return View(notifications);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Users = await _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.UserName, Role = u.IsAdmin ? "مشرف" : (u.IsTeacher ? "خادم" : "مخدوم") })
            .ToListAsync();

        ViewBag.Teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        return View(new CreateCustomNotificationViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCustomNotificationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Users = await _db.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
            return View(model);
        }

        var current = await GetCurrentUserAsync();
        await _notificationService.SendNotificationAsync(
            model.Title.Trim(),
            model.Message.Trim(),
            "Custom",
            model.TargetType,
            targetUserId: model.TargetType == "SpecificUser" ? model.TargetUserId : null,
            targetTeamId: model.TargetType == "SpecificTeam" ? model.TargetTeamId : null,
            linkUrl: model.LinkUrl?.Trim(),
            createdById: current?.Id
        );

        TempData["SuccessMessage"] = "تم إرسال الإشعار بنجاح.";
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var notification = await _db.AppNotifications.FindAsync(id);
        if (notification != null)
        {
            _db.AppNotifications.Remove(notification);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الإشعار.";
        }
        return RedirectToAction(nameof(Manage));
    }

    private bool IsAjaxRequest()
    {
        return Request.Headers.XRequestedWith == "XMLHttpRequest" ||
               Request.Headers.Accept.ToString().Contains("application/json");
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.UserName == username);
    }
}

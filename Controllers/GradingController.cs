using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

[Authorize]
public class GradingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GradingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? searchTerm, int? schoolLevelId, int? schoolClassId, string? qrCode)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var query = _db.Users
            .Include(x => x.SchoolClass).ThenInclude(x => x!.SchoolLevel)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(qrCode))
        {
            var qr = qrCode.Trim();
            query = query.Where(u => u.QrCodeValue == qr);
        }
        else if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(u =>
                u.QrCodeValue == term ||
                u.FullName.Contains(term) ||
                (u.UserName != null && u.UserName.Contains(term)) ||
                (u.Email != null && u.Email.Contains(term)) ||
                (u.Mobile != null && u.Mobile.Contains(term)) ||
                (u.SchoolClass != null && u.SchoolClass.Name.Contains(term)) ||
                (u.SchoolLevel != null && u.SchoolLevel.Name.Contains(term)));
        }

        if (schoolLevelId.HasValue) query = query.Where(u => u.SchoolLevelId == schoolLevelId.Value);
        if (schoolClassId.HasValue) query = query.Where(u => u.SchoolClassId == schoolClassId.Value);

        var students = await query
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
            .OrderBy(u => u.FullName)
            .Take(50)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        ViewBag.QrCode = qrCode;
        ViewBag.SelectedLevelId = schoolLevelId;
        ViewBag.SelectedClassId = schoolClassId;
        ViewBag.Levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        ViewBag.Classes = await _db.SchoolClasses.Include(x => x.SchoolLevel).OrderBy(x => x.SchoolLevel.SortOrder).ThenBy(x => x.Name).ToListAsync();
        return View(students);
    }

    [HttpGet]
    public async Task<IActionResult> LiveSearch(string? searchTerm, int? schoolLevelId, int? schoolClassId, string? qrCode)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var query = _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher);

        if (!string.IsNullOrWhiteSpace(qrCode))
        {
            var qr = qrCode.Trim();
            query = query.Where(u => u.QrCodeValue == qr);
        }
        else if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(u =>
                u.QrCodeValue == term ||
                u.FullName.Contains(term) ||
                (u.UserName != null && u.UserName.Contains(term)) ||
                (u.Mobile != null && u.Mobile.Contains(term)) ||
                (u.Email != null && u.Email.Contains(term)));
        }

        if (schoolLevelId.HasValue) query = query.Where(u => u.SchoolLevelId == schoolLevelId.Value);
        if (schoolClassId.HasValue) query = query.Where(u => u.SchoolClassId == schoolClassId.Value);

        var students = await query
            .OrderBy(u => u.FullName)
            .Take(50)
            .Select(u => new GradingStudentDto
            {
                Id = u.Id,
                FullName = u.FullName,
                UserName = u.UserName,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                LevelName = u.SchoolLevel != null ? u.SchoolLevel.Name : u.Level,
                GradingBalance = u.GradingBalance,
                QrCodeValue = u.QrCodeValue
            })
            .ToListAsync();

        return Json(students);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(string studentId, string action)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                      (Request.Headers.Accept.ToString()?.Contains("application/json") ?? false);

        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return isAjax ? Json(new { success = false, message = "غير مصرح لك." }) : Forbid();
        }

        var actionMap = new Dictionary<string, int>
        {
            ["green"] = 1,
            ["red"] = -1,
            ["black"] = -5
        };

        if (!actionMap.TryGetValue(action, out var delta))
        {
            if (isAjax) return Json(new { success = false, message = "إجراء الدرجات غير صالح." });
            TempData["ErrorMessage"] = "إجراء الدرجات غير صالح.";
            return RedirectToAction(nameof(Index));
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.IsActive && !u.IsAdmin && !u.IsTeacher);
        if (student == null)
        {
            if (isAjax) return Json(new { success = false, message = "المخدوم غير موجود أو غير نشط." });
            TempData["ErrorMessage"] = "المخدوم غير موجود أو غير نشط.";
            return RedirectToAction(nameof(Index));
        }

        var key = $"{studentId}:{current.Id}:{action}:{DateTime.UtcNow:yyyyMMddHHmmss}";
        var transaction = new GradingTransaction
        {
            StudentId = student.Id,
            TeacherId = current.Id,
            PointsDelta = delta,
            Reason = action switch
            {
                "green" => "أخضر: +1 نقطة",
                "red" => "أحمر: -1 نقطة",
                "black" => "أسود: -5 نقاط",
                _ => "إجراء يدوي"
            },
            SourceType = "Manual",
            IdempotencyKey = key,
            CreatedAt = DateTime.UtcNow
        };

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var existing = await _db.GradingTransactions.AnyAsync(x => x.IdempotencyKey == transaction.IdempotencyKey);
            if (existing)
            {
                if (isAjax) return Json(new { success = false, message = "تم رفض التكرار نفسه للدرجات." });
                TempData["WarningMessage"] = "تم رفض التكرار نفسه للدرجات.";
                return RedirectToAction(nameof(Index));
            }

            student.GradingBalance += delta;
            _db.GradingTransactions.Add(transaction);
            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = current.Id,
                Action = "GradingApplied",
                TargetType = "Student",
                TargetId = student.Id,
                Details = $"{student.FullName}: {transaction.Reason}",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            if (isAjax)
            {
                return Json(new { success = true, newBalance = student.GradingBalance, message = $"تم تطبيق {transaction.Reason} بنجاح." });
            }

            TempData["SuccessMessage"] = $"تم تطبيق {transaction.Reason} بنجاح.";
        }
        catch
        {
            await tx.RollbackAsync();
            if (isAjax) return Json(new { success = false, message = "فشل تطبيق العملية. حاول مرة أخرى." });
            TempData["ErrorMessage"] = "فشل تطبيق العملية. حاول مرة أخرى.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return await _db.Users.FirstOrDefaultAsync(x => x.UserName == username);
    }
}
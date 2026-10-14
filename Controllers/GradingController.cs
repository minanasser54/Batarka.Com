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

    public async Task<IActionResult> Index(string? searchTerm)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(u => u.FullName.Contains(term) || (u.UserName != null && u.UserName.Contains(term)));
        }

        var students = await query
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
            .Take(25)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return View(students);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(string studentId, string action)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var actionMap = new Dictionary<string, int>
        {
            ["green"] = 1,
            ["red"] = -1,
            ["black"] = -5
        };

        if (!actionMap.TryGetValue(action, out var delta))
        {
            TempData["ErrorMessage"] = "إجراء الدرجات غير صالح.";
            return RedirectToAction(nameof(Index));
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Id == studentId && u.IsActive);
        if (student == null)
        {
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
                Details = transaction.Reason,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["SuccessMessage"] = $"تم تطبيق {transaction.Reason} بنجاح.";
        }
        catch
        {
            await tx.RollbackAsync();
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
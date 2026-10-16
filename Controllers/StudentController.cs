using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

[Authorize]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _db;

    public StudentController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Dashboard()
    {
        var user = await GetCurrentUserAsync();
        if (user == null || user.IsAdmin || user.IsTeacher)
        {
            return Forbid();
        }

        var attendanceCount = await _db.AttendanceRecords.CountAsync(x => x.UserId == user.Id && !x.IsDeleted);
        var latestAttendance = await _db.AttendanceRecords
            .Where(x => x.UserId == user.Id && !x.IsDeleted)
            .OrderByDescending(x => x.AttendanceTimeUtc)
            .Select(x => x.AttendanceTimeUtc)
            .FirstOrDefaultAsync();

        var recentTransactions = await _db.GradingTransactions
            .Where(x => x.StudentId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToListAsync();

        var records = await _db.AttendanceRecords
            .Where(x => x.UserId == user.Id && !x.IsDeleted)
            .OrderByDescending(x => x.AttendanceTimeUtc)
            .Take(10)
            .ToListAsync();

        ViewBag.User = user;
        ViewBag.AttendanceCount = attendanceCount;
        ViewBag.LatestAttendance = latestAttendance;
        ViewBag.RecentTransactions = recentTransactions;
        ViewBag.Records = records;

        return View();
    }

    public async Task<IActionResult> Profile(string? id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account");
        }

        ApplicationUser? targetUser;
        if (string.IsNullOrWhiteSpace(id) || id == currentUser.Id)
        {
            targetUser = currentUser;
        }
        else
        {
            targetUser = await _db.Users
                .Include(u => u.SchoolClass)
                .Include(u => u.SchoolLevel)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (targetUser == null)
            {
                return NotFound();
            }

            // Authorization rules:
            // 1. Admin can view ANY user's profile (student, teacher, admin)
            // 2. Teacher can view ANY student profile (not other teachers/admins)
            // 3. Student can ONLY view their own profile
            if (currentUser.IsAdmin)
            {
                // Allowed admin
            }
            else if (currentUser.IsTeacher && !targetUser.IsTeacher && !targetUser.IsAdmin)
            {
                // Allowed teacher viewing student
            }
            else
            {
                return Forbid();
            }
        }

        // Ensure QR code value exists
        if (string.IsNullOrWhiteSpace(targetUser.QrCodeValue))
        {
            targetUser.QrCodeValue = Guid.NewGuid().ToString("N");
            _db.Users.Update(targetUser);
            await _db.SaveChangesAsync();
        }

        var transactions = await _db.GradingTransactions
            .Where(x => x.StudentId == targetUser.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Take(30)
            .ToListAsync();

        var attendance = await _db.AttendanceRecords
            .Where(x => x.UserId == targetUser.Id && !x.IsDeleted)
            .OrderByDescending(x => x.AttendanceTimeUtc)
            .Take(30)
            .ToListAsync();

        var totalAttendanceCount = await _db.AttendanceRecords
            .CountAsync(x => x.UserId == targetUser.Id && !x.IsDeleted);

        ViewBag.Transactions = transactions;
        ViewBag.Attendance = attendance;
        ViewBag.TotalAttendanceCount = totalAttendanceCount;
        ViewBag.IsSelf = targetUser.Id == currentUser.Id;
        ViewBag.CurrentUser = currentUser;

        return View(targetUser);
    }

    public async Task<IActionResult> Leaderboard()
    {
        var leaderboard = await _db.Users
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
            .OrderByDescending(u => u.GradingBalance)
            .ThenBy(u => u.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View(leaderboard);
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return await _db.Users
            .Include(x => x.SchoolClass)
            .Include(x => x.SchoolLevel)
            .FirstOrDefaultAsync(x => x.UserName == username);
    }
}
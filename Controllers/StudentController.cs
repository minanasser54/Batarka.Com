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

    public async Task<IActionResult> Profile()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!user.IsAdmin && !user.IsTeacher && user.Id != User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
        {
            return Forbid();
        }

        var transactions = await _db.GradingTransactions
            .Where(x => x.StudentId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var attendance = await _db.AttendanceRecords
            .Where(x => x.UserId == user.Id && !x.IsDeleted)
            .OrderByDescending(x => x.AttendanceTimeUtc)
            .ToListAsync();

        ViewBag.Transactions = transactions;
        ViewBag.Attendance = attendance;
        return View(user);
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

        return await _db.Users.FirstOrDefaultAsync(x => x.UserName == username);
    }
}
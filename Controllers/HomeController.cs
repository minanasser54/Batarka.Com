using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUserName = User.Identity.Name;
            var user = string.IsNullOrWhiteSpace(currentUserName)
                ? null
                : await _db.Users.FirstOrDefaultAsync(x => x.UserName == currentUserName);

            if (user == null) return RedirectToAction("Login", "Account");
            if (user.IsAdmin) return RedirectToAction("Index", "Admin");
            if (user.IsTeacher) return RedirectToAction("Index", "Attendance");
            return RedirectToAction("Dashboard", "Student");
        }

        var topStudents = await _db.Users
            .Where(x => x.IsActive && !x.IsAdmin && !x.IsTeacher)
            .OrderByDescending(x => x.GradingBalance)
            .ThenBy(x => x.FullName)
            .Take(10)
            .ToListAsync();

        ViewData["Title"] = "أسرة بطاركة - مدارس الأحد";
        return View(new HomeLandingViewModel { TopStudents = topStudents });
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "غير مصرح بالدخول";
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

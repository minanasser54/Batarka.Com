using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user == null || (!user.IsAdmin && !user.IsTeacher))
        {
            return Forbid();
        }

        var todayUtc = DateTime.UtcNow;
        var team = await _db.Users.Where(x => x.IsActive).ToListAsync();
        var students = team.Where(x => !x.IsTeacher && !x.IsAdmin).ToList();
        var teachers = team.Where(x => x.IsTeacher || x.IsAdmin).ToList();

        var model = new
        {
            TotalStudents = students.Count,
            TotalTeachers = teachers.Count,
            AttendanceToday = _db.AttendanceRecords.Count(x => x.AttendanceTimeUtc.Date == todayUtc.Date),
            AbsentToday = Math.Max(0, students.Count - _db.AttendanceRecords.Where(x => x.AttendanceTimeUtc.Date == todayUtc.Date).Select(x => x.UserId).Distinct().Count()),
            TotalPointsAwarded = await _db.GradingTransactions.SumAsync(x => (int?)x.PointsDelta) ?? 0,
            AttendanceRegistrations = await _db.AttendanceRecords.CountAsync(x => x.AttendanceTimeUtc.Date == todayUtc.Date)
        };

        ViewBag.Model = model;
        ViewData["Title"] = "لوحة التحكم";
        return View();
    }

    public async Task<IActionResult> Students(string? search, int? schoolClassId, int? schoolLevelId, string? className, string? level, string? activeStatus)
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var query = _db.Users.Where(u => !u.IsAdmin && !u.IsTeacher);
        if (activeStatus == "active") query = query.Where(u => u.IsActive);
        if (activeStatus == "inactive") query = query.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(u => u.FullName.Contains(q) || (u.UserName != null && u.UserName.Contains(q)));
        }

        if (schoolClassId.HasValue)
        {
            query = query.Where(u => u.SchoolClassId == schoolClassId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(className))
        {
            query = query.Where(u => u.ClassName == className);
        }

        if (schoolLevelId.HasValue)
        {
            query = query.Where(u => u.SchoolLevelId == schoolLevelId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(u => u.Level == level);
        }

        var students = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminStudentListItemViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                UserName = u.UserName ?? string.Empty,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                Level = u.SchoolLevel != null ? u.SchoolLevel.Name : u.Level,
                SchoolLevelId = u.SchoolLevelId,
                GradingBalance = u.GradingBalance,
                TotalAttendance = _db.AttendanceRecords.Count(ar => ar.UserId == u.Id),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        ViewBag.Classes = await _db.SchoolClasses.Include(x => x.SchoolLevel).OrderBy(x => x.SchoolLevel.SortOrder).ThenBy(x => x.Name).ToListAsync();
        ViewBag.Levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        ViewBag.SelectedClassId = schoolClassId;
        ViewBag.SelectedLevelId = schoolLevelId;
        return View(students);
    }

    public async Task<IActionResult> Teachers()
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var teachers = await _db.Users
            .Where(u => u.IsTeacher || u.IsAdmin)
            .OrderByDescending(u => u.LastLoginAt)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.UserName,
                u.IsActive,
                u.IsTeacher,
                u.IsAdmin,
                u.CreatedAt,
                u.LastLoginAt
            })
            .ToListAsync();

        return View(teachers);
    }

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        await LoadSchoolOptionsAsync();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(RegisterViewModel model)
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        var existing = await _userManager.FindByNameAsync(model.UserName);
        if (existing != null)
        {
            ModelState.AddModelError(string.Empty, "اسم المستخدم موجود بالفعل.");
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        var schoolClass = model.SchoolClassId.HasValue
            ? await _db.SchoolClasses.Include(x => x.SchoolLevel).FirstOrDefaultAsync(x => x.Id == model.SchoolClassId.Value)
            : null;
        if (model.SchoolClassId.HasValue && (schoolClass == null || schoolClass.SchoolLevelId != model.SchoolLevelId))
        {
            ModelState.AddModelError(nameof(model.SchoolClassId), "الفصل مش تابع للمرحلة المختارة.");
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        var schoolLevel = model.SchoolLevelId.HasValue
            ? await _db.SchoolLevels.FindAsync(model.SchoolLevelId.Value)
            : null;

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.UserName,
            FullName = model.FullName,
            SchoolClassId = schoolClass?.Id,
            ClassName = schoolClass?.Name,
            SchoolLevelId = schoolLevel?.Id,
            Level = schoolLevel?.Name,
            IsTeacher = model.IsTeacher,
            IsAdmin = model.IsAdmin,
            IsActive = model.IsActive,
            QrCodeValue = Guid.NewGuid().ToString("N"),
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        if (user.IsTeacher) await _userManager.AddToRoleAsync(user, "Teacher");
        if (user.IsAdmin) await _userManager.AddToRoleAsync(user, "Admin");

        TempData["SuccessMessage"] = "تم إنشاء الحساب بنجاح.";
        return RedirectToAction(nameof(Students));
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        if (!await IsAdminAsync()) return Forbid();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        await LoadSchoolOptionsAsync();
        return View(new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Mobile = user.Mobile,
            Address = user.Address,
            Notes = user.Notes,
            SchoolClassId = user.SchoolClassId,
            SchoolLevelId = user.SchoolLevelId,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        var nameOwner = await _userManager.FindByNameAsync(model.UserName);
        if (nameOwner != null && nameOwner.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم مستخدم لحساب تاني.");
        }

        var schoolClass = model.SchoolClassId.HasValue
            ? await _db.SchoolClasses.FirstOrDefaultAsync(x => x.Id == model.SchoolClassId.Value)
            : null;
        var schoolLevel = model.SchoolLevelId.HasValue
            ? await _db.SchoolLevels.FirstOrDefaultAsync(x => x.Id == model.SchoolLevelId.Value)
            : null;
        if (model.SchoolClassId.HasValue && (schoolClass == null || schoolClass.SchoolLevelId != model.SchoolLevelId))
        {
            ModelState.AddModelError(nameof(model.SchoolClassId), "الفصل مش تابع للمرحلة المختارة.");
        }
        if (model.SchoolLevelId.HasValue && schoolLevel == null)
        {
            ModelState.AddModelError(nameof(model.SchoolLevelId), "المرحلة المختارة مش موجودة.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.UserName = model.UserName.Trim();
        user.Email = model.UserName.Trim();
        user.Mobile = model.Mobile?.Trim();
        user.Address = model.Address?.Trim();
        user.Notes = model.Notes?.Trim();
        user.SchoolClassId = schoolClass?.Id;
        user.ClassName = schoolClass?.Name;
        user.SchoolLevelId = schoolLevel?.Id;
        user.Level = schoolLevel?.Name;
        user.IsActive = model.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedById = actor.Id;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = "UserUpdated",
            TargetType = "User",
            TargetId = user.Id,
            Details = "تم تحديث بيانات الحساب من الإدارة.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم تحديث بيانات الحساب.";
        return RedirectToAction(user.IsTeacher || user.IsAdmin ? nameof(Teachers) : nameof(Students));
    }

    public async Task<IActionResult> Settings()
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var settings = await _db.ApplicationSettings.ToDictionaryAsync(x => x.Key, x => x.Value);
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(string timezone, bool negativeBalanceAllowed)
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        await SaveSettingAsync("Timezone", timezone);
        await SaveSettingAsync("NegativeBalanceAllowed", negativeBalanceAllowed ? "true" : "false");

        TempData["SuccessMessage"] = "تم تحديث إعدادات النظام.";
        return RedirectToAction(nameof(Settings));
    }

    public async Task<IActionResult> Audit()
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var logs = await _db.AuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        return View(logs);
    }

    public async Task<IActionResult> Reports()
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var today = DateTime.UtcNow.Date;
        var model = new
        {
            AttendanceRate = 0,
            TodayRecords = await _db.AttendanceRecords.CountAsync(x => x.AttendanceTimeUtc.Date == today),
            TotalStudents = await _db.Users.CountAsync(u => !u.IsAdmin && !u.IsTeacher),
            TotalTeachers = await _db.Users.CountAsync(u => u.IsTeacher || u.IsAdmin)
        };

        ViewBag.Model = model;
        return View();
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

    private async Task<bool> IsAdminAsync()
    {
        var user = await GetCurrentUserAsync();
        return user != null && user.IsAdmin;
    }

    private async Task SaveSettingAsync(string key, string value)
    {
        var setting = await _db.ApplicationSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (setting == null)
        {
            _db.ApplicationSettings.Add(new ApplicationSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    private async Task LoadSchoolOptionsAsync()
    {
        ViewBag.Classes = await _db.SchoolClasses
            .Include(x => x.SchoolLevel)
            .OrderBy(x => x.SchoolLevel.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        ViewBag.Levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
    }
}

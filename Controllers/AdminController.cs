using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using MyMvcApp10.Data;
using MyMvcApp10.Models;
using System.Text;

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
                Email = u.Email,
                Mobile = u.Mobile,
                Address = u.Address,
                Notes = u.Notes,
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
                u.LastLoginAt,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                Level = u.SchoolClass != null ? u.SchoolClass.SchoolLevel.Name : u.Level
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
        if (model.SchoolClassId.HasValue && schoolClass == null)
        {
            ModelState.AddModelError(nameof(model.SchoolClassId), "الفصل المختار مش موجود.");
            await LoadSchoolOptionsAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.UserName,
            FullName = model.FullName,
            SchoolClassId = schoolClass?.Id,
            ClassName = schoolClass?.Name,
            SchoolLevelId = schoolClass?.SchoolLevelId,
            Level = schoolClass?.SchoolLevel.Name,
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

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = (await GetCurrentUserAsync())!.Id,
            Action = "UserCreated",
            TargetType = "User",
            TargetId = user.Id,
            Details = $"تم إنشاء حساب {(user.IsTeacher ? "خادم" : "مخدوم")}: {user.FullName}",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

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
            SchoolLevelId = user.SchoolClassId.HasValue ? user.SchoolLevelId : null,
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
            ? await _db.SchoolClasses.Include(x => x.SchoolLevel).FirstOrDefaultAsync(x => x.Id == model.SchoolClassId.Value)
            : null;
        if (model.SchoolClassId.HasValue && schoolClass == null)
        {
            ModelState.AddModelError(nameof(model.SchoolClassId), "الفصل المختار مش موجود.");
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
        user.SchoolLevelId = schoolClass?.SchoolLevelId;
        user.Level = schoolClass?.SchoolLevel.Name;
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

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        if (!await IsAdminAsync()) return Forbid();
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(new ResetPasswordViewModel { UserId = user.Id, FullName = user.FullName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null) return NotFound();
        model.FullName = user.FullName;
        if (!ModelState.IsValid) return View(model);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = "PasswordReset",
            TargetType = "User",
            TargetId = user.Id,
            Details = "إعادة تعيين كلمة المرور بواسطة الإدارة.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم تغيير كلمة المرور.";
        return RedirectToAction(user.IsTeacher || user.IsAdmin ? nameof(Teachers) : nameof(Students));
    }

    [HttpGet]
    public async Task<IActionResult> Questions()
    {
        if (!await IsAdminAsync()) return Forbid();
        ViewBag.AttendancePoints = await GetAttendancePointsAsync();
        return View(await _db.AttendanceQuestionDefinitions.OrderBy(x => x.SortOrder).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuestion(AttendanceQuestionEditViewModel model)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();

        model.Code = model.Code.Trim();
        model.DisplayText = model.DisplayText.Trim();
        if (model.StartDayOfMonth.HasValue && model.EndDayOfMonth.HasValue && model.StartDayOfMonth == model.EndDayOfMonth)
        {
            ModelState.AddModelError(nameof(model.EndDayOfMonth), "اختار يوم نهاية مختلف، أو سيب اليومين فاضيين للسؤال الدائم.");
        }
        if (await _db.AttendanceQuestionDefinitions.AnyAsync(x => x.Code == model.Code && x.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Code), "الكود مستخدم لسؤال تاني.");
        }
        if (!ModelState.IsValid)
        {
            ViewBag.AttendancePoints = await GetAttendancePointsAsync();
            return View("Questions", await _db.AttendanceQuestionDefinitions.OrderBy(x => x.SortOrder).ToListAsync());
        }

        var question = model.Id == 0
            ? new AttendanceQuestionDefinition()
            : await _db.AttendanceQuestionDefinitions.FindAsync(model.Id);
        if (question == null) return NotFound();

        question.Code = model.Code;
        question.DisplayText = model.DisplayText;
        question.PointsAwarded = model.PointsAwarded;
        question.IsEnabled = model.IsEnabled;
        question.SortOrder = model.SortOrder;
        question.StartDayOfMonth = model.StartDayOfMonth;
        question.EndDayOfMonth = model.EndDayOfMonth;
        question.UpdatedAt = DateTime.UtcNow;
        if (model.Id == 0) _db.AttendanceQuestionDefinitions.Add(question);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = model.Id == 0 ? "QuestionCreated" : "QuestionUpdated",
            TargetType = "AttendanceQuestionDefinition",
            TargetId = model.Id.ToString(),
            Details = $"تحديث سؤال: {question.DisplayText}، النقاط: {question.PointsAwarded}",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم حفظ السؤال ونقاطه.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAttendancePoints(int attendancePoints)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();
        if (attendancePoints is < 0 or > 1000)
        {
            TempData["ErrorMessage"] = "نقاط الحضور لازم تكون بين 0 و1000.";
            return RedirectToAction(nameof(Questions));
        }

        await SaveSettingAsync("AttendancePoints", attendancePoints.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = "AttendancePointsUpdated",
            TargetType = "ApplicationSetting",
            TargetId = "AttendancePoints",
            Details = $"نقاط الحضور: {attendancePoints}",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم حفظ نقاط الحضور.";
        return RedirectToAction(nameof(Questions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int id)
    {
        if (!await IsAdminAsync()) return Forbid();
        var question = await _db.AttendanceQuestionDefinitions.FindAsync(id);
        if (question == null) return NotFound();
        if (await _db.AttendanceQuestionAnswers.AnyAsync(x => x.QuestionId == id))
        {
            question.IsEnabled = false;
            question.UpdatedAt = DateTime.UtcNow;
            TempData["WarningMessage"] = "السؤال له إجابات محفوظة، فتم إيقافه بدل حذفه.";
        }
        else
        {
            _db.AttendanceQuestionDefinitions.Remove(question);
            TempData["SuccessMessage"] = "تم حذف السؤال.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Questions));
    }

    [HttpGet]
    public async Task<IActionResult> BulkStudents(int? schoolClassId)
    {
        if (!await IsAdminAsync()) return Forbid();
        await LoadSchoolOptionsAsync();
        ViewBag.SelectedClassId = schoolClassId;
        ViewBag.ClassStudents = schoolClassId.HasValue
            ? await _db.Users.Where(x => x.SchoolClassId == schoolClassId && !x.IsTeacher && !x.IsAdmin).OrderBy(x => x.FullName).ToListAsync()
            : new List<ApplicationUser>();
        return View(new BulkStudentImportViewModel { SchoolClassId = schoolClassId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStudents(BulkStudentImportViewModel model)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();
        var schoolClass = await _db.SchoolClasses.Include(x => x.SchoolLevel).FirstOrDefaultAsync(x => x.Id == model.SchoolClassId);
        if (schoolClass == null) ModelState.AddModelError(nameof(model.SchoolClassId), "الفصل المختار مش موجود.");
        if (!ModelState.IsValid)
        {
            await LoadBulkStudentsViewAsync(model);
            return View("BulkStudents", model);
        }

        var errors = new List<string>();
        var created = 0;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(model.Rows));
        using var parser = new TextFieldParser(stream, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");
        var rowNumber = 0;
        while (!parser.EndOfData)
        {
            rowNumber++;
            var fields = parser.ReadFields() ?? Array.Empty<string>();
            if (fields.Length < 3 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]) || string.IsNullOrWhiteSpace(fields[2]))
            {
                errors.Add($"السطر {rowNumber}: مطلوب الاسم واسم المستخدم وكلمة المرور.");
                continue;
            }
            if (await _userManager.FindByNameAsync(fields[1]) != null)
            {
                errors.Add($"السطر {rowNumber}: اسم المستخدم {fields[1]} موجود بالفعل.");
                continue;
            }

            var student = new ApplicationUser
            {
                FullName = fields[0],
                UserName = fields[1],
                Email = fields.Length > 4 && !string.IsNullOrWhiteSpace(fields[4]) ? fields[4] : $"{fields[1]}@students.local",
                Mobile = fields.Length > 3 ? fields[3] : null,
                SchoolClassId = schoolClass!.Id,
                ClassName = schoolClass.Name,
                SchoolLevelId = schoolClass.SchoolLevelId,
                Level = schoolClass.SchoolLevel.Name,
                IsActive = true,
                EmailConfirmed = true,
                QrCodeValue = Guid.NewGuid().ToString("N")
            };
            var result = await _userManager.CreateAsync(student, fields[2]);
            if (!result.Succeeded)
            {
                errors.Add($"السطر {rowNumber}: {string.Join("؛ ", result.Errors.Select(x => x.Description))}");
                continue;
            }
            var roleResult = await _userManager.AddToRoleAsync(student, "Student");
            if (!roleResult.Succeeded)
            {
                student.IsActive = false;
                await _userManager.UpdateAsync(student);
                errors.Add($"السطر {rowNumber}: الحساب اتعمل لكن تعذر تعيين دوره.");
                continue;
            }
            created++;
        }

        if (created > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = actor.Id,
                Action = "BulkStudentsImported",
                TargetType = "SchoolClass",
                TargetId = schoolClass!.Id.ToString(),
                Details = $"إضافة {created} مخدوم للفصل {schoolClass.Name}",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        TempData["SuccessMessage"] = $"تمت إضافة {created} مخدوم.";
        if (errors.Count > 0)
        {
            await LoadBulkStudentsViewAsync(model);
            ViewBag.ImportErrors = errors;
            return View("BulkStudents", model);
        }
        return RedirectToAction(nameof(BulkStudents), new { schoolClassId = schoolClass!.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDeactivateStudents(int schoolClassId, List<string>? studentIds)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();
        studentIds ??= new List<string>();
        var students = await _db.Users
            .Where(x => x.SchoolClassId == schoolClassId && studentIds.Contains(x.Id) && !x.IsTeacher && !x.IsAdmin && x.IsActive)
            .ToListAsync();
        foreach (var student in students)
        {
            student.IsActive = false;
            student.UpdatedAt = DateTime.UtcNow;
            student.UpdatedById = actor.Id;
            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = actor.Id,
                Action = "StudentDeactivated",
                TargetType = "User",
                TargetId = student.Id,
                Details = $"تعطيل حساب {student.FullName} ضمن إجراء جماعي.",
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"تم تعطيل {students.Count} حساب. سجل الحضور والدرجات محفوظين.";
        return RedirectToAction(nameof(BulkStudents), new { schoolClassId });
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

    public async Task<IActionResult> Audit(string? actionName, string? search, DateTime? from, DateTime? to)
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var query = _db.AuditLogs.Include(x => x.Actor).AsQueryable();
        if (!string.IsNullOrWhiteSpace(actionName)) query = query.Where(x => x.Action == actionName);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                (x.Actor != null && ((x.Actor.FullName != null && x.Actor.FullName.Contains(term)) || (x.Actor.UserName != null && x.Actor.UserName.Contains(term)))) ||
                (x.TargetType != null && x.TargetType.Contains(term)) ||
                (x.TargetId != null && x.TargetId.Contains(term)) ||
                (x.Details != null && x.Details.Contains(term)) ||
                (x.Reason != null && x.Reason.Contains(term)));
        }

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new AdminAuditListItemViewModel
            {
                Id = x.Id,
                Action = x.Action,
                ActorName = x.Actor != null ? x.Actor.FullName : null,
                ActorUserName = x.Actor != null ? x.Actor.UserName : null,
                TargetType = x.TargetType,
                TargetId = x.TargetId,
                Details = x.Details,
                Reason = x.Reason,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        ViewBag.ActionName = actionName;
        ViewBag.Search = search;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.ActionOptions = await _db.AuditLogs.Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync();
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

    private async Task<int> GetAttendancePointsAsync()
    {
        var value = await _db.ApplicationSettings
            .Where(x => x.Key == "AttendancePoints")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
        return int.TryParse(value, out var points) ? points : 1;
    }

    private async Task LoadBulkStudentsViewAsync(BulkStudentImportViewModel model)
    {
        await LoadSchoolOptionsAsync();
        ViewBag.SelectedClassId = model.SchoolClassId;
        ViewBag.ClassStudents = model.SchoolClassId > 0
            ? await _db.Users.Where(x => x.SchoolClassId == model.SchoolClassId && !x.IsTeacher && !x.IsAdmin).OrderBy(x => x.FullName).ToListAsync()
            : new List<ApplicationUser>();
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

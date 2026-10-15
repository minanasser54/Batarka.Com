using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers;

[Authorize(Roles = "Admin")]
public class SchoolStructureController : Controller
{
    private readonly ApplicationDbContext _db;

    public SchoolStructureController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Levels()
    {
        var levels = await _db.SchoolLevels
            .Include(x => x.Classes)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        return View(levels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLevel(string name, int sortOrder)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 120)
        {
            TempData["ErrorMessage"] = "اكتب اسم مرحلة صحيح.";
            return RedirectToAction(nameof(Levels));
        }
        if (await _db.SchoolLevels.AnyAsync(x => x.Name == name))
        {
            TempData["ErrorMessage"] = "اسم المرحلة موجود بالفعل.";
            return RedirectToAction(nameof(Levels));
        }

        _db.SchoolLevels.Add(new SchoolLevel { Name = name, SortOrder = sortOrder });
        await _db.SaveChangesAsync();
        await LogAdminActionAsync("SchoolLevelCreated", "SchoolLevel", "تمت إضافة مرحلة: " + name);
        TempData["SuccessMessage"] = "تمت إضافة المرحلة.";
        return RedirectToAction(nameof(Levels));
    }

    [HttpGet]
    public async Task<IActionResult> EditLevel(int id)
    {
        var level = await _db.SchoolLevels.FindAsync(id);
        return level == null ? NotFound() : View(level);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLevel(int id, string name, int sortOrder)
    {
        name = (name ?? string.Empty).Trim();
        var level = await _db.SchoolLevels.FindAsync(id);
        if (level == null) return NotFound();
        if (name.Length == 0 || name.Length > 120 || await _db.SchoolLevels.AnyAsync(x => x.Id != id && x.Name == name))
        {
            ModelState.AddModelError(nameof(name), "اسم المرحلة مطلوب، ولازم يكون مختلف عن المراحل التانية.");
            level.Name = name;
            level.SortOrder = sortOrder;
            return View(level);
        }

        level.Name = name;
        level.SortOrder = sortOrder;
        await _db.SaveChangesAsync();
        await _db.Users.Where(x => x.SchoolLevelId == id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Level, name));
        await LogAdminActionAsync("SchoolLevelUpdated", "SchoolLevel", $"تعديل المرحلة إلى: {name}", id.ToString());
        TempData["SuccessMessage"] = "تم تعديل بيانات المرحلة.";
        return RedirectToAction(nameof(Levels));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLevel(int id)
    {
        var level = await _db.SchoolLevels
            .Include(x => x.Classes)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (level == null) return NotFound();
        if (level.Classes.Count > 0 || await _db.Users.AnyAsync(x => x.SchoolLevelId == id))
        {
            TempData["ErrorMessage"] = "مينفعش تمسح مرحلة مرتبطة بفصول أو مخدومين. انقلهم الأول.";
            return RedirectToAction(nameof(Levels));
        }

        _db.SchoolLevels.Remove(level);
        await _db.SaveChangesAsync();
        await LogAdminActionAsync("SchoolLevelDeleted", "SchoolLevel", "حذف مرحلة: " + level.Name, id.ToString());
        TempData["SuccessMessage"] = "تم حذف المرحلة.";
        return RedirectToAction(nameof(Levels));
    }

    [HttpGet]
    public async Task<IActionResult> Classes(int? levelId)
    {
        var query = _db.SchoolClasses.Include(x => x.SchoolLevel).AsQueryable();
        if (levelId.HasValue) query = query.Where(x => x.SchoolLevelId == levelId.Value);
        ViewBag.Levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        ViewBag.SelectedLevelId = levelId;
        return View(await query.OrderBy(x => x.SchoolLevel.SortOrder).ThenBy(x => x.Name).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClass(string name, int schoolLevelId)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 120 || !await _db.SchoolLevels.AnyAsync(x => x.Id == schoolLevelId))
        {
            TempData["ErrorMessage"] = "اكتب اسم فصل صحيح واختار المرحلة.";
            return RedirectToAction(nameof(Classes));
        }
        if (await _db.SchoolClasses.AnyAsync(x => x.SchoolLevelId == schoolLevelId && x.Name == name))
        {
            TempData["ErrorMessage"] = "اسم الفصل موجود بالفعل في المرحلة دي.";
            return RedirectToAction(nameof(Classes), new { levelId = schoolLevelId });
        }

        _db.SchoolClasses.Add(new SchoolClass { Name = name, SchoolLevelId = schoolLevelId });
        await _db.SaveChangesAsync();
        await LogAdminActionAsync("SchoolClassCreated", "SchoolClass", $"إضافة فصل {name} للمرحلة {schoolLevelId}");
        TempData["SuccessMessage"] = "تمت إضافة الفصل.";
        return RedirectToAction(nameof(Classes), new { levelId = schoolLevelId });
    }

    [HttpGet]
    public async Task<IActionResult> EditClass(int id)
    {
        var schoolClass = await _db.SchoolClasses.FindAsync(id);
        if (schoolClass == null) return NotFound();
        await LoadLevelsAsync();
        return View(schoolClass);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditClass(int id, string name, int schoolLevelId)
    {
        name = (name ?? string.Empty).Trim();
        var schoolClass = await _db.SchoolClasses.FindAsync(id);
        if (schoolClass == null) return NotFound();
        var targetLevel = await _db.SchoolLevels.FirstOrDefaultAsync(x => x.Id == schoolLevelId);
        var duplicate = await _db.SchoolClasses.AnyAsync(x => x.Id != id && x.SchoolLevelId == schoolLevelId && x.Name == name);
        if (name.Length == 0 || name.Length > 120 || targetLevel == null || duplicate)
        {
            ModelState.AddModelError(nameof(name), "اسم الفصل والمرحلة مطلوبين، والاسم لازم يكون مختلف جوه المرحلة.");
            schoolClass.Name = name;
            schoolClass.SchoolLevelId = schoolLevelId;
            await LoadLevelsAsync();
            return View(schoolClass);
        }

        schoolClass.Name = name;
        schoolClass.SchoolLevelId = schoolLevelId;
        await _db.SaveChangesAsync();
        await _db.Users.Where(x => x.SchoolClassId == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.ClassName, name)
            .SetProperty(x => x.SchoolLevelId, schoolLevelId)
            .SetProperty(x => x.Level, targetLevel.Name));
        await LogAdminActionAsync("SchoolClassUpdated", "SchoolClass", $"نقل أو تعديل فصل {name} للمرحلة {targetLevel.Name}", id.ToString());
        TempData["SuccessMessage"] = "تم تعديل بيانات الفصل.";
        return RedirectToAction(nameof(Classes), new { levelId = schoolLevelId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClass(int id)
    {
        var schoolClass = await _db.SchoolClasses.FindAsync(id);
        if (schoolClass == null) return NotFound();
        if (await _db.Users.AnyAsync(x => x.SchoolClassId == id))
        {
            TempData["ErrorMessage"] = "مينفعش تمسح فصل فيه مخدومين. انقلهم لفصل تاني الأول.";
            return RedirectToAction(nameof(Classes), new { levelId = schoolClass.SchoolLevelId });
        }

        var levelId = schoolClass.SchoolLevelId;
        _db.SchoolClasses.Remove(schoolClass);
        await _db.SaveChangesAsync();
        await LogAdminActionAsync("SchoolClassDeleted", "SchoolClass", "حذف فصل: " + schoolClass.Name, id.ToString());
        TempData["SuccessMessage"] = "تم حذف الفصل.";
        return RedirectToAction(nameof(Classes), new { levelId });
    }

    [HttpGet]
    public async Task<IActionResult> ClassStudents(int id)
    {
        var schoolClass = await _db.SchoolClasses.Include(x => x.SchoolLevel).FirstOrDefaultAsync(x => x.Id == id);
        if (schoolClass == null) return NotFound();
        ViewBag.ClassTitle = schoolClass.Name;
        ViewBag.LevelTitle = schoolClass.SchoolLevel.Name;
        ViewBag.ClassId = id;
        return View(await GetClassStudentsAsync(id));
    }

    [HttpGet]
    public async Task<IActionResult> ExportClassStudents(int id)
    {
        var schoolClass = await _db.SchoolClasses.FirstOrDefaultAsync(x => x.Id == id);
        if (schoolClass == null) return NotFound();
        var students = await GetClassStudentsAsync(id);
        var csv = new StringBuilder();
        csv.AppendLine("الاسم,اسم المستخدم,البريد,الموبايل,العنوان,ملاحظات,الفصل,المرحلة,الرصيد,عدد مرات الحضور,الحالة,تاريخ الإنشاء");
        foreach (var student in students)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(student.FullName), Csv(student.UserName), Csv(student.Email), Csv(student.Mobile), Csv(student.Address), Csv(student.Notes), Csv(student.ClassName), Csv(student.Level),
                Csv(student.GradingBalance.ToString(CultureInfo.InvariantCulture)),
                Csv(student.TotalAttendance.ToString(CultureInfo.InvariantCulture)),
                Csv(student.IsActive ? "نشط" : "متوقف"),
                Csv(student.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            }));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"students-class-{id}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> LevelStudents(int id)
    {
        var level = await _db.SchoolLevels.FindAsync(id);
        if (level == null) return NotFound();
        ViewBag.LevelTitle = level.Name;
        ViewBag.LevelId = id;
        return View(await GetLevelStudentsAsync(id));
    }

    [HttpGet]
    public async Task<IActionResult> ExportLevelStudents(int id)
    {
        var level = await _db.SchoolLevels.FindAsync(id);
        if (level == null) return NotFound();
        var students = await GetLevelStudentsAsync(id);
        var csv = new StringBuilder();
        csv.AppendLine("الاسم,اسم المستخدم,البريد,الموبايل,العنوان,ملاحظات,الفصل,المرحلة,الرصيد,عدد مرات الحضور,الحالة,تاريخ الإنشاء");
        foreach (var student in students)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(student.FullName), Csv(student.UserName), Csv(student.Email), Csv(student.Mobile), Csv(student.Address), Csv(student.Notes), Csv(student.ClassName), Csv(student.Level),
                Csv(student.GradingBalance.ToString(CultureInfo.InvariantCulture)),
                Csv(student.TotalAttendance.ToString(CultureInfo.InvariantCulture)),
                Csv(student.IsActive ? "نشط" : "متوقف"),
                Csv(student.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            }));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"students-level-{id}.csv");
    }

    private Task<List<AdminStudentListItemViewModel>> GetLevelStudentsAsync(int id)
    {
        return _db.Users
            .Where(x => x.SchoolLevelId == id && !x.IsTeacher && !x.IsAdmin)
            .OrderBy(x => x.SchoolClass!.Name)
            .ThenBy(x => x.FullName)
            .Select(x => new AdminStudentListItemViewModel
            {
                UserId = x.Id,
                FullName = x.FullName,
                UserName = x.UserName ?? string.Empty,
                Email = x.Email,
                Mobile = x.Mobile,
                Address = x.Address,
                Notes = x.Notes,
                ClassName = x.SchoolClass != null ? x.SchoolClass.Name : x.ClassName,
                Level = x.SchoolLevel != null ? x.SchoolLevel.Name : x.Level,
                SchoolLevelId = x.SchoolLevelId,
                GradingBalance = x.GradingBalance,
                TotalAttendance = _db.AttendanceRecords.Count(record => record.UserId == x.Id && !record.IsDeleted),
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    private Task<List<AdminStudentListItemViewModel>> GetClassStudentsAsync(int id)
    {
        return _db.Users
            .Where(x => x.SchoolClassId == id && !x.IsTeacher && !x.IsAdmin)
            .OrderBy(x => x.FullName)
            .Select(x => new AdminStudentListItemViewModel
            {
                UserId = x.Id,
                FullName = x.FullName,
                UserName = x.UserName ?? string.Empty,
                Email = x.Email,
                Mobile = x.Mobile,
                Address = x.Address,
                Notes = x.Notes,
                ClassName = x.SchoolClass != null ? x.SchoolClass.Name : x.ClassName,
                Level = x.SchoolLevel != null ? x.SchoolLevel.Name : x.Level,
                SchoolLevelId = x.SchoolLevelId,
                GradingBalance = x.GradingBalance,
                TotalAttendance = _db.AttendanceRecords.Count(record => record.UserId == x.Id && !record.IsDeleted),
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    private async Task LoadLevelsAsync()
    {
        ViewBag.Levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
    }

    private async Task LogAdminActionAsync(string action, string targetType, string details, string? targetId = null)
    {
        var userName = User.Identity?.Name;
        var actor = string.IsNullOrWhiteSpace(userName)
            ? null
            : await _db.Users.FirstOrDefaultAsync(x => x.UserName == userName);
        if (actor == null) return;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
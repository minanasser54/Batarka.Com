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
        var actor = await GetCurrentUserAsync();
        if (actor == null || (!actor.IsAdmin && !actor.IsTeacher))
        {
            return Forbid();
        }

        var query = _db.Users.Where(u => !u.IsAdmin && !u.IsTeacher && u.DeletedAt == null);
        if (activeStatus == "active") query = query.Where(u => u.IsActive);
        if (activeStatus == "inactive") query = query.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(u => u.FullName.Contains(q) || (u.UserName != null && u.UserName.Contains(q)) || (u.Mobile != null && u.Mobile.Contains(q)) || (u.Address != null && u.Address.Contains(q)));
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
                Mobile2 = u.Mobile2,
                Landline = u.Landline,
                Address = u.Address,
                StreetNumber = u.StreetNumber,
                ApartmentNumber = u.ApartmentNumber,
                Building = u.Building,
                Landmark = u.Landmark,
                AddressNotes = u.AddressNotes,
                BirthDay = u.BirthDay,
                BirthMonth = u.BirthMonth,
                BirthYear = u.BirthYear,
                ConfessionFather = u.ConfessionFather,
                StudyStage = u.StudyStage,
                SchoolOrUniversity = u.SchoolOrUniversity,
                Gender = u.Gender,
                Occupation = u.Occupation,
                FatherOccupation = u.FatherOccupation,
                MotherOccupation = u.MotherOccupation,
                MotherName = u.MotherName,
                OtherServices = u.OtherServices,
                NationalId = u.NationalId,
                ImageUrl = u.ImageUrl,
                AreaCode = u.AreaCode,
                Notes = u.Notes,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                Level = u.SchoolLevel != null ? u.SchoolLevel.Name : u.Level,
                SchoolLevelId = u.SchoolLevelId,
                GradingBalance = u.GradingBalance,
                TotalAttendance = _db.AttendanceRecords.Count(ar => ar.UserId == u.Id),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                TeamName = _db.TeamMembers.Where(tm => tm.StudentId == u.Id).Select(tm => tm.Team.Name).FirstOrDefault()
            })
            .ToListAsync();

        ViewBag.Classes = await _db.SchoolClasses.Include(x => x.SchoolLevel).OrderBy(x => x.SchoolLevel.SortOrder).ThenBy(x => x.Name).ToListAsync();
        ViewBag.Levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        ViewBag.SelectedClassId = schoolClassId;
        ViewBag.SelectedLevelId = schoolLevelId;
        ViewBag.IsAdmin = actor.IsAdmin;
        ViewBag.IsTeacher = actor.IsTeacher;
        return View(students);
    }


    [HttpGet]
    public async Task<IActionResult> ExportStudentsCsv(string? search, int? schoolClassId, int? schoolLevelId, string? className, string? level, string? activeStatus)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || (!actor.IsAdmin && !actor.IsTeacher))
        {
            return Forbid();
        }

        var query = _db.Users.Where(u => !u.IsAdmin && !u.IsTeacher && u.DeletedAt == null);
        if (activeStatus == "active") query = query.Where(u => u.IsActive);
        if (activeStatus == "inactive") query = query.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(u => u.FullName.Contains(q)
                || (u.UserName != null && u.UserName.Contains(q))
                || (u.Mobile != null && u.Mobile.Contains(q))
                || (u.Address != null && u.Address.Contains(q)));
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

        var list = await query
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.FullName,
                UserName = u.UserName ?? string.Empty,
                u.Email,
                u.StreetNumber,
                u.Address,
                u.ApartmentNumber,
                u.Building,
                u.Landmark,
                u.AddressNotes,
                u.Landline,
                u.Mobile,
                u.Mobile2,
                u.BirthDay,
                u.BirthMonth,
                u.BirthYear,
                u.ConfessionFather,
                u.StudyStage,
                u.SchoolOrUniversity,
                u.Gender,
                u.Occupation,
                u.FatherOccupation,
                u.MotherOccupation,
                u.MotherName,
                u.OtherServices,
                u.NationalId,
                u.ImageUrl,
                u.AreaCode,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                Level = u.SchoolLevel != null ? u.SchoolLevel.Name : u.Level,
                u.GradingBalance,
                TotalAttendance = _db.AttendanceRecords.Count(ar => ar.UserId == u.Id),
                TeamName = _db.TeamMembers.Where(tm => tm.StudentId == u.Id).Select(tm => tm.Team.Name).FirstOrDefault(),
                u.Notes,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("الاسم,اسم المستخدم,البريد,رقم الشارع,العنوان,رقم الشقة,العمارة,علامة مميزة,ملاحظات العنوان,التليفون الأرضي,موبايل 1,موبايل 2,يوم الميلاد,شهر الميلاد,سنة الميلاد,أب الاعتراف,المرحلة الدراسية (0-9),المدرسة / الجامعة,النوع (ولد / بنت),المهنة,مهنة الأب,مهنة الأم,اسم الأم,خدمات أخرى,الرقم القومي,رابط الصورة الشخصية,ملاحظات,كود المنطقة,المرحلة,الفصل,الفريق,الرصيد,مرات الحضور,الحالة,تاريخ التسجيل");
        foreach (var item in list)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                CsvEscape(item.FullName),
                CsvEscape(item.UserName),
                CsvEscape(item.Email),
                CsvEscape(item.StreetNumber),
                CsvEscape(item.Address),
                CsvEscape(item.ApartmentNumber),
                CsvEscape(item.Building),
                CsvEscape(item.Landmark),
                CsvEscape(item.AddressNotes),
                CsvEscape(item.Landline),
                CsvEscape(item.Mobile),
                CsvEscape(item.Mobile2),
                CsvEscape(item.BirthDay?.ToString()),
                CsvEscape(item.BirthMonth?.ToString()),
                CsvEscape(item.BirthYear?.ToString()),
                CsvEscape(item.ConfessionFather),
                CsvEscape(item.StudyStage?.ToString()),
                CsvEscape(item.SchoolOrUniversity),
                CsvEscape(item.Gender),
                CsvEscape(item.Occupation),
                CsvEscape(item.FatherOccupation),
                CsvEscape(item.MotherOccupation),
                CsvEscape(item.MotherName),
                CsvEscape(item.OtherServices),
                CsvEscape(item.NationalId),
                CsvEscape(item.ImageUrl),
                CsvEscape(item.Notes),
                CsvEscape(item.AreaCode),
                CsvEscape(item.Level),
                CsvEscape(item.ClassName),
                CsvEscape(item.TeamName),
                item.GradingBalance.ToString(),
                item.TotalAttendance.ToString(),
                item.IsActive ? "نشط" : "متوقف",
                item.CreatedAt.ToString("yyyy-MM-dd")
            }));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"students-info-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string CsvEscape(string? value)
    {
        var escaped = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string NormalizeCsvHeader(string? header)
    {
        return (header ?? string.Empty).Trim().TrimStart('\uFEFF').Replace(" ", string.Empty).Replace("ـ", string.Empty).ToLowerInvariant();
    }

    private static string? CsvValue(IReadOnlyDictionary<string, int> headerMap, string[] fields, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (headerMap.TryGetValue(NormalizeCsvHeader(alias), out var index) && index < fields.Length)
            {
                var value = fields[index]?.Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }

        return null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    [HttpGet]
    public async Task<IActionResult> StudentInfo(string? search, int? schoolClassId, int? schoolLevelId, string? activeStatus)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || (!actor.IsAdmin && !actor.IsTeacher))
        {
            return Forbid();
        }

        var query = _db.Users.Where(u => !u.IsAdmin && !u.IsTeacher && u.DeletedAt == null);
        if (activeStatus == "active") query = query.Where(u => u.IsActive);
        if (activeStatus == "inactive") query = query.Where(u => !u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(u => u.FullName.Contains(q)
                || (u.UserName != null && u.UserName.Contains(q))
                || (u.Mobile != null && u.Mobile.Contains(q))
                || (u.Address != null && u.Address.Contains(q)));
        }

        if (schoolClassId.HasValue) query = query.Where(u => u.SchoolClassId == schoolClassId.Value);
        if (schoolLevelId.HasValue) query = query.Where(u => u.SchoolLevelId == schoolLevelId.Value);

        var students = await query
            .OrderBy(u => u.FullName)
            .Select(u => new AdminStudentListItemViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email,
                Mobile = u.Mobile,
                Mobile2 = u.Mobile2,
                Landline = u.Landline,
                Address = u.Address,
                StreetNumber = u.StreetNumber,
                ApartmentNumber = u.ApartmentNumber,
                Building = u.Building,
                Landmark = u.Landmark,
                AddressNotes = u.AddressNotes,
                BirthDay = u.BirthDay,
                BirthMonth = u.BirthMonth,
                BirthYear = u.BirthYear,
                ConfessionFather = u.ConfessionFather,
                StudyStage = u.StudyStage,
                SchoolOrUniversity = u.SchoolOrUniversity,
                Gender = u.Gender,
                Occupation = u.Occupation,
                FatherOccupation = u.FatherOccupation,
                MotherOccupation = u.MotherOccupation,
                MotherName = u.MotherName,
                OtherServices = u.OtherServices,
                NationalId = u.NationalId,
                ImageUrl = u.ImageUrl,
                AreaCode = u.AreaCode,
                Notes = u.Notes,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                Level = u.SchoolLevel != null ? u.SchoolLevel.Name : u.Level,
                SchoolLevelId = u.SchoolLevelId,
                GradingBalance = u.GradingBalance,
                TotalAttendance = _db.AttendanceRecords.Count(ar => ar.UserId == u.Id),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                TeamName = _db.TeamMembers.Where(tm => tm.StudentId == u.Id).Select(tm => tm.Team.Name).FirstOrDefault()
            })
            .ToListAsync();

        var levels = await _db.SchoolLevels.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        var classes = await _db.SchoolClasses.Include(x => x.SchoolLevel).OrderBy(x => x.SchoolLevel.SortOrder).ThenBy(x => x.Name).ToListAsync();

        var model = new StudentContactDirectoryViewModel
        {
            Students = students,
            Levels = levels,
            Classes = classes,
            Search = search,
            SchoolLevelId = schoolLevelId,
            SchoolClassId = schoolClassId,
            ActiveStatus = activeStatus,
            TotalCount = students.Count,
            WithMobileCount = students.Count(s => !string.IsNullOrWhiteSpace(s.Mobile)),
            WithAddressCount = students.Count(s => !string.IsNullOrWhiteSpace(s.Address))
        };

        ViewBag.IsAdmin = actor.IsAdmin;
        ViewBag.IsTeacher = actor.IsTeacher;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> BulkEditStudents(string? search, int? schoolClassFilterId, string? activeStatusFilter)
    {
        if (!await IsAdminAsync()) return Forbid();
        return await LoadBulkEditUsersAsync(new BulkEditUsersViewModel
        {
            Search = search,
            SchoolClassFilterId = schoolClassFilterId,
            ActiveStatusFilter = activeStatusFilter
        }, isTeacher: false);
    }

    [HttpGet]
    public async Task<IActionResult> BulkEditTeachers(string? search, int? schoolClassFilterId, string? activeStatusFilter)
    {
        if (!await IsAdminAsync()) return Forbid();
        return await LoadBulkEditUsersAsync(new BulkEditUsersViewModel
        {
            Search = search,
            SchoolClassFilterId = schoolClassFilterId,
            ActiveStatusFilter = activeStatusFilter
        }, isTeacher: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateBulkEditStudent(BulkEditUserListItemViewModel model) => SaveBulkEditUserRowAsync(model, isTeacher: false);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateBulkEditTeacher(BulkEditUserListItemViewModel model) => SaveBulkEditUserRowAsync(model, isTeacher: true);

    private async Task<IActionResult> SaveBulkEditUserRowAsync(BulkEditUserListItemViewModel model, bool isTeacher)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();

        var user = await _db.Users.FirstOrDefaultAsync(item => item.Id == model.Id && item.DeletedAt == null);
        if (user == null) return NotFound();
        if (user.IsTeacher != isTeacher || user.IsAdmin) return Forbid();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.FullName))
        {
            TempData["ErrorMessage"] = "تحقق من الاسم والحقول الرقمية قبل الحفظ.";
            return RedirectToBulkEditList(model, isTeacher);
        }

        if (model.BirthDay is < 1 or > 31 || model.BirthMonth is < 1 or > 12
            || model.BirthYear is < 1800 or > 2100 || model.StudyStage is < 0 or > 9)
        {
            TempData["ErrorMessage"] = "تحقق من يوم وشهر وسنة الميلاد والمرحلة الدراسية.";
            return RedirectToBulkEditList(model, isTeacher);
        }

        SchoolClass? schoolClass = null;
        if (model.SchoolClassId.HasValue)
        {
            schoolClass = await _db.SchoolClasses.Include(item => item.SchoolLevel)
                .FirstOrDefaultAsync(item => item.Id == model.SchoolClassId.Value);
            if (schoolClass == null)
            {
                TempData["ErrorMessage"] = "الفصل المختار غير موجود.";
                return RedirectToBulkEditList(model, isTeacher);
            }
        }

        user.FullName = model.FullName.Trim();
        user.SchoolClassId = schoolClass?.Id;
        user.ClassName = schoolClass?.Name;
        user.SchoolLevelId = schoolClass?.SchoolLevelId;
        user.Level = schoolClass?.SchoolLevel.Name;
        user.IsActive = model.IsActive;
        user.Mobile = model.Mobile?.Trim();
        user.Mobile2 = model.Mobile2?.Trim();
        user.Landline = model.Landline?.Trim();
        user.Address = model.Address?.Trim();
        user.StreetNumber = model.StreetNumber?.Trim();
        user.ApartmentNumber = model.ApartmentNumber?.Trim();
        user.Building = model.Building?.Trim();
        user.Landmark = model.Landmark?.Trim();
        user.AddressNotes = model.AddressNotes?.Trim();
        user.BirthDay = model.BirthDay;
        user.BirthMonth = model.BirthMonth;
        user.BirthYear = model.BirthYear;
        user.ConfessionFather = model.ConfessionFather?.Trim();
        user.StudyStage = model.StudyStage;
        user.SchoolOrUniversity = model.SchoolOrUniversity?.Trim();
        user.Gender = model.Gender?.Trim();
        user.Occupation = model.Occupation?.Trim();
        user.FatherOccupation = model.FatherOccupation?.Trim();
        user.MotherOccupation = model.MotherOccupation?.Trim();
        user.MotherName = model.MotherName?.Trim();
        user.OtherServices = model.OtherServices?.Trim();
        user.NationalId = model.NationalId?.Trim();
        user.ImageUrl = model.ImageUrl?.Trim();
        user.Notes = model.Notes?.Trim();
        user.AreaCode = model.AreaCode?.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedById = actor.Id;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join("؛ ", result.Errors.Select(error => error.Description));
            return RedirectToBulkEditList(model, isTeacher);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = isTeacher ? "TeacherUpdated" : "StudentUpdated",
            TargetType = "User",
            TargetId = user.Id,
            Details = "تم تحديث بيانات الحساب من جدول التعديل الجماعي.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"تم تحديث بيانات {user.FullName}.";
        return RedirectToBulkEditList(model, isTeacher);
    }

    private IActionResult RedirectToBulkEditList(BulkEditUserListItemViewModel model, bool isTeacher)
    {
        return RedirectToAction(isTeacher ? nameof(BulkEditTeachers) : nameof(BulkEditStudents), new
        {
            search = model.Search,
            schoolClassFilterId = model.SchoolClassFilterId,
            activeStatusFilter = model.ActiveStatusFilter
        });
    }

    private async Task<IActionResult> LoadBulkEditUsersAsync(BulkEditUsersViewModel model, bool isTeacher)
    {
        model.IsTeacher = isTeacher;
        var query = _db.Users.Where(user => user.DeletedAt == null && (isTeacher ? user.IsTeacher && !user.IsAdmin : !user.IsTeacher && !user.IsAdmin));
        if (model.ActiveStatusFilter == "active") query = query.Where(user => user.IsActive);
        if (model.ActiveStatusFilter == "inactive") query = query.Where(user => !user.IsActive);
        if (model.SchoolClassFilterId.HasValue) query = query.Where(user => user.SchoolClassId == model.SchoolClassFilterId.Value);
        if (!string.IsNullOrWhiteSpace(model.Search))
        {
            var term = model.Search.Trim();
            query = query.Where(user => user.FullName.Contains(term) || (user.UserName != null && user.UserName.Contains(term))
                || (user.Mobile != null && user.Mobile.Contains(term)));
        }

        model.Users = await query.OrderBy(user => user.FullName)
            .Select(user => new BulkEditUserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                SchoolClassId = user.SchoolClassId,
                IsActive = user.IsActive,
                Mobile = user.Mobile,
                Mobile2 = user.Mobile2,
                Landline = user.Landline,
                Address = user.Address,
                StreetNumber = user.StreetNumber,
                ApartmentNumber = user.ApartmentNumber,
                Building = user.Building,
                Landmark = user.Landmark,
                AddressNotes = user.AddressNotes,
                BirthDay = user.BirthDay,
                BirthMonth = user.BirthMonth,
                BirthYear = user.BirthYear,
                ConfessionFather = user.ConfessionFather,
                StudyStage = user.StudyStage,
                SchoolOrUniversity = user.SchoolOrUniversity,
                Gender = user.Gender,
                Occupation = user.Occupation,
                FatherOccupation = user.FatherOccupation,
                MotherOccupation = user.MotherOccupation,
                MotherName = user.MotherName,
                OtherServices = user.OtherServices,
                NationalId = user.NationalId,
                ImageUrl = user.ImageUrl,
                Notes = user.Notes,
                AreaCode = user.AreaCode
            })
            .ToListAsync();
        ViewBag.Classes = await _db.SchoolClasses.Include(item => item.SchoolLevel)
            .OrderBy(item => item.SchoolLevel.SortOrder).ThenBy(item => item.Name).ToListAsync();
        return View("BulkEditUsers", model);
    }
    [HttpGet]
    public IActionResult Questionnaires()
    {
        return RedirectToAction("QuestionnaireAnswers", "Attendance");
    }


    public async Task<IActionResult> Teachers(string? search, string? activeStatus, string? accountType)
    {
        if (!await IsAdminAsync())
        {
            return Forbid();
        }

        var query = _db.Users.Where(u => (u.IsTeacher || u.IsAdmin) && u.DeletedAt == null);
        if (activeStatus == "active") query = query.Where(u => u.IsActive);
        if (activeStatus == "inactive") query = query.Where(u => !u.IsActive);
        if (accountType == "teacher") query = query.Where(u => u.IsTeacher && !u.IsAdmin);
        if (accountType == "admin") query = query.Where(u => u.IsAdmin);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || (u.UserName != null && u.UserName.Contains(term))
                || (u.Mobile != null && u.Mobile.Contains(term)));
        }

        var teachers = await query
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

        ViewBag.Search = search;
        ViewBag.ActiveStatus = activeStatus;
        ViewBag.AccountType = accountType;
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
            StreetNumber = model.StreetNumber?.Trim(),
            Address = model.Address?.Trim(),
            ApartmentNumber = model.ApartmentNumber?.Trim(),
            Building = model.Building?.Trim(),
            Landmark = model.Landmark?.Trim(),
            AddressNotes = model.AddressNotes?.Trim(),
            Landline = model.Landline?.Trim(),
            Mobile = model.Mobile?.Trim(),
            Mobile2 = model.Mobile2?.Trim(),
            BirthDay = model.BirthDay,
            BirthMonth = model.BirthMonth,
            BirthYear = model.BirthYear,
            ConfessionFather = model.ConfessionFather?.Trim(),
            StudyStage = model.StudyStage,
            SchoolOrUniversity = model.SchoolOrUniversity?.Trim(),
            Gender = model.Gender?.Trim(),
            Occupation = model.Occupation?.Trim(),
            FatherOccupation = model.FatherOccupation?.Trim(),
            MotherOccupation = model.MotherOccupation?.Trim(),
            MotherName = model.MotherName?.Trim(),
            OtherServices = model.OtherServices?.Trim(),
            NationalId = model.NationalId?.Trim(),
            ImageUrl = model.ImageUrl?.Trim(),
            Notes = model.Notes?.Trim(),
            AreaCode = model.AreaCode?.Trim(),
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
        var actor = await GetCurrentUserAsync();
        if (actor == null || (!actor.IsAdmin && !actor.IsTeacher)) return Forbid();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.DeletedAt != null) return NotFound();

        // Teachers can ONLY edit students
        if (actor.IsTeacher && !actor.IsAdmin)
        {
            if (user.IsTeacher || user.IsAdmin)
            {
                return Forbid();
            }
        }

        await LoadSchoolOptionsAsync();
        ViewBag.IsAdmin = actor.IsAdmin;
        ViewBag.IsTeacher = actor.IsTeacher;
        return View(new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            UserName = user.UserName ?? string.Empty,
            Mobile = user.Mobile,
            Address = user.Address,
            StreetNumber = user.StreetNumber,
            ApartmentNumber = user.ApartmentNumber,
            Building = user.Building,
            Landmark = user.Landmark,
            AddressNotes = user.AddressNotes,
            Landline = user.Landline,
            Mobile2 = user.Mobile2,
            BirthDay = user.BirthDay,
            BirthMonth = user.BirthMonth,
            BirthYear = user.BirthYear,
            ConfessionFather = user.ConfessionFather,
            StudyStage = user.StudyStage,
            SchoolOrUniversity = user.SchoolOrUniversity,
            Gender = user.Gender,
            Occupation = user.Occupation,
            FatherOccupation = user.FatherOccupation,
            MotherOccupation = user.MotherOccupation,
            MotherName = user.MotherName,
            OtherServices = user.OtherServices,
            NationalId = user.NationalId,
            ImageUrl = user.ImageUrl,
            AreaCode = user.AreaCode,
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
        if (actor == null || (!actor.IsAdmin && !actor.IsTeacher)) return Forbid();

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null || user.DeletedAt != null) return NotFound();

        // Teachers can ONLY edit students
        if (actor.IsTeacher && !actor.IsAdmin)
        {
            if (user.IsTeacher || user.IsAdmin)
            {
                return Forbid();
            }
        }

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
            ViewBag.IsAdmin = actor.IsAdmin;
            ViewBag.IsTeacher = actor.IsTeacher;
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.UserName = model.UserName.Trim();
        user.Email = model.UserName.Trim();
        user.Mobile = model.Mobile?.Trim();
        user.Address = model.Address?.Trim();
        user.StreetNumber = model.StreetNumber?.Trim();
        user.ApartmentNumber = model.ApartmentNumber?.Trim();
        user.Building = model.Building?.Trim();
        user.Landmark = model.Landmark?.Trim();
        user.AddressNotes = model.AddressNotes?.Trim();
        user.Landline = model.Landline?.Trim();
        user.Mobile2 = model.Mobile2?.Trim();
        user.BirthDay = model.BirthDay;
        user.BirthMonth = model.BirthMonth;
        user.BirthYear = model.BirthYear;
        user.ConfessionFather = model.ConfessionFather?.Trim();
        user.StudyStage = model.StudyStage;
        user.SchoolOrUniversity = model.SchoolOrUniversity?.Trim();
        user.Gender = model.Gender?.Trim();
        user.Occupation = model.Occupation?.Trim();
        user.FatherOccupation = model.FatherOccupation?.Trim();
        user.MotherOccupation = model.MotherOccupation?.Trim();
        user.MotherName = model.MotherName?.Trim();
        user.OtherServices = model.OtherServices?.Trim();
        user.NationalId = model.NationalId?.Trim();
        user.ImageUrl = model.ImageUrl?.Trim();
        user.AreaCode = model.AreaCode?.Trim();
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
            ViewBag.IsAdmin = actor.IsAdmin;
            ViewBag.IsTeacher = actor.IsTeacher;
            return View(model);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = "UserUpdated",
            TargetType = "User",
            TargetId = user.Id,
            Details = $"تم تحديث بيانات الحساب بواسطة {(actor.IsAdmin ? "المشرف" : "الخادم")}: {actor.FullName}",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم تحديث بيانات الحساب بنجاح.";
        if (actor.IsTeacher && !actor.IsAdmin)
        {
            return RedirectToAction(nameof(Students));
        }
        return RedirectToAction(user.IsTeacher || user.IsAdmin ? nameof(Teachers) : nameof(Students));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (user.Id == actor.Id)
        {
            TempData["ErrorMessage"] = "لا يمكنك حذف حسابك الحالي.";
            return RedirectToAction(nameof(Students));
        }

        bool isTeacherOrAdmin = user.IsTeacher || user.IsAdmin;
        string userFullName = user.FullName;

        // Clean up user team memberships
        var memberships = await _db.TeamMembers.Where(tm => tm.StudentId == user.Id).ToListAsync();
        if (memberships.Count > 0)
        {
            _db.TeamMembers.RemoveRange(memberships);
        }

        // Clean up user notifications reads
        var reads = await _db.UserNotificationReads.Where(r => r.UserId == user.Id).ToListAsync();
        if (reads.Count > 0) _db.UserNotificationReads.RemoveRange(reads);

        // Clean up reactions and comments
        var reactions = await _db.MarketCardReactions.Where(r => r.UserId == user.Id).ToListAsync();
        if (reactions.Count > 0) _db.MarketCardReactions.RemoveRange(reactions);

        var comments = await _db.MarketCardComments.Where(c => c.UserId == user.Id).ToListAsync();
        if (comments.Count > 0) _db.MarketCardComments.RemoveRange(comments);

        // Clean up purchase deductions if any
        var deductions = await _db.MagicCardPurchaseDeductions.Where(d => d.StudentId == user.Id).ToListAsync();
        if (deductions.Count > 0) _db.MagicCardPurchaseDeductions.RemoveRange(deductions);

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.SaveChangesAsync();
            var deleteResult = await _userManager.DeleteAsync(user);
            if (deleteResult.Succeeded)
            {
                await tx.CommitAsync();
            }
            else
            {
                throw new Exception("Identity delete failed: " + string.Join(", ", deleteResult.Errors.Select(e => e.Description)));
            }
        }
        catch
        {
            await tx.RollbackAsync();
            // Soft delete fallback to preserve integrity of historical attendance and audit logs
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.UserName = $"deleted_{DateTime.UtcNow.Ticks}_{user.UserName}";
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedById = actor.Id;
            await _userManager.UpdateAsync(user);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actor.Id,
            Action = "UserDeleted",
            TargetType = "User",
            TargetId = id,
            Details = $"تم حذف الحساب: {userFullName} بواسطة المشرف {actor.FullName}.",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم حذف حساب [{userFullName}] بنجاح.";
        return RedirectToAction(isTeacherOrAdmin ? nameof(Teachers) : nameof(Students));
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
    public async Task<IActionResult> Questions(string? search, string? status)
    {
        if (!await IsAdminAsync()) return Forbid();
        ViewBag.AttendancePoints = await GetAttendancePointsAsync();
        var query = _db.AttendanceQuestionDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Code.Contains(term) || x.DisplayText.Contains(term));
        }
        if (status == "enabled") query = query.Where(x => x.IsEnabled);
        if (status == "disabled") query = query.Where(x => !x.IsEnabled);
        ViewBag.Search = search;
        ViewBag.QuestionStatus = status;
        return View(await query.OrderBy(x => x.SortOrder).ToListAsync());
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
    public async Task<IActionResult> BulkStudents(int? schoolClassId, bool importTeachers = false)
    {
        if (!await IsAdminAsync()) return Forbid();
        await LoadSchoolOptionsAsync();
        ViewBag.SelectedClassId = schoolClassId;
        ViewBag.ClassStudents = schoolClassId.HasValue
            ? await _db.Users.Where(x => x.SchoolClassId == schoolClassId && !x.IsTeacher && !x.IsAdmin).OrderBy(x => x.FullName).ToListAsync()
            : new List<ApplicationUser>();
        return View(new BulkStudentImportViewModel { SchoolClassId = schoolClassId, ImportTeachers = importTeachers });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStudents(BulkStudentImportViewModel model)
    {
        var actor = await GetCurrentUserAsync();
        if (actor == null || !actor.IsAdmin) return Forbid();
        if (model.CsvFile is { Length: > 10_485_760 })
        {
            ModelState.AddModelError(nameof(model.CsvFile), "حجم ملف CSV لازم يكون أقل من 10 ميجابايت.");
        }
        if (model.CsvFile != null && !string.Equals(Path.GetExtension(model.CsvFile.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.CsvFile), "ارفع ملف CSV فقط.");
        }
        if (model.CsvFile == null && string.IsNullOrWhiteSpace(model.Rows))
        {
            ModelState.AddModelError(nameof(model.Rows), "ارفع ملف CSV أو الصق محتواه.");
        }
        var schoolClass = model.SchoolClassId.HasValue
            ? await _db.SchoolClasses.Include(x => x.SchoolLevel).FirstOrDefaultAsync(x => x.Id == model.SchoolClassId.Value)
            : null;
        if (model.SchoolClassId.HasValue && schoolClass == null) ModelState.AddModelError(nameof(model.SchoolClassId), "الفصل المختار مش موجود.");
        if (!ModelState.IsValid)
        {
            await LoadBulkStudentsViewAsync(model);
            return View("BulkStudents", model);
        }

        var errors = new List<string>();
        var created = 0;
        using var stream = new MemoryStream();
        if (model.CsvFile != null)
        {
            await model.CsvFile.CopyToAsync(stream);
            stream.Position = 0;
        }
        else
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(model.Rows));
            stream.Position = 0;
        }
        using var parser = new TextFieldParser(stream, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",", "\t");
        if (parser.EndOfData)
        {
            ModelState.AddModelError(nameof(model.Rows), "ملف CSV فارغ.");
            await LoadBulkStudentsViewAsync(model);
            return View("BulkStudents", model);
        }

        var headers = parser.ReadFields() ?? Array.Empty<string>();
        var headerMap = headers.Select((header, index) => new { Name = NormalizeCsvHeader(header), Index = index })
            .Where(item => !string.IsNullOrEmpty(item.Name))
            .GroupBy(item => item.Name)
            .ToDictionary(group => group.Key, group => group.First().Index);
        var knownHeaders = new[]
        {
            "الاسم", "name", "fullname", "اسم المستخدم", "username", "رقم الشارع", "streetnumber", "العنوان", "address",
            "رقم الشقة", "apartmentnumber", "العمارة", "building", "علامة مميزة", "landmark", "ملاحظات العنوان",
            "التليفون الأرضي", "landline", "موبايل 1", "mobile", "موبايل 2", "mobile2", "يوم الميلاد", "birthday",
            "شهر الميلاد", "birthmonth", "سنة الميلاد", "birthyear", "أب الاعتراف", "confessionfather", "المرحلة الدراسية",
            "studystage", "المدرسة / الجامعة", "schooloruniversity", "النوع", "gender", "المهنة", "occupation",
            "مهنة الأب", "fatheroccupation", "مهنة الأم", "motheroccupation", "اسم الأم", "mothername", "خدمات أخرى",
            "otherservices", "الرقم القومي", "nationalid", "رابط الصورة الشخصية", "imageurl", "ملاحظات", "notes", "كود المنطقة", "areacode",
            "المرحلة الدراسية (0-9)", "النوع (ولد / بنت)"
        };
        if (!knownHeaders.Any(name => headerMap.ContainsKey(NormalizeCsvHeader(name))))
        {
            ModelState.AddModelError(nameof(model.Rows), "صف العناوين لا يحتوي على أي اسم حقل معروف.");
            await LoadBulkStudentsViewAsync(model);
            return View("BulkStudents", model);
        }
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            var fields = parser.ReadFields() ?? Array.Empty<string>();
            if (fields.All(string.IsNullOrWhiteSpace)) continue;

            var fullName = CsvValue(headerMap, fields, "الاسم", "name", "fullname");
            var userName = CsvValue(headerMap, fields, "اسم المستخدم", "username", "user name");
            if (string.IsNullOrWhiteSpace(userName)) userName = "account" + Guid.NewGuid().ToString("N")[..10];
            if (await _userManager.FindByNameAsync(userName) != null)
            {
                errors.Add($"السطر {rowNumber}: اسم المستخدم {userName} موجود بالفعل.");
                continue;
            }

            var birthDayText = CsvValue(headerMap, fields, "يوم الميلاد", "birthday", "birth day");
            var birthMonthText = CsvValue(headerMap, fields, "شهر الميلاد", "birthmonth", "birth month");
            var birthYearText = CsvValue(headerMap, fields, "سنة الميلاد", "birthyear", "birth year");
            var studyStageText = CsvValue(headerMap, fields, "المرحلة الدراسية (0-9)", "المرحلة الدراسية", "studystage", "study stage");
            var birthDay = ParseNullableInt(birthDayText);
            var birthMonth = ParseNullableInt(birthMonthText);
            var birthYear = ParseNullableInt(birthYearText);
            var studyStage = ParseNullableInt(studyStageText);
            if ((!string.IsNullOrWhiteSpace(birthDayText) && !birthDay.HasValue)
                || (!string.IsNullOrWhiteSpace(birthMonthText) && !birthMonth.HasValue)
                || (!string.IsNullOrWhiteSpace(birthYearText) && !birthYear.HasValue)
                || (!string.IsNullOrWhiteSpace(studyStageText) && !studyStage.HasValue))
            {
                errors.Add($"السطر {rowNumber}: حقول الميلاد والمرحلة الدراسية لازم تكون أرقامًا.");
                continue;
            }
            if ((birthDay.HasValue && birthDay is < 1 or > 31)
                || (birthMonth.HasValue && birthMonth is < 1 or > 12)
                || (birthYear.HasValue && birthYear is < 1800 or > 2100)
                || (studyStage.HasValue && studyStage is < 0 or > 9))
            {
                errors.Add($"السطر {rowNumber}: تحقق من يوم وشهر الميلاد والمرحلة الدراسية (0-9).");
                continue;
            }

            var isTeacherImport = model.ImportTeachers;
            var user = new ApplicationUser
            {
                FullName = string.IsNullOrWhiteSpace(fullName) ? "بدون اسم" : fullName.Trim(),
                UserName = userName,
                Email = CsvValue(headerMap, fields, "البريد", "email") ?? $"{userName}@accounts.local",
                StreetNumber = CsvValue(headerMap, fields, "رقم الشارع", "streetnumber", "street number"),
                Address = CsvValue(headerMap, fields, "العنوان", "address"),
                ApartmentNumber = CsvValue(headerMap, fields, "رقم الشقة", "apartmentnumber", "apartment number"),
                Building = CsvValue(headerMap, fields, "العمارة", "building"),
                Landmark = CsvValue(headerMap, fields, "علامة مميزة", "landmark"),
                AddressNotes = CsvValue(headerMap, fields, "ملاحظات العنوان", "addressnotes", "address notes"),
                Landline = CsvValue(headerMap, fields, "التليفون الأرضي", "الهاتف الأرضي", "landline"),
                Mobile = CsvValue(headerMap, fields, "موبايل 1", "mobile", "mobile1"),
                Mobile2 = CsvValue(headerMap, fields, "موبايل 2", "mobile2"),
                BirthDay = birthDay,
                BirthMonth = birthMonth,
                BirthYear = birthYear,
                ConfessionFather = CsvValue(headerMap, fields, "أب الاعتراف", "confessionfather", "confession father"),
                StudyStage = studyStage,
                SchoolOrUniversity = CsvValue(headerMap, fields, "المدرسة / الجامعة", "المدرسة/الجامعة", "schooloruniversity", "school/university"),
                Gender = CsvValue(headerMap, fields, "النوع (ولد / بنت)", "النوع", "gender"),
                Occupation = CsvValue(headerMap, fields, "المهنة", "occupation"),
                FatherOccupation = CsvValue(headerMap, fields, "مهنة الأب", "fatheroccupation", "father occupation"),
                MotherOccupation = CsvValue(headerMap, fields, "مهنة الأم", "motheroccupation", "mother occupation"),
                MotherName = CsvValue(headerMap, fields, "اسم الأم", "mothername", "mother name"),
                OtherServices = CsvValue(headerMap, fields, "خدمات أخرى", "otherservices", "other services"),
                NationalId = CsvValue(headerMap, fields, "الرقم القومي", "nationalid", "national id"),
                ImageUrl = CsvValue(headerMap, fields, "رابط الصورة الشخصية", "imageurl", "profile image"),
                Notes = CsvValue(headerMap, fields, "ملاحظات", "notes"),
                AreaCode = CsvValue(headerMap, fields, "كود المنطقة", "areacode", "area code"),
                SchoolClassId = schoolClass?.Id,
                ClassName = schoolClass?.Name,
                SchoolLevelId = schoolClass?.SchoolLevelId,
                Level = schoolClass?.SchoolLevel.Name,
                IsTeacher = isTeacherImport,
                IsActive = true,
                EmailConfirmed = true,
                QrCodeValue = Guid.NewGuid().ToString("N"),
                MustChangePassword = string.IsNullOrWhiteSpace(CsvValue(headerMap, fields, "password", "كلمة المرور"))
            };
            var password = CsvValue(headerMap, fields, "password", "كلمة المرور")
                ?? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18));
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                errors.Add($"السطر {rowNumber}: {string.Join("؛ ", result.Errors.Select(x => x.Description))}");
                continue;
            }
            var roleResult = await _userManager.AddToRoleAsync(user, isTeacherImport ? "Teacher" : "Student");
            if (!roleResult.Succeeded)
            {
                user.IsActive = false;
                await _userManager.UpdateAsync(user);
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
                Action = model.ImportTeachers ? "BulkTeachersImported" : "BulkStudentsImported",
                TargetType = model.SchoolClassId.HasValue ? "SchoolClass" : "User",
                TargetId = schoolClass?.Id.ToString(),
                Details = $"إضافة {created} {(model.ImportTeachers ? "خادم" : "مخدوم")}",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        TempData["SuccessMessage"] = $"تمت إضافة {created} {(model.ImportTeachers ? "خادم" : "مخدوم")}. الحسابات التي لم يتضمن CSV كلمة مرور لها تحتاج تعيين كلمة مرور من الإدارة قبل تسجيل الدخول.";
        if (errors.Count > 0)
        {
            await LoadBulkStudentsViewAsync(model);
            ViewBag.ImportErrors = errors;
            return View("BulkStudents", model);
        }
        return RedirectToAction(nameof(BulkStudents), new { schoolClassId = model.SchoolClassId, importTeachers = model.ImportTeachers });
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
            var matchingUserIds = await _db.Users
                .Where(u => u.FullName.Contains(term) || (u.UserName != null && u.UserName.Contains(term)))
                .Select(u => u.Id)
                .ToListAsync();

            query = query.Where(x =>
                (x.Actor != null && ((x.Actor.FullName != null && x.Actor.FullName.Contains(term)) || (x.Actor.UserName != null && x.Actor.UserName.Contains(term)))) ||
                (x.TargetType != null && x.TargetType.Contains(term)) ||
                (x.TargetId != null && (x.TargetId.Contains(term) || matchingUserIds.Contains(x.TargetId))) ||
                (x.Details != null && x.Details.Contains(term)) ||
                (x.Reason != null && x.Reason.Contains(term)));
        }

        var rawLogs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();

        var targetUserIds = rawLogs
            .Where(x => !string.IsNullOrEmpty(x.TargetId) && (x.TargetType == "User" || x.TargetType == "Student" || x.TargetType == "Teacher"))
            .Select(x => x.TargetId!)
            .Distinct()
            .ToList();

        var targetUsers = await _db.Users
            .Where(u => targetUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var attendanceRecordIds = rawLogs
            .Where(x => x.TargetType == "AttendanceRecord" && int.TryParse(x.TargetId, out _))
            .Select(x => int.Parse(x.TargetId!))
            .Distinct()
            .ToList();

        var attendanceUsers = await _db.AttendanceRecords
            .Include(ar => ar.User)
            .Where(x => attendanceRecordIds.Contains(x.Id) && x.User != null)
            .ToDictionaryAsync(x => x.Id, x => x.User!);

        var classIds = rawLogs
            .Where(x => x.TargetType == "SchoolClass" && int.TryParse(x.TargetId, out _))
            .Select(x => int.Parse(x.TargetId!))
            .Distinct()
            .ToList();
        var classes = await _db.SchoolClasses.Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var levelIds = rawLogs
            .Where(x => x.TargetType == "SchoolLevel" && int.TryParse(x.TargetId, out _))
            .Select(x => int.Parse(x.TargetId!))
            .Distinct()
            .ToList();
        var levels = await _db.SchoolLevels.Where(l => levelIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);

        var logs = rawLogs.Select(x =>
        {
            string? targetName = null;
            string? targetUserName = null;
            string? targetRole = null;

            if (!string.IsNullOrEmpty(x.TargetId) && targetUsers.TryGetValue(x.TargetId, out var targetUser))
            {
                targetName = targetUser.FullName;
                targetUserName = targetUser.UserName;
                targetRole = targetUser.IsAdmin ? "مشرف" : (targetUser.IsTeacher ? "خادم" : "مخدوم");
            }
            else if (x.TargetType == "AttendanceRecord" && int.TryParse(x.TargetId, out var attId) && attendanceUsers.TryGetValue(attId, out var attUser))
            {
                targetName = attUser.FullName;
                targetUserName = attUser.UserName;
                targetRole = attUser.IsAdmin ? "مشرف" : (attUser.IsTeacher ? "خادم" : "مخدوم");
            }
            else if (x.TargetType == "SchoolClass" && int.TryParse(x.TargetId, out var cid) && classes.TryGetValue(cid, out var cls))
            {
                targetName = cls.Name;
                targetRole = "فصل";
            }
            else if (x.TargetType == "SchoolLevel" && int.TryParse(x.TargetId, out var lid) && levels.TryGetValue(lid, out var lvl))
            {
                targetName = lvl.Name;
                targetRole = "مرحلة";
            }

            return new AdminAuditListItemViewModel
            {
                Id = x.Id,
                Action = x.Action,
                ActorName = x.Actor != null ? x.Actor.FullName : (x.ActorId == "System" ? "النظام" : x.ActorId),
                ActorUserName = x.Actor?.UserName,
                ActorRole = x.Actor == null ? "النظام" : (x.Actor.IsAdmin ? "مشرف" : (x.Actor.IsTeacher ? "خادم" : "مخدوم")),
                TargetType = x.TargetType,
                TargetId = x.TargetId,
                TargetName = targetName,
                TargetUserName = targetUserName,
                TargetRole = targetRole,
                Details = x.Details,
                Reason = x.Reason,
                CreatedAt = x.CreatedAt
            };
        }).ToList();

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
        ViewBag.ClassStudents = model.SchoolClassId.GetValueOrDefault() > 0
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

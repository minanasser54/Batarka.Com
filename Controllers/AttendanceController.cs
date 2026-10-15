using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;
using System.Globalization;
using System.Text;

namespace MyMvcApp10.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AttendanceController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? date)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var timeZone = await GetSchoolTimeZoneAsync();
        var selectedDate = ParseDate(date) ?? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date, DateTimeKind.Unspecified);
        var (dateStartUtc, dateEndUtc) = GetUtcDayRange(selectedDate, timeZone);

        var records = await _db.AttendanceRecords
            .Where(x => !x.IsDeleted && x.AttendanceTimeUtc >= dateStartUtc && x.AttendanceTimeUtc < dateEndUtc)
            .Include(x => x.User).ThenInclude(x => x!.SchoolClass)
            .Include(x => x.User).ThenInclude(x => x!.SchoolLevel)
            .OrderByDescending(x => x.AttendanceTimeUtc)
            .ToListAsync();

        var activeStudents = await _db.Users
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
            .ToListAsync();
        var activeTeacherCount = await _db.Users.CountAsync(u => u.IsActive && u.IsTeacher && !u.IsAdmin);
        var studentRecords = records.Where(x => x.User != null && !x.User.IsTeacher && !x.User.IsAdmin).ToList();
        var teacherRecords = records.Where(x => x.User != null && x.User.IsTeacher && !x.User.IsAdmin).ToList();

        var missing = activeStudents
            .Where(u => !studentRecords.Any(r => r.UserId == u.Id))
            .OrderBy(u => u.FullName)
            .ToList();

        var viewModel = new AttendancePageViewModel
        {
            Today = DateTime.UtcNow,
            SelectedDate = selectedDate,
            ActiveStudentCount = activeStudents.Count,
            ActiveTeacherCount = activeTeacherCount,
            LocalAttendanceTimes = records.ToDictionary(
                x => x.Id,
                x => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.AttendanceTimeUtc, DateTimeKind.Utc), timeZone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            TodayAttendance = records,
            StudentAttendance = studentRecords,
            TeacherAttendance = teacherRecords,
            MissingPeople = missing
        };

        ViewData["Title"] = "تسجيل الحضور";
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? date)
    {
        return await ExportAttendanceCsv(date, teachers: false);
    }

    [HttpGet]
    public async Task<IActionResult> ExportTeachersCsv(string? date)
    {
        return await ExportAttendanceCsv(date, teachers: true);
    }

    private async Task<IActionResult> ExportAttendanceCsv(string? date, bool teachers)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin)) return Forbid();

        var timeZone = await GetSchoolTimeZoneAsync();
        var selectedDate = ParseDate(date) ?? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date, DateTimeKind.Unspecified);
        var (dateStartUtc, dateEndUtc) = GetUtcDayRange(selectedDate, timeZone);
        var records = await _db.AttendanceRecords
            .Where(x => !x.IsDeleted && x.AttendanceTimeUtc >= dateStartUtc && x.AttendanceTimeUtc < dateEndUtc)
            .Include(x => x.User).ThenInclude(x => x!.SchoolClass)
            .Include(x => x.User).ThenInclude(x => x!.SchoolLevel)
            .Where(x => teachers ? x.User!.IsTeacher && !x.User.IsAdmin : !x.User!.IsTeacher && !x.User.IsAdmin)
            .OrderBy(x => x.User!.FullName)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("الاسم,اسم المستخدم,الفصل,المرحلة,تاريخ الحضور,الوقت,طريقة التسجيل");
        foreach (var record in records)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(record.User?.FullName),
                Csv(record.User?.UserName),
                Csv(record.User?.SchoolClass?.Name ?? record.User?.ClassName),
                Csv(record.User?.SchoolLevel?.Name ?? record.User?.Level),
                Csv(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(record.AttendanceTimeUtc, DateTimeKind.Utc), timeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(record.AttendanceTimeUtc, DateTimeKind.Utc), timeZone).ToString("HH:mm", CultureInfo.InvariantCulture)),
                Csv(record.RegistrationMethod)
            }));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var roleLabel = teachers ? "teachers" : "students";
        return File(bytes, "text/csv; charset=utf-8", $"attendance-{roleLabel}-{selectedDate:yyyy-MM-dd}.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterByQr(string qrCode, string? attendanceDate)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(qrCode))
        {
            TempData["ErrorMessage"] = "الـ QR غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.QrCodeValue == qrCode && x.IsActive);
        if (user == null)
        {
            TempData["ErrorMessage"] = "لم يتم العثور على الحساب المرتبط بهذا الـ QR.";
            return RedirectToAction(nameof(Index));
        }

        var timeZone = await GetSchoolTimeZoneAsync();
        var selectedDate = ParseDate(attendanceDate) ?? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date, DateTimeKind.Unspecified);
        var session = await GetOrCreateSessionAsync(selectedDate);
        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(x => x.SessionId == session.Id && x.UserId == user.Id && !x.IsDeleted);

        if (existing != null)
        {
            TempData["WarningMessage"] = $"تم تسجيل حضور {user.FullName} قبل كده في اليوم المحدد.";
            return RedirectToAction(nameof(Index), new { date = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        var record = new AttendanceRecord
        {
            SessionId = session.Id,
            UserId = user.Id,
            AttendanceTimeUtc = ToUtcAttendanceTime(selectedDate, timeZone),
            RecordedById = current.Id,
            RegistrationMethod = "Qr",
            Status = "Present"
        };

        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync();
        await LogAttendanceAsync(record, user, current);

        if (!user.IsTeacher && !user.IsAdmin)
        {
            await AwardAttendancePointAsync(record, current);
        }

        TempData["SuccessMessage"] = $"تم تسجيل حضور {user.FullName} بنجاح.";
        return user.IsTeacher || user.IsAdmin
            ? RedirectToAction(nameof(Index), new { date = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })
            : RedirectToAction(nameof(Questionnaire), new { attendanceId = record.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterByName(string userId, string? attendanceDate)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
        if (user == null)
        {
            TempData["ErrorMessage"] = "المستخدم غير موجود أو غير نشط.";
            return RedirectToAction(nameof(Index));
        }

        var timeZone = await GetSchoolTimeZoneAsync();
        var selectedDate = ParseDate(attendanceDate) ?? DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date, DateTimeKind.Unspecified);
        var session = await GetOrCreateSessionAsync(selectedDate);
        var existing = await _db.AttendanceRecords.AnyAsync(x => x.SessionId == session.Id && x.UserId == user.Id && !x.IsDeleted);
        if (existing)
        {
            TempData["WarningMessage"] = $"تم تسجيل حضور {user.FullName} قبل كده في اليوم المحدد.";
            return RedirectToAction(nameof(Index), new { date = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        var record = new AttendanceRecord
        {
            SessionId = session.Id,
            UserId = user.Id,
            AttendanceTimeUtc = ToUtcAttendanceTime(selectedDate, timeZone),
            RecordedById = current.Id,
            RegistrationMethod = "NameSearch",
            Status = "Present"
        };

        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync();
        await LogAttendanceAsync(record, user, current);

        if (!user.IsTeacher && !user.IsAdmin)
        {
            await AwardAttendancePointAsync(record, current);
        }

        TempData["SuccessMessage"] = $"تم تسجيل حضور {user.FullName} بنجاح.";
        return user.IsTeacher || user.IsAdmin
            ? RedirectToAction(nameof(Index), new { date = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })
            : RedirectToAction(nameof(Questionnaire), new { attendanceId = record.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Search(string term)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        term = (term ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return Json(new List<AttendanceSearchResultViewModel>());
        }

        var users = await _db.Users
            .Where(u => u.IsActive &&
                (u.FullName.Contains(term) || (u.UserName != null && u.UserName.Contains(term))))
            .Take(10)
            .Select(u => new AttendanceSearchResultViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                AccountType = u.IsTeacher ? "خادم" : "مخدوم",
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                Level = u.SchoolLevel != null ? u.SchoolLevel.Name : u.Level,
                QrCodeValue = u.QrCodeValue
            })
            .ToListAsync();

        return Json(users);
    }

    [HttpGet]
    public async Task<IActionResult> Questionnaire(int attendanceId)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var record = await _db.AttendanceRecords
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == attendanceId);

        if (record == null)
        {
            return NotFound();
        }

        var questions = await _db.AttendanceQuestionDefinitions
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var timeZone = await GetSchoolTimeZoneAsync();
        var attendanceLocalDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(record.AttendanceTimeUtc, DateTimeKind.Utc), timeZone);
        var currentDay = attendanceLocalDate.Day;
        var applicableQuestions = questions
            .Where(q => IsQuestionApplicable(q, currentDay))
            .ToList();

        ViewBag.AttendanceUser = record.User;
        ViewBag.AttendanceId = attendanceId;
        return View(applicableQuestions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitQuestionnaire(int attendanceId, List<int>? answeredQuestionIds)
    {
        var current = await GetCurrentUserAsync();
        if (current == null || (!current.IsTeacher && !current.IsAdmin))
        {
            return Forbid();
        }

        var record = await _db.AttendanceRecords
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == attendanceId);

        if (record == null)
        {
            return NotFound();
        }

        if (record.User.IsTeacher || record.User.IsAdmin)
        {
            return Forbid();
        }

        if (await _db.AttendanceQuestionnaireSubmissions.AnyAsync(x => x.AttendanceRecordId == attendanceId))
        {
            TempData["WarningMessage"] = "تم إرسال نموذج الأسئلة من قبل لهذا الحضور.";
            return RedirectToAction(nameof(Index));
        }

        var allQuestions = await _db.AttendanceQuestionDefinitions
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
        var timeZone = await GetSchoolTimeZoneAsync();
        var attendanceLocalDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(record.AttendanceTimeUtc, DateTimeKind.Utc), timeZone);
        var answered = (answeredQuestionIds ?? new List<int>()).ToHashSet();

        var submission = new AttendanceQuestionnaireSubmission
        {
            AttendanceRecordId = attendanceId,
            SubmittedById = current.Id,
            Status = "Submitted",
            SubmittedAtUtc = DateTime.UtcNow
        };

        foreach (var question in allQuestions)
        {
            var applicable = IsQuestionApplicable(question, attendanceLocalDate.Day);
            var value = applicable && answered.Contains(question.Id);

            if (applicable)
            {
                var answer = new AttendanceQuestionAnswer
                {
                    QuestionId = question.Id,
                    AnswerValue = value,
                    PointsAwarded = value ? question.PointsAwarded : 0
                };

                submission.Answers.Add(answer);
                if (value)
                {
                    await CreateTransactionAsync(record.UserId, current.Id, question.PointsAwarded, question.DisplayText, "AttendanceQuestionnaire", attendanceId, $"question:{attendanceId}:{question.Id}");
                }
            }
        }

        _db.AttendanceQuestionnaireSubmissions.Add(submission);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم حفظ إجابات نموذج الحضور وبنجاح.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _db.Users.FirstOrDefaultAsync(x => x.UserName == username);
    }

    private async Task<AttendanceSession> GetOrCreateSessionAsync(DateTime selectedDate)
    {
        var day = selectedDate.Date;
        var session = await _db.AttendanceSessions
            .FirstOrDefaultAsync(x => x.SessionDate >= day && x.SessionDate < day.AddDays(1));

        if (session != null) return session;

        var current = await GetCurrentUserAsync();
        session = new AttendanceSession
        {
            Name = "حضور مدرسة الأحد",
            SessionDate = day,
            Status = "Open",
            CreatedById = current?.Id,
            CreatedAt = DateTime.UtcNow
        };

        _db.AttendanceSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    private static DateTime? ParseDate(string? date)
    {
        return DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified)
            : null;
    }

    private async Task<TimeZoneInfo> GetSchoolTimeZoneAsync()
    {
        var timeZoneId = await _db.ApplicationSettings
            .Where(x => x.Key == "Timezone")
            .Select(x => x.Value)
            .FirstOrDefaultAsync() ?? "Africa/Cairo";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetUtcDayRange(DateTime localDate, TimeZoneInfo timeZone)
    {
        var localStart = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);
        return (TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone), TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
    }

    private static DateTime ToUtcAttendanceTime(DateTime localDate, TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var localAttendance = DateTime.SpecifyKind(localDate.Date.Add(localNow.TimeOfDay), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localAttendance, timeZone);
    }

    private static string Csv(string? value)
    {
        var escaped = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static bool IsQuestionApplicable(AttendanceQuestionDefinition question, int day)
    {
        if (!question.StartDayOfMonth.HasValue && !question.EndDayOfMonth.HasValue) return true;
        if (question.StartDayOfMonth.HasValue && !question.EndDayOfMonth.HasValue) return day >= question.StartDayOfMonth.Value;
        if (!question.StartDayOfMonth.HasValue && question.EndDayOfMonth.HasValue) return day <= question.EndDayOfMonth.Value;
        var start = question.StartDayOfMonth!.Value;
        var end = question.EndDayOfMonth!.Value;
        return start <= end ? day >= start && day <= end : day >= start || day <= end;
    }

    private async Task AwardAttendancePointAsync(AttendanceRecord record, ApplicationUser current)
    {
        var configuredValue = await _db.ApplicationSettings
            .Where(x => x.Key == "AttendancePoints")
            .Select(x => x.Value)
            .FirstOrDefaultAsync();
        var points = int.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configuredPoints)
            ? configuredPoints
            : 1;

        await CreateTransactionAsync(record.UserId, current.Id, points, "نقطة حضور", "Attendance", record.Id, $"attendance:{record.Id}");
    }

    private async Task LogAttendanceAsync(AttendanceRecord record, ApplicationUser user, ApplicationUser current)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = current.Id,
            Action = "AttendanceRegistered",
            TargetType = "AttendanceRecord",
            TargetId = record.Id.ToString(CultureInfo.InvariantCulture),
            Details = $"تسجيل حضور {(user.IsTeacher ? "خادم" : "مخدوم")}: {user.FullName}، الطريقة: {record.RegistrationMethod}",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task CreateTransactionAsync(string studentId, string teacherId, int pointsDelta, string reason, string sourceType, int attendanceId, string idempotencyKey)
    {
        if (pointsDelta == 0 || await _db.GradingTransactions.AnyAsync(x => x.IdempotencyKey == idempotencyKey))
        {
            return;
        }

        var student = await _db.Users.FirstOrDefaultAsync(x => x.Id == studentId);
        if (student == null)
        {
            return;
        }

        var transaction = new GradingTransaction
        {
            StudentId = studentId,
            TeacherId = teacherId,
            PointsDelta = pointsDelta,
            Reason = reason,
            SourceType = sourceType,
            SourceId = attendanceId,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        _db.GradingTransactions.Add(transaction);
        student.GradingBalance += pointsDelta;
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = teacherId,
            Action = sourceType == "Attendance" ? "AttendancePointAwarded" : "QuestionPointsAwarded",
            TargetType = "User",
            TargetId = studentId,
            Details = $"{reason}: {pointsDelta} نقطة",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
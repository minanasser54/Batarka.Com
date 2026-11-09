using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Services;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers.Api.V1;

[ApiController]
[Route("api/v1/attendance")]
[Authorize(AuthenticationSchemes = ApiAuthSchemes.Bearer)]
public sealed class AttendanceApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public AttendanceApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    [HttpPost("register-by-qr")]
    [ProducesResponseType<ApiAttendanceItem>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiAttendanceItem>> RegisterByQr([FromBody] ApiRegisterAttendanceRequest request)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();
        if (!current.IsAdmin && !current.IsTeacher) return Forbid();

        var user = await _db.Users.FirstOrDefaultAsync(item => item.QrCodeValue == request.QrCodeValue && item.IsActive && item.DeletedAt == null);
        if (user == null) return NotFound(new ProblemDetails { Title = "The active account for this QR code was not found." });

        var timeZoneId = await _db.ApplicationSettings.Where(item => item.Key == "Timezone")
            .Select(item => item.Value).FirstOrDefaultAsync() ?? "Africa/Cairo";
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }

        var localDate = request.AttendanceDate?.ToDateTime(TimeOnly.MinValue)
            ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
        var date = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        var session = await _db.AttendanceSessions.FirstOrDefaultAsync(item => item.SessionDate >= date && item.SessionDate < date.AddDays(1));
        if (session == null)
        {
            session = new AttendanceSession
            {
                Name = "حضور مدرسة الأحد",
                SessionDate = date,
                Status = "Open",
                CreatedById = current.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.AttendanceSessions.Add(session);
            await _db.SaveChangesAsync();
        }

        if (await _db.AttendanceRecords.AnyAsync(item => item.SessionId == session.Id && item.UserId == user.Id && !item.IsDeleted))
            return Conflict(new ProblemDetails { Title = "Attendance is already registered for this account and session." });

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var localAttendance = DateTime.SpecifyKind(date.Add(localNow.TimeOfDay), DateTimeKind.Unspecified);
        var record = new AttendanceRecord
        {
            SessionId = session.Id,
            UserId = user.Id,
            AttendanceTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localAttendance, timeZone),
            RecordedById = current.Id,
            RegistrationMethod = "Qr",
            Status = "Present",
            CreatedAt = DateTime.UtcNow
        };
        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync();
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = current.Id,
            Action = "AttendanceRegistered",
            TargetType = "AttendanceRecord",
            TargetId = record.Id.ToString(CultureInfo.InvariantCulture),
            Details = $"API QR attendance registered for {user.FullName}.",
            CreatedAt = DateTime.UtcNow
        });

        if (!user.IsTeacher && !user.IsAdmin)
        {
            var configuredPoints = await _db.ApplicationSettings.Where(item => item.Key == "AttendancePoints")
                .Select(item => item.Value).FirstOrDefaultAsync();
            var points = int.TryParse(configuredPoints, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 1;
            var key = $"attendance:{record.Id}";
            if (points != 0 && !await _db.GradingTransactions.AnyAsync(item => item.IdempotencyKey == key))
            {
                user.GradingBalance += points;
                _db.GradingTransactions.Add(new GradingTransaction
                {
                    StudentId = user.Id,
                    TeacherId = current.Id,
                    PointsDelta = points,
                    Reason = "نقطة حضور",
                    SourceType = "Attendance",
                    SourceId = record.Id,
                    IdempotencyKey = key,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        try
        {
            await _notificationService.SendToUserAsync(user.Id, "✅ تسجيل حضور", "تم تسجيل حضورك بنجاح.", "Attendance", "/Student/Profile");
        }
        catch
        {
        }

        return CreatedAtAction(nameof(GetStudentAttendance), new { studentId = user.Id }, new ApiAttendanceItem
        {
            Id = record.Id,
            AttendanceTimeUtc = record.AttendanceTimeUtc,
            Status = record.Status,
            RegistrationMethod = record.RegistrationMethod,
            SessionName = session.Name
        });
    }

    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyList<ApiAttendanceItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiAttendanceItem>>> GetMyAttendance(int take = 50)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();
        take = Math.Clamp(take, 1, 100);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.AttendanceTimeUtc)
            .Take(take)
            .Select(item => new ApiAttendanceItem
            {
                Id = item.Id,
                AttendanceTimeUtc = item.AttendanceTimeUtc,
                Status = item.Status,
                RegistrationMethod = item.RegistrationMethod,
                SessionName = item.Session.Name
            })
            .ToListAsync();
        return Ok(records);
    }

    [HttpGet("students/{studentId}")]
    [ProducesResponseType<IReadOnlyList<ApiAttendanceItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiAttendanceItem>>> GetStudentAttendance(string studentId, int take = 100)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();
        if (!current.IsAdmin && !current.IsTeacher) return Forbid();

        var studentExists = await _db.Users.AnyAsync(item => item.Id == studentId && !item.IsAdmin && !item.IsTeacher && item.DeletedAt == null);
        if (!studentExists) return NotFound();
        take = Math.Clamp(take, 1, 100);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(item => item.UserId == studentId && !item.IsDeleted)
            .OrderByDescending(item => item.AttendanceTimeUtc)
            .Take(take)
            .Select(item => new ApiAttendanceItem
            {
                Id = item.Id,
                AttendanceTimeUtc = item.AttendanceTimeUtc,
                Status = item.Status,
                RegistrationMethod = item.RegistrationMethod,
                SessionName = item.Session.Name
            })
            .ToListAsync();
        return Ok(records);
    }
}

[ApiController]
[Route("api/v1/notifications")]
[Authorize(AuthenticationSchemes = ApiAuthSchemes.Bearer)]
public sealed class NotificationsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApiNotificationItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiNotificationItem>>> List(bool unreadOnly = false, int take = 50)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();
        take = Math.Clamp(take, 1, 100);

        var teamId = await _db.TeamMembers.AsNoTracking()
            .Where(member => member.StudentId == current.Id)
            .Select(member => (int?)member.TeamId)
            .FirstOrDefaultAsync();
        var notifications = await _db.AppNotifications.AsNoTracking()
            .Where(item => item.TargetType == "All"
                || (item.TargetType == "SpecificUser" && item.TargetUserId == current.Id)
                || (item.TargetType == "Students" && !current.IsAdmin && !current.IsTeacher)
                || (item.TargetType == "Teachers" && current.IsTeacher && !current.IsAdmin)
                || (item.TargetType == "Admins" && current.IsAdmin)
                || (item.TargetType == "SpecificTeam" && teamId.HasValue && item.TargetTeamId == teamId.Value))
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .Select(item => new ApiNotificationItem
            {
                Id = item.Id,
                Title = item.Title,
                Message = item.Message,
                NotificationType = item.NotificationType,
                LinkUrl = item.LinkUrl,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync();

        var readIds = await _db.UserNotificationReads.AsNoTracking()
            .Where(item => item.UserId == current.Id && notifications.Select(notification => notification.Id).Contains(item.NotificationId))
            .Select(item => item.NotificationId)
            .ToHashSetAsync();
        foreach (var notification in notifications)
            notification.IsRead = readIds.Contains(notification.Id);

        if (unreadOnly) notifications = notifications.Where(item => !item.IsRead).ToList();
        return Ok(notifications);
    }

    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();

        var teamId = await _db.TeamMembers.AsNoTracking()
            .Where(member => member.StudentId == current.Id)
            .Select(member => (int?)member.TeamId)
            .FirstOrDefaultAsync();
        var visible = await _db.AppNotifications.AnyAsync(item => item.Id == id && (item.TargetType == "All"
            || (item.TargetType == "SpecificUser" && item.TargetUserId == current.Id)
            || (item.TargetType == "Students" && !current.IsAdmin && !current.IsTeacher)
            || (item.TargetType == "Teachers" && current.IsTeacher && !current.IsAdmin)
            || (item.TargetType == "Admins" && current.IsAdmin)
            || (item.TargetType == "SpecificTeam" && teamId.HasValue && item.TargetTeamId == teamId.Value)));
        if (!visible) return NotFound();

        if (!await _db.UserNotificationReads.AnyAsync(item => item.NotificationId == id && item.UserId == current.Id))
        {
            _db.UserNotificationReads.Add(new UserNotificationRead
            {
                NotificationId = id,
                UserId = current.Id,
                ReadAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}

[ApiController]
[Route("api/v1/grading/students")]
[Authorize(AuthenticationSchemes = ApiAuthSchemes.Bearer)]
public sealed class GradingApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public GradingApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    [HttpPost("{studentId}/actions")]
    [ProducesResponseType<ApiApplyGradeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiApplyGradeResponse>> Apply(string studentId, [FromBody] ApiApplyGradeRequest request)
    {
        var teacher = await _userManager.GetUserAsync(User);
        if (teacher == null) return Unauthorized();
        if (!teacher.IsAdmin && !teacher.IsTeacher) return Forbid();

        var student = await _db.Users.FirstOrDefaultAsync(item => item.Id == studentId
            && item.IsActive && !item.IsAdmin && !item.IsTeacher && item.DeletedAt == null);
        if (student == null) return NotFound();

        var delta = request.Action switch
        {
            "green" => 1,
            "red" => -1,
            "black" => -5,
            _ => 0
        };
        if (delta == 0) return BadRequest(new ProblemDetails { Title = "Unsupported grading action." });

        var idempotencyKey = $"api:{student.Id}:{teacher.Id}:{request.IdempotencyKey.Trim()}";
        if (await _db.GradingTransactions.AnyAsync(item => item.IdempotencyKey == idempotencyKey))
            return Conflict(new ProblemDetails { Title = "This grading action has already been applied." });

        var reason = request.Action switch
        {
            "green" => "أخضر: +1 نقطة",
            "red" => "أحمر: -1 نقطة",
            _ => "أسود: -5 نقاط"
        };
        using var transactionScope = await _db.Database.BeginTransactionAsync();
        student.GradingBalance += delta;
        _db.GradingTransactions.Add(new GradingTransaction
        {
            StudentId = student.Id,
            TeacherId = teacher.Id,
            PointsDelta = delta,
            Reason = reason,
            SourceType = "Manual",
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        });
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = teacher.Id,
            Action = "GradingApplied",
            TargetType = "Student",
            TargetId = student.Id,
            Details = $"{student.FullName}: {reason} (API)",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await transactionScope.CommitAsync();

        try
        {
            await _notificationService.SendToUserAsync(student.Id, "تحديث الدرجات", $"تم تسجيل {reason} في رصيدك. رصيدك الحالي: {student.GradingBalance} نقطة.", "GradeChange", "/Grading");
        }
        catch
        {
        }

        return Ok(new ApiApplyGradeResponse { PointsDelta = delta, NewBalance = student.GradingBalance });
    }
}
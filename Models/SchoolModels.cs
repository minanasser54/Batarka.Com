using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace MyMvcApp10.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }
    public bool IsTeacher { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(120)]
    public string? ClassName { get; set; }

    [MaxLength(120)]
    public string? Level { get; set; }

    public int? SchoolClassId { get; set; }
    public SchoolClass? SchoolClass { get; set; }

    public int? SchoolLevelId { get; set; }
    public SchoolLevel? SchoolLevel { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? QrCodeValue { get; set; }

    public int GradingBalance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public virtual ICollection<AttendanceRecord> AttendanceRecordedBy { get; set; } = new List<AttendanceRecord>();
    public virtual ICollection<GradingTransaction> GradingTransactions { get; set; } = new List<GradingTransaction>();
    public virtual ICollection<GradingTransaction> GradingTransactionsRecordedBy { get; set; } = new List<GradingTransaction>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<AttendanceSession> SessionsCreated { get; set; } = new List<AttendanceSession>();
}

public class SchoolLevel
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public ICollection<SchoolClass> Classes { get; set; } = new List<SchoolClass>();
    public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
}

public class SchoolClass
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public int SchoolLevelId { get; set; }
    public SchoolLevel SchoolLevel { get; set; } = null!;
    public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
}

public class AttendanceSession
{
    public int Id { get; set; }

    [Required]
    public DateTime SessionDate { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "الجلوس اليومي";

    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Open";

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

public class AttendanceRecord
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public AttendanceSession Session { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public DateTime AttendanceTimeUtc { get; set; }

    public string RecordedById { get; set; } = string.Empty;
    public ApplicationUser? RecordedBy { get; set; }

    [Required]
    [MaxLength(40)]
    public string RegistrationMethod { get; set; } = "Qr";

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "Present";

    public string? CorrectionReason { get; set; }
    public string? CorrectedById { get; set; }
    public ApplicationUser? CorrectedBy { get; set; }
    public DateTime? CorrectedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<AttendanceQuestionnaireSubmission> QuestionnaireSubmissions { get; set; } = new List<AttendanceQuestionnaireSubmission>();
}

public class AttendanceQuestionDefinition
{
    public int Id { get; set; }

    [Required]
    [MaxLength(250)]
    public string DisplayText { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }

    public int? StartDayOfMonth { get; set; }
    public int? EndDayOfMonth { get; set; }
    public int PointsAwarded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class AttendanceQuestionnaireSubmission
{
    public int Id { get; set; }

    public int AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    public string SubmittedById { get; set; } = string.Empty;
    public ApplicationUser? SubmittedBy { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Submitted";
    public string? CorrectionReason { get; set; }

    public ICollection<AttendanceQuestionAnswer> Answers { get; set; } = new List<AttendanceQuestionAnswer>();
}

public class AttendanceQuestionAnswer
{
    public int Id { get; set; }

    public int SubmissionId { get; set; }
    public AttendanceQuestionnaireSubmission Submission { get; set; } = null!;

    public int QuestionId { get; set; }
    public AttendanceQuestionDefinition Question { get; set; } = null!;

    public bool AnswerValue { get; set; }
    public int PointsAwarded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GradingTransaction
{
    public int Id { get; set; }

    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;

    public string TeacherId { get; set; } = string.Empty;
    public ApplicationUser Teacher { get; set; } = null!;

    public int PointsDelta { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string SourceType { get; set; } = "Manual";

    public int? SourceId { get; set; }

    [Required]
    [MaxLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ReversalOfTransactionId { get; set; }
    public GradingTransaction? ReversalOfTransaction { get; set; }
    public string? CorrectionReason { get; set; }
    public string? AdminNote { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }

    public string ActorId { get; set; } = string.Empty;
    public ApplicationUser? Actor { get; set; }

    [Required]
    [MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TargetType { get; set; }

    [MaxLength(200)]
    public string? TargetId { get; set; }

    public string? Details { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApplicationSetting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    [Display(Name = "اسم المستخدم")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "الاسم كامل مطلوب")]
    [Display(Name = "الاسم بالكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    [Display(Name = "اسم المستخدم")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور لازم تكون 6 أحرف أو أكتر")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور")]
    [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها مش متطابقة")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "الفصل")]
    public int? SchoolClassId { get; set; }

    [Display(Name = "المرحلة")]
    public int? SchoolLevelId { get; set; }

    public bool IsTeacher { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BulkStudentImportViewModel
{
    [Required(ErrorMessage = "اختار الفصل")]
    [Display(Name = "الفصل")]
    public int SchoolClassId { get; set; }

    [Required(ErrorMessage = "اكتب بيانات المخدومين")]
    [Display(Name = "بيانات المخدومين")]
    public string Rows { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "كلمة المرور لازم تكون 8 أحرف على الأقل")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "أكد كلمة المرور")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور مش متطابقين")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AttendanceQuestionEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "كود السؤال مطلوب")]
    [MaxLength(80)]
    [Display(Name = "كود السؤال")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "نص السؤال مطلوب")]
    [MaxLength(250)]
    [Display(Name = "نص السؤال")]
    public string DisplayText { get; set; } = string.Empty;

    [Range(0, 1000, ErrorMessage = "النقاط لازم تكون بين 0 و1000")]
    [Display(Name = "النقاط")]
    public int PointsAwarded { get; set; }

    [Display(Name = "السؤال مفعّل")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "ترتيب العرض")]
    public int SortOrder { get; set; }

    [Range(1, 31)]
    [Display(Name = "من يوم")]
    public int? StartDayOfMonth { get; set; }

    [Range(1, 31)]
    [Display(Name = "لحد يوم")]
    public int? EndDayOfMonth { get; set; }
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم كامل مطلوب")]
    [Display(Name = "الاسم بالكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    [Display(Name = "اسم المستخدم")]
    public string UserName { get; set; } = string.Empty;

    [Display(Name = "رقم الموبايل")]
    public string? Mobile { get; set; }

    [Display(Name = "العنوان")]
    public string? Address { get; set; }

    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    [Display(Name = "الفصل")]
    public int? SchoolClassId { get; set; }

    [Display(Name = "المرحلة")]
    public int? SchoolLevelId { get; set; }

    [Display(Name = "الحساب نشط")]
    public bool IsActive { get; set; }
}

public class AttendanceSearchResultViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? Level { get; set; }
    public string? QrCodeValue { get; set; }
}

public class AdminStudentListItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public string? ClassName { get; set; }
    public string? Level { get; set; }
    public int? SchoolLevelId { get; set; }
    public int GradingBalance { get; set; }
    public int TotalAttendance { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AttendancePageViewModel
{
    public DateTime Today { get; set; }
    public DateTime SelectedDate { get; set; }
    public int ActiveStudentCount { get; set; }
    public int ActiveTeacherCount { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public Dictionary<int, string> LocalAttendanceTimes { get; set; } = new();
    public List<AttendanceRecord> TodayAttendance { get; set; } = new();
    public List<AttendanceRecord> StudentAttendance { get; set; } = new();
    public List<AttendanceRecord> TeacherAttendance { get; set; } = new();
    public List<ApplicationUser> MissingPeople { get; set; } = new();
}

public class HomeLandingViewModel
{
    public List<ApplicationUser> TopStudents { get; set; } = new();
    public LoginViewModel Login { get; set; } = new();
}

public class AdminAuditListItemViewModel
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string? ActorUserName { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Details { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

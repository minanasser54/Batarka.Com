using System.ComponentModel.DataAnnotations;

namespace MyMvcApp10.Models;

public class ApiLoginRequest
{
    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ApiRefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ApiChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 4)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ApiRegisterAttendanceRequest
{
    [Required]
    public string QrCodeValue { get; set; } = string.Empty;

    public DateOnly? AttendanceDate { get; set; }
}

public class ApiApplyGradeRequest
{
    [Required]
    [RegularExpression("^(green|red|black)$")]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class ApiApplyGradeResponse
{
    public int PointsDelta { get; set; }
    public int NewBalance { get; set; }
}

public class ApiUserProfile
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsTeacher { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public string? Mobile { get; set; }
    public string? Mobile2 { get; set; }
    public string? Address { get; set; }
    public string? StreetNumber { get; set; }
    public string? ApartmentNumber { get; set; }
    public string? Building { get; set; }
    public string? Landmark { get; set; }
    public string? AddressNotes { get; set; }
    public string? Landline { get; set; }
    public int? BirthDay { get; set; }
    public int? BirthMonth { get; set; }
    public int? BirthYear { get; set; }
    public string? ConfessionFather { get; set; }
    public int? StudyStage { get; set; }
    public string? SchoolOrUniversity { get; set; }
    public string? Gender { get; set; }
    public string? Occupation { get; set; }
    public string? FatherOccupation { get; set; }
    public string? MotherOccupation { get; set; }
    public string? MotherName { get; set; }
    public string? OtherServices { get; set; }
    public string? NationalId { get; set; }
    public string? GpsLink { get; set; }
    public string? AreaCode { get; set; }
    public string? Notes { get; set; }
    public string? ClassName { get; set; }
    public string? Level { get; set; }
    public int GradingBalance { get; set; }
    public string? QrCodeValue { get; set; }
}

public class ApiStudentSummary
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Mobile { get; set; }
    public string? ClassName { get; set; }
    public string? Level { get; set; }
    public bool IsActive { get; set; }
    public int GradingBalance { get; set; }
}

public class ApiPagedResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}

public class ApiAttendanceItem
{
    public int Id { get; set; }
    public DateTime AttendanceTimeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RegistrationMethod { get; set; } = string.Empty;
    public string? SessionName { get; set; }
}

public class ApiNotificationItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public class ApiGalleryEvent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ImageCount { get; set; }
    public IReadOnlyList<ApiGalleryImage> Images { get; set; } = Array.Empty<ApiGalleryImage>();
}

public class ApiGalleryImage
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class ApiTeamItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public int MemberCount { get; set; }
    public int TotalBalance { get; set; }
    public int BoughtCardsCount { get; set; }
    public IReadOnlyList<string> Members { get; set; } = Array.Empty<string>();
}

public class ApiMarketCardItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Price { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsSold { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}
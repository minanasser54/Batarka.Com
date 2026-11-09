using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers.Api.V1;

[ApiController]
[Route("api/v1/me")]
[Authorize(AuthenticationSchemes = ApiAuthSchemes.Bearer)]
public sealed class ProfileApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    [ProducesResponseType<ApiUserProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiUserProfile>> GetProfile()
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var user = await _db.Users.AsNoTracking()
            .Include(item => item.SchoolClass)
            .Include(item => item.SchoolLevel)
            .FirstOrDefaultAsync(item => item.Id == userId && item.DeletedAt == null);
        if (user == null) return Unauthorized();
        return Ok(ApiUserMapper.ToProfile(user));
    }
}

[ApiController]
[Route("api/v1/students")]
[Authorize(AuthenticationSchemes = ApiAuthSchemes.Bearer)]
public sealed class StudentsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentsApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    [ProducesResponseType<ApiPagedResponse<ApiStudentSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiPagedResponse<ApiStudentSummary>>> List(
        string? search = null,
        int? schoolClassId = null,
        int? schoolLevelId = null,
        bool activeOnly = true,
        int page = 1,
        int pageSize = 50)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();
        if (!current.IsAdmin && !current.IsTeacher) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Users.AsNoTracking()
            .Where(item => !item.IsAdmin && !item.IsTeacher && item.DeletedAt == null);
        if (activeOnly) query = query.Where(item => item.IsActive);
        if (schoolClassId.HasValue) query = query.Where(item => item.SchoolClassId == schoolClassId.Value);
        if (schoolLevelId.HasValue) query = query.Where(item => item.SchoolLevelId == schoolLevelId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.FullName.Contains(term)
                || (item.UserName != null && item.UserName.Contains(term))
                || (item.Mobile != null && item.Mobile.Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var items = await query.OrderBy(item => item.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ApiStudentSummary
            {
                Id = item.Id,
                FullName = item.FullName,
                UserName = item.UserName,
                Mobile = item.Mobile,
                ClassName = item.SchoolClass != null ? item.SchoolClass.Name : item.ClassName,
                Level = item.SchoolLevel != null ? item.SchoolLevel.Name : item.Level,
                IsActive = item.IsActive,
                GradingBalance = item.GradingBalance
            })
            .ToListAsync();

        return Ok(new ApiPagedResponse<ApiStudentSummary>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        });
    }

    [HttpGet("{id}")]
    [ProducesResponseType<ApiUserProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiUserProfile>> GetStudent(string id)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Unauthorized();
        if (!current.IsAdmin && !current.IsTeacher && current.Id != id) return Forbid();

        var student = await _db.Users.AsNoTracking()
            .Include(item => item.SchoolClass)
            .Include(item => item.SchoolLevel)
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsAdmin && !item.IsTeacher && item.DeletedAt == null);
        return student == null ? NotFound() : Ok(ApiUserMapper.ToProfile(student));
    }
}

internal static class ApiUserMapper
{
    public static ApiUserProfile ToProfile(ApplicationUser user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        UserName = user.UserName,
        Email = user.Email,
        IsAdmin = user.IsAdmin,
        IsTeacher = user.IsTeacher,
        IsActive = user.IsActive,
        ImageUrl = user.ImageUrl,
        Mobile = user.Mobile,
        Mobile2 = user.Mobile2,
        Address = user.Address,
        StreetNumber = user.StreetNumber,
        ApartmentNumber = user.ApartmentNumber,
        Building = user.Building,
        Landmark = user.Landmark,
        AddressNotes = user.AddressNotes,
        Landline = user.Landline,
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
        GpsLink = user.GpsLink,
        AreaCode = user.AreaCode,
        Notes = user.Notes,
        ClassName = user.SchoolClass?.Name ?? user.ClassName,
        Level = user.SchoolLevel?.Name ?? user.Level,
        GradingBalance = user.GradingBalance,
        QrCodeValue = user.QrCodeValue
    };
}
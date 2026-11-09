using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Controllers.Api.V1;

[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public sealed class PublicApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PublicApiController(ApplicationDbContext db) => _db = db;

    [HttpGet("gallery/events")]
    [ProducesResponseType<IReadOnlyList<ApiGalleryEvent>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiGalleryEvent>>> GalleryEvents()
    {
        var events = await _db.GalleryEvents.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new ApiGalleryEvent
            {
                Id = item.Id,
                Name = item.Name,
                CreatedAt = item.CreatedAt,
                ImageCount = item.Images.Count,
                Images = item.Images.OrderByDescending(image => image.CreatedAt)
                    .Select(image => new ApiGalleryImage
                    {
                        Id = image.Id,
                        FileName = image.OriginalFileName,
                        CreatedAt = image.CreatedAt,
                        Url = "/api/v1/gallery/images/" + image.Id
                    })
                    .ToList()
            })
            .Where(item => item.ImageCount > 0)
            .ToListAsync();

        return Ok(events);
    }

    [HttpGet("gallery/images/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GalleryImage(int id)
    {
        var image = await _db.GalleryImages.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.ImageData, item.ContentType })
            .FirstOrDefaultAsync();
        if (image == null) return NotFound();
        Response.Headers.CacheControl = "public,max-age=3600";
        return File(image.ImageData, image.ContentType);
    }

    [HttpGet("leaderboard")]
    [ProducesResponseType<IReadOnlyList<ApiStudentSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiStudentSummary>>> Leaderboard(int take = 10)
    {
        take = Math.Clamp(take, 1, 50);
        var students = await _db.Users.AsNoTracking()
            .Where(user => user.IsActive && !user.IsAdmin && !user.IsTeacher && user.DeletedAt == null)
            .OrderByDescending(user => user.GradingBalance)
            .ThenBy(user => user.FullName)
            .Take(take)
            .Select(user => new ApiStudentSummary
            {
                Id = user.Id,
                FullName = user.FullName,
                ClassName = user.SchoolClass != null ? user.SchoolClass.Name : user.ClassName,
                Level = user.SchoolLevel != null ? user.SchoolLevel.Name : user.Level,
                IsActive = user.IsActive,
                GradingBalance = user.GradingBalance
            })
            .ToListAsync();
        return Ok(students);
    }

    [HttpGet("teams")]
    [ProducesResponseType<IReadOnlyList<ApiTeamItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiTeamItem>>> Teams()
    {
        var teams = await _db.Teams.AsNoTracking()
            .Select(team => new ApiTeamItem
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                LogoUrl = team.LogoUrl,
                MemberCount = team.Members.Count,
                TotalBalance = team.Members.Sum(member => (int?)member.Student.GradingBalance) ?? 0,
                BoughtCardsCount = team.BoughtCards.Count(card => card.IsSold),
                Members = team.Members.OrderBy(member => member.Student.FullName)
                    .Select(member => member.Student.FullName).ToList()
            })
            .OrderByDescending(team => team.TotalBalance)
            .ToListAsync();
        return Ok(teams);
    }

    [HttpGet("market/cards")]
    [ProducesResponseType<IReadOnlyList<ApiMarketCardItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiMarketCardItem>>> MarketCards(string? search = null, int? categoryId = null)
    {
        var query = _db.MagicCards.AsNoTracking().AsQueryable();
        if (categoryId.HasValue) query = query.Where(card => card.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(card => card.Title.Contains(term) || (card.Description != null && card.Description.Contains(term)));
        }

        var cards = await query.OrderBy(card => card.IsSold).ThenByDescending(card => card.CreatedAt)
            .Take(100)
            .Select(card => new ApiMarketCardItem
            {
                Id = card.Id,
                Title = card.Title,
                Description = card.Description,
                Price = card.Price,
                ImageUrl = card.ImageUrl,
                Category = card.Category.Name,
                IsSold = card.IsSold,
                IsUsed = card.IsUsed,
                CreatedAt = card.CreatedAt
            })
            .ToListAsync();
        return Ok(cards);
    }
}
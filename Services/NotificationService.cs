using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;

namespace MyMvcApp10.Services;

public interface INotificationService
{
    Task SendNotificationAsync(string title, string message, string type, string targetType, string? targetUserId = null, int? targetTeamId = null, string? linkUrl = null, string? createdById = null);
    Task SendToUserAsync(string userId, string title, string message, string type, string? linkUrl = null);
    Task SendToTeamAsync(int teamId, string title, string message, string type, string? linkUrl = null);
    Task SendToRoleAsync(string role, string title, string message, string type, string? linkUrl = null);
    Task SendToAllAsync(string title, string message, string type, string? linkUrl = null);
    Task<int> GetUnreadCountAsync(string userId, bool isAdmin, bool isTeacher);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task SendNotificationAsync(string title, string message, string type, string targetType, string? targetUserId = null, int? targetTeamId = null, string? linkUrl = null, string? createdById = null)
    {
        var notification = new AppNotification
        {
            Title = title,
            Message = message,
            NotificationType = type,
            TargetType = targetType,
            TargetUserId = targetUserId,
            TargetTeamId = targetTeamId,
            LinkUrl = linkUrl,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };

        _db.AppNotifications.Add(notification);
        await _db.SaveChangesAsync();
    }

    public Task SendToUserAsync(string userId, string title, string message, string type, string? linkUrl = null)
    {
        return SendNotificationAsync(title, message, type, "SpecificUser", targetUserId: userId, linkUrl: linkUrl);
    }

    public Task SendToTeamAsync(int teamId, string title, string message, string type, string? linkUrl = null)
    {
        return SendNotificationAsync(title, message, type, "SpecificTeam", targetTeamId: teamId, linkUrl: linkUrl);
    }

    public Task SendToRoleAsync(string role, string title, string message, string type, string? linkUrl = null)
    {
        return SendNotificationAsync(title, message, type, role, linkUrl: linkUrl);
    }

    public Task SendToAllAsync(string title, string message, string type, string? linkUrl = null)
    {
        return SendNotificationAsync(title, message, type, "All", linkUrl: linkUrl);
    }

    public async Task<int> GetUnreadCountAsync(string userId, bool isAdmin, bool isTeacher)
    {
        var userTeamId = await _db.TeamMembers
            .Where(tm => tm.StudentId == userId)
            .Select(tm => (int?)tm.TeamId)
            .FirstOrDefaultAsync();

        var query = _db.AppNotifications.AsNoTracking().Where(n =>
            n.TargetType == "All" ||
            (n.TargetType == "SpecificUser" && n.TargetUserId == userId) ||
            (n.TargetType == "Students" && !isAdmin && !isTeacher) ||
            (n.TargetType == "Teachers" && isTeacher) ||
            (n.TargetType == "Admins" && isAdmin) ||
            (n.TargetType == "SpecificTeam" && userTeamId.HasValue && n.TargetTeamId == userTeamId.Value));

        var readIds = await _db.UserNotificationReads
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.NotificationId)
            .ToListAsync();

        return await query.CountAsync(n => !readIds.Contains(n.Id));
    }
}

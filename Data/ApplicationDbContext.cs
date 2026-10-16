using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Models;

namespace MyMvcApp10.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceQuestionDefinition> AttendanceQuestionDefinitions => Set<AttendanceQuestionDefinition>();
    public DbSet<AttendanceQuestionnaireSubmission> AttendanceQuestionnaireSubmissions => Set<AttendanceQuestionnaireSubmission>();
    public DbSet<AttendanceQuestionAnswer> AttendanceQuestionAnswers => Set<AttendanceQuestionAnswer>();
    public DbSet<GradingTransaction> GradingTransactions => Set<GradingTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();
    public DbSet<SchoolLevel> SchoolLevels => Set<SchoolLevel>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();

    // Games Market & Teams
    public DbSet<MarketCategory> MarketCategories => Set<MarketCategory>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<MagicCard> MagicCards => Set<MagicCard>();
    public DbSet<MagicCardPurchaseDeduction> MagicCardPurchaseDeductions => Set<MagicCardPurchaseDeduction>();
    public DbSet<MarketCardReaction> MarketCardReactions => Set<MarketCardReaction>();
    public DbSet<MarketCardComment> MarketCardComments => Set<MarketCardComment>();

    // Notifications
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();
    public DbSet<UserNotificationRead> UserNotificationReads => Set<UserNotificationRead>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => x.QrCodeValue).IsUnique();
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.IsTeacher);
            entity.HasIndex(x => x.IsAdmin);
            entity.HasIndex(x => x.FullName);
            entity.HasIndex(x => x.ClassName);
            entity.HasIndex(x => x.Level);
            entity.HasOne(x => x.SchoolLevel)
                .WithMany(x => x.Students)
                .HasForeignKey(x => x.SchoolLevelId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.SchoolClass)
                .WithMany(x => x.Students)
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SchoolLevel>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<SchoolClass>(entity =>
        {
            entity.HasIndex(x => new { x.SchoolLevelId, x.Name }).IsUnique();
            entity.HasOne(x => x.SchoolLevel)
                .WithMany(x => x.Classes)
                .HasForeignKey(x => x.SchoolLevelId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.AttendanceTimeUtc);
            entity.HasIndex(x => x.Status);

            entity.HasOne(x => x.User)
                .WithMany(x => x.AttendanceRecords)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RecordedBy)
                .WithMany(x => x.AttendanceRecordedBy)
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

            builder.Entity<AttendanceQuestionnaireSubmission>(entity =>
            {
                entity.HasOne(x => x.SubmittedBy)
                .WithMany()
                .HasForeignKey(x => x.SubmittedById)
                .OnDelete(DeleteBehavior.NoAction);
            });

        builder.Entity<AttendanceSession>(entity =>
        {
            entity.HasIndex(x => x.SessionDate);
            entity.HasOne(x => x.CreatedBy)
                .WithMany(x => x.SessionsCreated)
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AttendanceQuestionAnswer>(entity =>
        {
            entity.HasIndex(x => new { x.SubmissionId, x.QuestionId }).IsUnique();
        });

        builder.Entity<GradingTransaction>(entity =>
        {
            entity.HasIndex(x => x.StudentId);
            entity.HasIndex(x => x.TeacherId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasOne(x => x.Student)
                .WithMany(x => x.GradingTransactions)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Teacher)
                .WithMany(x => x.GradingTransactionsRecordedBy)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationSetting>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Action);
        });

        // Games Market & Teams
        builder.Entity<MarketCategory>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Team>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<TeamMember>(entity =>
        {
            // Requirement 10: same student can join single team only
            entity.HasIndex(x => x.StudentId).IsUnique();
            entity.HasOne(x => x.Team)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MagicCard>(entity =>
        {
            entity.HasIndex(x => x.CategoryId);
            entity.HasIndex(x => x.IsSold);
            entity.HasIndex(x => x.IsUsed);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Cards)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BoughtByTeam)
                .WithMany(x => x.BoughtCards)
                .HasForeignKey(x => x.BoughtByTeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MagicCardPurchaseDeduction>(entity =>
        {
            entity.HasOne(x => x.Card)
                .WithMany(x => x.PurchaseDeductions)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MarketCardReaction>(entity =>
        {
            entity.HasIndex(x => new { x.CardId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Card)
                .WithMany(x => x.Reactions)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<MarketCardComment>(entity =>
        {
            entity.HasIndex(x => x.CardId);
            entity.HasOne(x => x.Card)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Notifications
        builder.Entity<AppNotification>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.TargetType);
            entity.HasIndex(x => x.TargetUserId);
            entity.HasOne(x => x.TargetUser)
                .WithMany()
                .HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.TargetTeam)
                .WithMany()
                .HasForeignKey(x => x.TargetTeamId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UserNotificationRead>(entity =>
        {
            entity.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Notification)
                .WithMany(x => x.Reads)
                .HasForeignKey(x => x.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}

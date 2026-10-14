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
        builder.Entity<SchoolClass>().HasIndex(x => new { x.SchoolLevelId, x.Name }).IsUnique();

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
    }
}

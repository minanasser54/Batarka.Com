using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Models;

namespace MyMvcApp10.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services, bool isDevelopment)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        await EnsureRoleAsync(roleManager, "Admin");
        await EnsureRoleAsync(roleManager, "Teacher");
        await EnsureRoleAsync(roleManager, "Student");

        var settings = await context.ApplicationSettings.ToListAsync();
        if (!settings.Any(x => x.Key == "Timezone"))
            context.ApplicationSettings.Add(new ApplicationSetting { Key = "Timezone", Value = "Africa/Cairo" });
        if (!settings.Any(x => x.Key == "NegativeBalanceAllowed"))
            context.ApplicationSettings.Add(new ApplicationSetting { Key = "NegativeBalanceAllowed", Value = "true" });
        if (!settings.Any(x => x.Key == "AttendanceSessionName"))
            context.ApplicationSettings.Add(new ApplicationSetting { Key = "AttendanceSessionName", Value = "حضور مدرسة الأحد" });

        var questions = await context.AttendanceQuestionDefinitions.ToListAsync();
        if (!questions.Any(x => x.Code == "mass"))
            context.AttendanceQuestionDefinitions.Add(new AttendanceQuestionDefinition { Code = "mass", DisplayText = "حضرت القداس؟", PointsAwarded = 1, IsEnabled = true, SortOrder = 1 });
        if (!questions.Any(x => x.Code == "samamsha"))
            context.AttendanceQuestionDefinitions.Add(new AttendanceQuestionDefinition { Code = "samamsha", DisplayText = "كنت في مدارس الشمامسة؟", PointsAwarded = 1, IsEnabled = true, SortOrder = 2 });
        if (!questions.Any(x => x.Code == "confession"))
            context.AttendanceQuestionDefinitions.Add(new AttendanceQuestionDefinition { Code = "confession", DisplayText = "اعترفت؟", PointsAwarded = 1, IsEnabled = true, StartDayOfMonth = 1, EndDayOfMonth = 7, SortOrder = 3 });

        await context.SaveChangesAsync();

        var adminUsername = Environment.GetEnvironmentVariable("SCHOOL_ADMIN_USERNAME") ?? "admin@church.local";
        var adminPassword = Environment.GetEnvironmentVariable("SCHOOL_ADMIN_PASSWORD");
        var adminUser = await userManager.FindByNameAsync(adminUsername);
        if (adminUser == null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException("Set SCHOOL_ADMIN_PASSWORD before creating the initial administrator.");
            }

            adminUser = new ApplicationUser
            {
                UserName = adminUsername,
                Email = adminUsername,
                FullName = "المدير الأساسي",
                IsAdmin = true,
                IsTeacher = true,
                IsActive = true,
                EmailConfirmed = true,
                QrCodeValue = Guid.NewGuid().ToString("N")
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Unable to create bootstrap admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        await EnsureUserInRoleAsync(userManager, adminUser, "Admin");
        await EnsureUserInRoleAsync(userManager, adminUser, "Teacher");

        if (!await context.AttendanceSessions.AnyAsync())
        {
            context.AttendanceSessions.Add(new AttendanceSession
            {
                Name = "حضور مدرسة الأحد",
                SessionDate = DateTime.UtcNow,
                Status = "Open",
                CreatedById = adminUser.Id,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        if (isDevelopment && !await context.Users.AnyAsync(u => u.UserName == "teacher1"))
        {
            var teacher = new ApplicationUser
            {
                UserName = "teacher1",
                Email = "teacher1@church.local",
                FullName = "الخادم مايكل",
                IsTeacher = true,
                IsActive = true,
                ClassName = "المرحلة الثانوية",
                Level = "الثانوية",
                EmailConfirmed = true,
                QrCodeValue = Guid.NewGuid().ToString("N")
            };
            await userManager.CreateAsync(teacher, "Teacher@123");
            await EnsureUserInRoleAsync(userManager, teacher, "Teacher");
        }

        if (isDevelopment && !await context.Users.AnyAsync(u => u.UserName == "student1"))
        {
            var student = new ApplicationUser
            {
                UserName = "student1",
                Email = "student1@church.local",
                FullName = "المخدوم مينا",
                IsTeacher = false,
                IsAdmin = false,
                IsActive = true,
                ClassName = "الفصل الثاني",
                Level = "الثانوية",
                EmailConfirmed = true,
                GradingBalance = 12,
                QrCodeValue = Guid.NewGuid().ToString("N")
            };
            await userManager.CreateAsync(student, "Student@123");
            await EnsureUserInRoleAsync(userManager, student, "Student");
        }

        await EnsureSchoolAssignmentsAsync(context);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private static async Task EnsureUserInRoleAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string roleName)
    {
        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }
    }

    private static async Task EnsureSchoolAssignmentsAsync(ApplicationDbContext context)
    {
        var legacyLevels = await context.Users
            .Where(x => x.Level != null && x.Level != string.Empty)
            .Select(x => x.Level!)
            .Distinct()
            .ToListAsync();

        foreach (var name in legacyLevels)
        {
            if (!await context.SchoolLevels.AnyAsync(x => x.Name == name))
            {
                context.SchoolLevels.Add(new SchoolLevel { Name = name, SortOrder = 0 });
            }
        }
        await context.SaveChangesAsync();

        var legacyUsers = await context.Users
            .Where(x => x.SchoolLevelId == null && x.Level != null && x.Level != string.Empty)
            .ToListAsync();
        foreach (var user in legacyUsers)
        {
            var level = await context.SchoolLevels.FirstAsync(x => x.Name == user.Level);
            user.SchoolLevelId = level.Id;
            if (!string.IsNullOrWhiteSpace(user.ClassName))
            {
                var schoolClass = await context.SchoolClasses.FirstOrDefaultAsync(x => x.SchoolLevelId == level.Id && x.Name == user.ClassName);
                if (schoolClass == null)
                {
                    schoolClass = new SchoolClass { Name = user.ClassName, SchoolLevelId = level.Id };
                    context.SchoolClasses.Add(schoolClass);
                    await context.SaveChangesAsync();
                }
                user.SchoolClassId = schoolClass.Id;
            }
        }
        await context.SaveChangesAsync();
    }
}

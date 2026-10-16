using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMvcApp10.Data;
using MyMvcApp10.Models;
using MyMvcApp10.Services;

namespace MyMvcApp10.Controllers;

[Authorize]
public class GamesMarketController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;

    public GamesMarketController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notificationService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    // 1. Marketplace public browsing page (Cards & buy)
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var currentUser = await GetCurrentUserAsync();
        var query = _db.MagicCards
            .Include(c => c.Category)
            .Include(c => c.Reactions).ThenInclude(r => r.User)
            .Include(c => c.Comments.Where(cm => !cm.IsDeleted)).ThenInclude(cm => cm.User)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Title.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }

        var cards = await query
            .OrderBy(c => c.IsSold)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync();

        // Check user's team & team balance
        Team? userTeam = null;
        int teamBalance = 0;

        if (currentUser != null)
        {
            var membership = await _db.TeamMembers
                .Include(tm => tm.Team)
                .ThenInclude(t => t.Members)
                .ThenInclude(m => m.Student)
                .FirstOrDefaultAsync(tm => tm.StudentId == currentUser.Id);

            if (membership != null)
            {
                userTeam = membership.Team;
                teamBalance = userTeam.Members.Sum(m => m.Student.GradingBalance);
            }
        }

        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await _db.MarketCategories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.UserTeam = userTeam;
        ViewBag.TeamBalance = teamBalance;
        ViewBag.CurrentUser = currentUser;

        return View(cards);
    }

    // 2. Buy Card for Team (Requirement 3 & 5 & 10)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyCard(int cardId)
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return RedirectToAction("Login", "Account");

        var membership = await _db.TeamMembers
            .Include(tm => tm.Team)
            .ThenInclude(t => t.Members)
            .ThenInclude(m => m.Student)
            .FirstOrDefaultAsync(tm => tm.StudentId == current.Id);

        if (membership == null)
        {
            TempData["ErrorMessage"] = "يجب أن تكون عضوًا في فريق حتى تتمكن من شراء كارت من سوق الألعاب.";
            return RedirectToAction(nameof(Index));
        }

        var team = membership.Team;
        if (team.Members.Count == 0)
        {
            TempData["ErrorMessage"] = "الفريق لا يحتوي على أعضاء.";
            return RedirectToAction(nameof(Index));
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var card = await _db.MagicCards.FirstOrDefaultAsync(c => c.Id == cardId);
            if (card == null)
            {
                TempData["ErrorMessage"] = "الكارت غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            if (card.IsSold)
            {
                TempData["WarningMessage"] = "هذا الكارت تم شراؤه مسبقًا وغير متاح.";
                return RedirectToAction(nameof(Index));
            }

            int teamBalance = team.Members.Sum(m => m.Student.GradingBalance);
            if (teamBalance < card.Price)
            {
                TempData["ErrorMessage"] = $"رصيد الفريق ({teamBalance} نقطة) لا يكفي لشراء هذا الكارت ({card.Price} نقطة).";
                return RedirectToAction(nameof(Index));
            }

            // Deduction calculation per Requirement 3:
            // "each individual_grades -= int(price/team members) but the whole item price should not be exceeded"
            int memberCount = team.Members.Count;
            int baseDeduction = card.Price / memberCount;
            int remainder = card.Price % memberCount;

            var membersList = team.Members.OrderBy(m => m.JoinedAt).ToList();
            int totalDeducted = 0;

            for (int i = 0; i < memberCount; i++)
            {
                var member = membersList[i];
                int memberDeduction = baseDeduction + (i < remainder ? 1 : 0);
                totalDeducted += memberDeduction;

                member.Student.GradingBalance -= memberDeduction;

                _db.GradingTransactions.Add(new GradingTransaction
                {
                    StudentId = member.Student.Id,
                    TeacherId = current.Id,
                    PointsDelta = -memberDeduction,
                    Reason = $"شراء كارت: {card.Title} للفريق ({team.Name})",
                    SourceType = "MagicCardPurchase",
                    IdempotencyKey = $"{member.Student.Id}:{card.Id}:buy:{DateTime.UtcNow.Ticks}",
                    CreatedAt = DateTime.UtcNow
                });

                _db.MagicCardPurchaseDeductions.Add(new MagicCardPurchaseDeduction
                {
                    CardId = card.Id,
                    StudentId = member.Student.Id,
                    PointsDeducted = memberDeduction,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Mark card as sold
            card.IsSold = true;
            card.BoughtByTeamId = team.Id;
            card.BoughtAt = DateTime.UtcNow;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorId = current.Id,
                Action = "MarketCardPurchased",
                TargetType = "MagicCard",
                TargetId = card.Id.ToString(),
                Details = $"قام فريق {team.Name} بشراء كارت {card.Title} بقيمة {card.Price} نقطة (إجمالي مخصوم: {totalDeducted}).",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Send notification to all team members
            await _notificationService.SendToTeamAsync(
                team.Id,
                "🎉 كارت سحري جديد للفريق!",
                $"قام {current.FullName} بشراء كارت [{card.Title}] لفريقكم {team.Name} بقيمة {card.Price} نقطة.",
                "Market",
                Url.Action("MyTeam", "GamesMarket")
            );

            TempData["SuccessMessage"] = $"مبروك! تم شراء كارت [{card.Title}] لفريق {team.Name} بنجاح وتم خصم {card.Price} نقطة بالتساوي بين الأعضاء.";
            return RedirectToAction(nameof(MyTeam));
        }
        catch
        {
            await tx.RollbackAsync();
            TempData["ErrorMessage"] = "حدث خطأ أثناء إتمام عملية الشراء. حاول مرة أخرى.";
            return RedirectToAction(nameof(Index));
        }
    }

    // 3. Teams Dashboard & Leaderboard (Requirement 11) - open to all
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Teams()
    {
        var teams = await _db.Teams
            .Include(t => t.Members).ThenInclude(m => m.Student)
            .Include(t => t.BoughtCards)
            .ToListAsync();

        var teamViewModels = teams.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            t.LogoUrl,
            MemberCount = t.Members.Count,
            TotalBalance = t.Members.Sum(m => m.Student.GradingBalance),
            BoughtCardsCount = t.BoughtCards.Count(c => c.IsSold),
            ActiveCardsCount = t.BoughtCards.Count(c => c.IsSold && !c.IsUsed),
            Members = t.Members.Select(m => m.Student.FullName).ToList()
        })
        .OrderByDescending(t => t.TotalBalance)
        .ToList();

        ViewBag.Teams = teamViewModels;
        return View();
    }

    // 4. My Team Page (Requirement 11)
    [HttpGet]
    public async Task<IActionResult> MyTeam()
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return RedirectToAction("Login", "Account");

        var membership = await _db.TeamMembers
            .Include(tm => tm.Team)
                .ThenInclude(t => t.Members)
                .ThenInclude(m => m.Student)
            .Include(tm => tm.Team)
                .ThenInclude(t => t.BoughtCards)
                .ThenInclude(c => c.Category)
            .FirstOrDefaultAsync(tm => tm.StudentId == current.Id);

        if (membership == null)
        {
            ViewBag.HasTeam = false;
            return View();
        }

        ViewBag.HasTeam = true;
        var team = membership.Team;
        ViewBag.Team = team;
        ViewBag.TotalBalance = team.Members.Sum(m => m.Student.GradingBalance);
        ViewBag.BoughtCards = team.BoughtCards.OrderByDescending(c => c.BoughtAt).ToList();

        return View();
    }

    // 5. Reacts (Requirement 12)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReaction(int cardId, string reactionType)
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return Json(new { success = false, message = "سجل الدخول أولاً." });

        reactionType = string.IsNullOrWhiteSpace(reactionType) ? "like" : reactionType.Trim().ToLowerInvariant();

        var existing = await _db.MarketCardReactions
            .FirstOrDefaultAsync(r => r.CardId == cardId && r.UserId == current.Id);

        if (existing != null)
        {
            if (existing.ReactionType == reactionType)
            {
                // Remove reaction if clicked again
                _db.MarketCardReactions.Remove(existing);
            }
            else
            {
                // Switch reaction
                existing.ReactionType = reactionType;
                existing.CreatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            _db.MarketCardReactions.Add(new MarketCardReaction
            {
                CardId = cardId,
                UserId = current.Id,
                ReactionType = reactionType,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        var reactions = await _db.MarketCardReactions
            .Where(r => r.CardId == cardId)
            .GroupBy(r => r.ReactionType)
            .Select(g => new { type = g.Key, count = g.Count() })
            .ToDictionaryAsync(g => g.type, g => g.count);

        return Json(new { success = true, reactions });
    }

    // 6. Comments (Requirement 12)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int cardId, string content)
    {
        var current = await GetCurrentUserAsync();
        if (current == null) return Json(new { success = false, message = "سجل الدخول أولاً." });

        if (string.IsNullOrWhiteSpace(content))
        {
            return Json(new { success = false, message = "لا يمكن إضافة تعليق فارغ." });
        }

        var card = await _db.MagicCards.FindAsync(cardId);
        if (card == null) return Json(new { success = false, message = "الكارت غير موجود." });

        var comment = new MarketCardComment
        {
            CardId = cardId,
            UserId = current.Id,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.MarketCardComments.Add(comment);
        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            comment = new
            {
                id = comment.Id,
                userName = current.FullName,
                content = comment.Content,
                createdAt = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            }
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int commentId)
    {
        var comment = await _db.MarketCardComments.FindAsync(commentId);
        if (comment != null)
        {
            comment.IsDeleted = true;
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        return Json(new { success = false, message = "التعليق غير موجود." });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReaction(int reactionId)
    {
        var reaction = await _db.MarketCardReactions.FindAsync(reactionId);
        if (reaction != null)
        {
            _db.MarketCardReactions.Remove(reaction);
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        return Json(new { success = false });
    }

    // -------------------------------------------------------------
    // ADMIN ONLY MANAGEMENT (Requirements 4, 6, 7, 8, 10)
    // -------------------------------------------------------------

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageCards()
    {
        var cards = await _db.MagicCards
            .Include(c => c.Category)
            .Include(c => c.BoughtByTeam)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View(cards);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCard()
    {
        ViewBag.Categories = await _db.MarketCategories.OrderBy(c => c.Name).ToListAsync();
        return View(new MagicCardCreateEditViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCard(MagicCardCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.MarketCategories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        var card = new MagicCard
        {
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            Price = model.Price,
            ImageUrl = model.ImageUrl?.Trim(),
            CategoryId = model.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _db.MagicCards.Add(card);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم إضافة الكارت الجديد في سوق الألعاب بنجاح.";
        return RedirectToAction(nameof(ManageCards));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditCard(int id)
    {
        var card = await _db.MagicCards.FindAsync(id);
        if (card == null) return NotFound();

        ViewBag.Categories = await _db.MarketCategories.OrderBy(c => c.Name).ToListAsync();
        return View(new MagicCardCreateEditViewModel
        {
            Id = card.Id,
            Title = card.Title,
            Description = card.Description,
            Price = card.Price,
            ImageUrl = card.ImageUrl,
            CategoryId = card.CategoryId
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCard(MagicCardCreateEditViewModel model)
    {
        if (!model.Id.HasValue) return NotFound();
        var card = await _db.MagicCards.FindAsync(model.Id.Value);
        if (card == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.MarketCategories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        card.Title = model.Title.Trim();
        card.Description = model.Description?.Trim();
        card.Price = model.Price;
        card.ImageUrl = model.ImageUrl?.Trim();
        card.CategoryId = model.CategoryId;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم تعديل بيانات الكارت بنجاح.";
        return RedirectToAction(nameof(ManageCards));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCard(int id)
    {
        var card = await _db.MagicCards.FindAsync(id);
        if (card != null)
        {
            _db.MagicCards.Remove(card);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الكارت بنجاح.";
        }
        return RedirectToAction(nameof(ManageCards));
    }

    // Requirement 7: Admin marks card as Used
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCardAsUsed(int id)
    {
        var card = await _db.MagicCards.Include(c => c.BoughtByTeam).FirstOrDefaultAsync(c => c.Id == id);
        if (card == null) return NotFound();

        card.IsUsed = true;
        card.UsedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = (await GetCurrentUserAsync())!.Id,
            Action = "MarketCardUsed",
            TargetType = "MagicCard",
            TargetId = card.Id.ToString(),
            Details = $"تم استهلاك كارت {card.Title} بواسطة المشرف للفريق {card.BoughtByTeam?.Name}.",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        if (card.BoughtByTeamId.HasValue)
        {
            await _notificationService.SendToTeamAsync(
                card.BoughtByTeamId.Value,
                "✨ تم استهلاك كارت سحري",
                $"تم استخدام كارت [{card.Title}] رسميًا بمعرفة المشرف.",
                "Market"
            );
        }

        TempData["SuccessMessage"] = $"تم تعليم كارت [{card.Title}] كمستخدم (تم التنفيذ).";
        return RedirectToAction(nameof(ManageCards));
    }

    // Requirement 8: Admin removes ownership and auto-returns grades to students!
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefundCard(int id)
    {
        var card = await _db.MagicCards
            .Include(c => c.BoughtByTeam)
            .Include(c => c.PurchaseDeductions).ThenInclude(d => d.Student)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card == null) return NotFound();
        if (!card.IsSold || card.BoughtByTeamId == null)
        {
            TempData["WarningMessage"] = "الكارت غير مباع لأي فريق.";
            return RedirectToAction(nameof(ManageCards));
        }

        var team = card.BoughtByTeam;
        var teamId = card.BoughtByTeamId.Value;
        var deductions = card.PurchaseDeductions.ToList();

        // Auto return grades to students
        foreach (var deduction in deductions)
        {
            var student = deduction.Student ?? await _db.Users.FindAsync(deduction.StudentId);
            if (student != null)
            {
                student.GradingBalance += deduction.PointsDeducted;

                _db.GradingTransactions.Add(new GradingTransaction
                {
                    StudentId = student.Id,
                    TeacherId = (await GetCurrentUserAsync())!.Id,
                    PointsDelta = deduction.PointsDeducted,
                    Reason = $"إلغاء شراء كارت: {card.Title} وإعادة الدرجات المستقطعة",
                    SourceType = "MarketRefund",
                    IdempotencyKey = $"{student.Id}:{card.Id}:refund:{DateTime.UtcNow.Ticks}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _db.MagicCardPurchaseDeductions.RemoveRange(deductions);

        // Reset card state
        card.IsSold = false;
        card.BoughtByTeamId = null;
        card.BoughtAt = null;
        card.IsUsed = false;
        card.UsedAt = null;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = (await GetCurrentUserAsync())!.Id,
            Action = "MarketCardRefunded",
            TargetType = "MagicCard",
            TargetId = card.Id.ToString(),
            Details = $"تم إلغاء شراء كارت {card.Title} لفريق {team?.Name} وإعادة الدرجات للأعضاء تلقائيًا.",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        await _notificationService.SendToTeamAsync(
            teamId,
            "↩️ تم استرجاع كارت سحري",
            $"تم إلغاء شراء كارت [{card.Title}] من قِبل المشرف وإعادة الدرجات المستقطعة إلى أرصدتكم كاملة.",
            "Market"
        );

        TempData["SuccessMessage"] = $"تم إلغاء شراء الكارت وإعادة الدرجات كاملة لجميع أعضاء الفريق تلقائيًا.";
        return RedirectToAction(nameof(ManageCards));
    }

    // Categories Management
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Categories()
    {
        var categories = await _db.MarketCategories
            .Include(c => c.Cards)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "اسم القسم مطلوب.";
            return RedirectToAction(nameof(Categories));
        }

        if (await _db.MarketCategories.AnyAsync(c => c.Name == name.Trim()))
        {
            TempData["ErrorMessage"] = "هذا القسم موجود بالفعل.";
            return RedirectToAction(nameof(Categories));
        }

        _db.MarketCategories.Add(new MarketCategory
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم إضافة القسم بنجاح.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.MarketCategories.Include(c => c.Cards).FirstOrDefaultAsync(c => c.Id == id);
        if (category != null)
        {
            if (category.Cards.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف قسم يحتوي على كروت مسجلة.";
                return RedirectToAction(nameof(Categories));
            }
            _db.MarketCategories.Remove(category);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف القسم بنجاح.";
        }
        return RedirectToAction(nameof(Categories));
    }

    // Teams Management (Requirement 4 & 10)
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageTeams()
    {
        var teams = await _db.Teams
            .Include(t => t.Members).ThenInclude(m => m.Student)
            .Include(t => t.BoughtCards)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(teams);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateTeam()
    {
        // Enforce Requirement 10: show students and highlight if already in a team
        var assignedStudentIds = await _db.TeamMembers.Select(tm => tm.StudentId).ToListAsync();
        ViewBag.AvailableStudents = await _db.Users
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.UserName,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                IsAssigned = assignedStudentIds.Contains(u.Id)
            })
            .ToListAsync();

        return View(new TeamCreateEditViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTeam(TeamCreateEditViewModel model)
    {
        if (await _db.Teams.AnyAsync(t => t.Name == model.Name.Trim()))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم الفريق موجود مسبقًا.");
        }

        // Validate Requirement 10: same student cannot join multiple teams
        if (model.SelectedStudentIds != null && model.SelectedStudentIds.Any())
        {
            var alreadyAssigned = await _db.TeamMembers
                .Where(tm => model.SelectedStudentIds.Contains(tm.StudentId))
                .Select(tm => tm.Student.FullName)
                .ToListAsync();

            if (alreadyAssigned.Any())
            {
                ModelState.AddModelError(string.Empty, $"الطلاب التاليين مسجلين في فرق أخرى بالفعل: {string.Join("، ", alreadyAssigned)}. الطالب الواحد يمكنه الانضمام لفريق واحد فقط.");
            }
        }

        if (!ModelState.IsValid)
        {
            var assignedStudentIds = await _db.TeamMembers.Select(tm => tm.StudentId).ToListAsync();
            ViewBag.AvailableStudents = await _db.Users
                .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName,
                    ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                    IsAssigned = assignedStudentIds.Contains(u.Id)
                })
                .ToListAsync();
            return View(model);
        }

        var team = new Team
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            LogoUrl = model.LogoUrl?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        if (model.SelectedStudentIds != null)
        {
            foreach (var studentId in model.SelectedStudentIds.Distinct())
            {
                team.Members.Add(new TeamMember
                {
                    StudentId = studentId,
                    JoinedAt = DateTime.UtcNow
                });
            }
        }

        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم إنشاء فريق [{team.Name}] وتعيين أعضائه بنجاح.";
        return RedirectToAction(nameof(ManageTeams));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditTeam(int id)
    {
        var team = await _db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        var teamMemberIds = team.Members.Select(m => m.StudentId).ToList();
        var assignedInOtherTeams = await _db.TeamMembers
            .Where(tm => tm.TeamId != id)
            .Select(tm => tm.StudentId)
            .ToListAsync();

        ViewBag.AvailableStudents = await _db.Users
            .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.UserName,
                ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                IsSelectedInThisTeam = teamMemberIds.Contains(u.Id),
                IsAssignedInOtherTeam = assignedInOtherTeams.Contains(u.Id)
            })
            .ToListAsync();

        return View(new TeamCreateEditViewModel
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            LogoUrl = team.LogoUrl,
            SelectedStudentIds = teamMemberIds
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTeam(TeamCreateEditViewModel model)
    {
        if (!model.Id.HasValue) return NotFound();
        var team = await _db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == model.Id.Value);
        if (team == null) return NotFound();

        if (await _db.Teams.AnyAsync(t => t.Name == model.Name.Trim() && t.Id != team.Id))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم الفريق موجود مسبقًا.");
        }

        // Validate Requirement 10: ensure selected students aren't in other teams
        if (model.SelectedStudentIds != null && model.SelectedStudentIds.Any())
        {
            var alreadyAssignedOther = await _db.TeamMembers
                .Where(tm => tm.TeamId != team.Id && model.SelectedStudentIds.Contains(tm.StudentId))
                .Select(tm => tm.Student.FullName)
                .ToListAsync();

            if (alreadyAssignedOther.Any())
            {
                ModelState.AddModelError(string.Empty, $"الطلاب التاليين مسجلين في فرق أخرى بالفعل: {string.Join("، ", alreadyAssignedOther)}.");
            }
        }

        if (!ModelState.IsValid)
        {
            var assignedInOtherTeams = await _db.TeamMembers
                .Where(tm => tm.TeamId != team.Id)
                .Select(tm => tm.StudentId)
                .ToListAsync();

            ViewBag.AvailableStudents = await _db.Users
                .Where(u => u.IsActive && !u.IsAdmin && !u.IsTeacher)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName,
                    ClassName = u.SchoolClass != null ? u.SchoolClass.Name : u.ClassName,
                    IsSelectedInThisTeam = model.SelectedStudentIds != null && model.SelectedStudentIds.Contains(u.Id),
                    IsAssignedInOtherTeam = assignedInOtherTeams.Contains(u.Id)
                })
                .ToListAsync();
            return View(model);
        }

        team.Name = model.Name.Trim();
        team.Description = model.Description?.Trim();
        team.LogoUrl = model.LogoUrl?.Trim();

        // Update members
        _db.TeamMembers.RemoveRange(team.Members);
        if (model.SelectedStudentIds != null)
        {
            foreach (var studentId in model.SelectedStudentIds.Distinct())
            {
                team.Members.Add(new TeamMember
                {
                    TeamId = team.Id,
                    StudentId = studentId,
                    JoinedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "تم تحديث بيانات الفريق وأعضائه بنجاح.";
        return RedirectToAction(nameof(ManageTeams));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTeam(int id)
    {
        var team = await _db.Teams.Include(t => t.BoughtCards).FirstOrDefaultAsync(t => t.Id == id);
        if (team != null)
        {
            if (team.BoughtCards.Any(c => c.IsSold))
            {
                TempData["ErrorMessage"] = "لا يمكن حذف فريق يمتلك كروت نشطة. يرجى استرجاع الكروت أولاً.";
                return RedirectToAction(nameof(ManageTeams));
            }

            _db.Teams.Remove(team);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف الفريق بنجاح.";
        }
        return RedirectToAction(nameof(ManageTeams));
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _db.Users.FirstOrDefaultAsync(u => u.UserName == username);
    }
}

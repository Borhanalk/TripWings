using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWings.Data;
using TripWings.Models;

namespace TripWings.Controllers;

public class CommentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CommentController> _logger;

    public CommentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CommentController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateCommentViewModel model)
    {
        if (model == null)
        {
            return Json(new { success = false, message = "Model is null" });
        }

        if (model.Rating < 1 || model.Rating > 5)
        {
            return Json(new { success = false, message = "Rating must be between 1 and 5" });
        }

        if (string.IsNullOrWhiteSpace(model.CommentText))
        {
            return Json(new { success = false, message = "Comment text is required" });
        }

        if (model.CommentText.Length > 2000)
        {
            return Json(new { success = false, message = "Comment text cannot exceed 2000 characters" });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, message = "User not found" });
        }

        // Check if user already has a comment
        var existingComment = await _context.SiteComments
            .FirstOrDefaultAsync(c => c.UserId == user.Id);
        
        if (existingComment != null)
        {
            return Json(new { 
                success = false, 
                message = "יש לך כבר תגובה. אתה יכול להוסיף תגובה אחת בלבד / You already have a comment. Only one comment per user is allowed." 
            });
        }

        var comment = new SiteComment
        {
            UserId = user.Id,
            Rating = model.Rating,
            CommentText = model.CommentText.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsApproved = true
        };

        try
        {
            _context.SiteComments.Add(comment);
            await _context.SaveChangesAsync();

            var commentWithUser = await _context.SiteComments
                .Include(c => c.User)
                .Include(c => c.Ratings)
                .FirstOrDefaultAsync(c => c.Id == comment.Id);

            if (commentWithUser == null)
            {
                return Json(new { success = false, message = "Failed to create comment" });
            }

            return Json(new
            {
                success = true,
                comment = new
                {
                    id = commentWithUser.Id,
                    userName = $"{commentWithUser.User.FirstName} {commentWithUser.User.LastName}",
                    rating = commentWithUser.Rating,
                    commentText = commentWithUser.CommentText,
                    createdAt = commentWithUser.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    helpfulCount = commentWithUser.Ratings.Count(r => r.IsHelpful),
                    currentUserHasRated = false
                }
            });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database error creating comment");

            if (dbEx.InnerException?.Message?.Contains("Invalid object name") == true || 
                dbEx.InnerException?.Message?.Contains("does not exist") == true)
            {
                return Json(new { 
                    success = false, 
                    message = "Database tables not found. Please run migrations: dotnet ef database update" 
                });
            }
            return Json(new { 
                success = false, 
                message = "Database error: " + dbEx.InnerException?.Message ?? dbEx.Message 
            });
        }
        catch (InvalidOperationException invalidOpEx)
        {
            _logger.LogError(invalidOpEx, "Invalid operation creating comment");
            if (invalidOpEx.Message.Contains("table") || invalidOpEx.Message.Contains("does not exist"))
            {
                return Json(new { 
                    success = false, 
                    message = "Database tables not found. Please run migrations: dotnet ef database update" 
                });
            }
            return Json(new { 
                success = false, 
                message = "Invalid operation: " + invalidOpEx.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comment");
            return Json(new { 
                success = false, 
                message = "An error occurred while creating the comment: " + ex.Message 
            });
        }
    }

    [HttpPost]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RateComment([FromBody] RateCommentViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, message = "User not found" });
        }

        var comment = await _context.SiteComments
            .Include(c => c.Ratings)
            .FirstOrDefaultAsync(c => c.Id == model.CommentId);

        if (comment == null)
        {
            return Json(new { success = false, message = "Comment not found" });
        }

        var existingRating = await _context.CommentRatings
            .FirstOrDefaultAsync(r => r.SiteCommentId == model.CommentId && r.UserId == user.Id);

        if (existingRating != null)
        {

            existingRating.IsHelpful = model.IsHelpful;
            existingRating.CreatedAt = DateTime.UtcNow;
        }
        else
        {

            var rating = new CommentRating
            {
                SiteCommentId = model.CommentId,
                UserId = user.Id,
                IsHelpful = model.IsHelpful,
                CreatedAt = DateTime.UtcNow
            };
            _context.CommentRatings.Add(rating);
        }

        await _context.SaveChangesAsync();

        var helpfulCount = await _context.CommentRatings
            .CountAsync(r => r.SiteCommentId == model.CommentId && r.IsHelpful);

        return Json(new
        {
            success = true,
            helpfulCount = helpfulCount,
            hasRated = true
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> HasComment()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { hasComment = false });
        }

        var hasComment = await _context.SiteComments
            .AnyAsync(c => c.UserId == user.Id);

        return Json(new { hasComment });
    }

    [HttpGet]
    public async Task<IActionResult> GetComments(int page = 1, int pageSize = 10)
    {
        try
        {

            List<SiteComment> comments;
            int totalCount;

            try
            {
                comments = await _context.SiteComments
                    .Where(c => c.IsApproved)
                    .Include(c => c.User)
                    .Include(c => c.Ratings)
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                totalCount = await _context.SiteComments
                    .CountAsync(c => c.IsApproved);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {

                return Json(new
                {
                    comments = new List<object>(),
                    totalCount = 0,
                    page = page,
                    pageSize = pageSize,
                    totalPages = 0
                });
            }
            catch (InvalidOperationException)
            {

                return Json(new
                {
                    comments = new List<object>(),
                    totalCount = 0,
                    page = page,
                    pageSize = pageSize,
                    totalPages = 0
                });
            }

            string? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                currentUserId = user?.Id;
            }

            var commentsData = comments.Select(c => new
            {
                id = c.Id,
                userName = $"{c.User.FirstName} {c.User.LastName}",
                rating = c.Rating,
                commentText = c.CommentText,
                createdAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                helpfulCount = c.Ratings.Count(r => r.IsHelpful),
                currentUserHasRated = currentUserId != null && c.Ratings.Any(r => r.UserId == currentUserId)
            }).ToList();

            return Json(new
            {
                comments = commentsData,
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading comments");

            return Json(new
            {
                comments = new List<object>(),
                totalCount = 0,
                page = page,
                pageSize = pageSize,
                totalPages = 0,
                error = ex.Message
            });
        }
    }
}

public class CreateCommentViewModel
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [StringLength(2000)]
    public string CommentText { get; set; } = string.Empty;
}

public class RateCommentViewModel
{
    [Required]
    public int CommentId { get; set; }

    [Required]
    public bool IsHelpful { get; set; }
}

using BookStoreSample.Data;
using BookStoreSample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BookStoreSample.Pages.Admin.Reviews;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<BookReview> Reviews { get; private set; } = [];
    public int TotalCount { get; private set; }
    public double AverageRating { get; private set; }
    public int LowRatingCount { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Rating { get; set; }

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        await LoadReviewsAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var review = await dbContext.BookReviews.FirstOrDefaultAsync(item => item.Id == id);
        if (review is null)
        {
            Message = "评价不存在或已被删除。";
            return RedirectToPage(new { Keyword, Rating });
        }

        dbContext.BookReviews.Remove(review);
        await dbContext.SaveChangesAsync();
        Message = "评价已删除。";

        return RedirectToPage(new { Keyword, Rating });
    }

    private async Task LoadReviewsAsync()
    {
        var query = dbContext.BookReviews
            .AsNoTracking()
            .Include(review => review.Product)
            .Include(review => review.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            query = query.Where(review =>
                review.Content.Contains(Keyword) ||
                (review.Product != null && review.Product.Title.Contains(Keyword)) ||
                (review.User != null && review.User.DisplayName.Contains(Keyword)));
        }

        if (Rating is >= 1 and <= 5)
        {
            query = query.Where(review => review.Rating == Rating.Value);
        }

        TotalCount = await query.CountAsync();
        AverageRating = await query.AverageAsync(review => (double?)review.Rating) ?? 0;
        LowRatingCount = await query.CountAsync(review => review.Rating <= 2);

        Reviews = await query
            .OrderBy(review => review.Rating <= 2 ? 0 : 1)
            .ThenByDescending(review => review.UpdatedAt)
            .Take(80)
            .ToListAsync();
    }
}

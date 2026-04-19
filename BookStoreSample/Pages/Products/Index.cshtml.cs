using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Products;

public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<BookProduct> Products { get; private set; } = [];
    public IReadOnlyList<BookProduct> RecommendedProducts { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int CurrentPageNumber { get; private set; } = 1;
    public int PageSize { get; } = 12;
    public int TotalPages { get; private set; } = 1;
    public int StartItem => TotalCount == 0 ? 0 : (CurrentPageNumber - 1) * PageSize + 1;
    public int EndItem => Math.Min(CurrentPageNumber * PageSize, TotalCount);
    public HashSet<int> WishlistedIds { get; private set; } = [];
    public Dictionary<int, ReviewSummary> ReviewSummaries { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MinPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MaxPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "newest";

    [BindProperty(SupportsGet = true)]
    public bool ReviewedOnly { get; set; }

    public async Task OnGetAsync()
    {
        var pagedResult = await storeService.GetProductsAsync(
            Keyword, CurrentPage, PageSize, Category, MinPrice, MaxPrice, SortBy, ReviewedOnly);
        Products = pagedResult.Items;
        TotalCount = pagedResult.TotalCount;
        CurrentPageNumber = pagedResult.Page;
        TotalPages = pagedResult.TotalPages;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var wishlist = await storeService.GetWishlistAsync(userId);
            RecommendedProducts = await storeService.GetRecommendedProductsAsync(userId, 4);
            WishlistedIds = wishlist.Select(w => w.ProductId).ToHashSet();
        }

        var summaryProductIds = Products
            .Select(product => product.Id)
            .Concat(RecommendedProducts.Select(product => product.Id));
        ReviewSummaries = await storeService.GetReviewSummariesAsync(summaryProductIds);
    }

    public async Task<IActionResult> OnPostToggleWishlistAsync(int productId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (await storeService.IsInWishlistAsync(userId, productId))
        {
            await storeService.RemoveFromWishlistAsync(userId, productId);
        }
        else
        {
            await storeService.AddToWishlistAsync(userId, productId);
        }

        return RedirectToPage(new { Keyword, CurrentPage, Category, MinPrice, MaxPrice, SortBy, ReviewedOnly });
    }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Products;

public class DetailsModel(StoreService storeService) : PageModel
{
    private const string RecentProductsCookie = "bookstore_recent_products";
    private const int RecentProductsLimit = 8;

    public BookProduct? Product { get; private set; }
    public bool IsInWishlist { get; private set; }
    public IReadOnlyList<BookProduct> RelatedProducts { get; private set; } = [];
    public IReadOnlyList<BookProduct> RecentlyViewedProducts { get; private set; } = [];
    public IReadOnlyList<BookReview> Reviews { get; private set; } = [];
    public ReviewSummary ReviewSummary { get; private set; } = new(0, 0);
    public bool CanReview { get; private set; }
    public BookReview? UserReview { get; private set; }

    [BindProperty]
    public AddCartInput Input { get; set; } = new() { Quantity = 1 };

    [BindProperty]
    public ReviewInput Review { get; set; } = new() { Rating = 5 };

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public string? MessageType { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Product = await storeService.GetProductAsync(id);
        if (Product is null)
        {
            return Page();
        }

        Input.ProductId = Product.Id;
        RelatedProducts = await storeService.GetRelatedProductsAsync(Product.Id, Product.Category);
        RecentlyViewedProducts = await storeService.GetProductsByIdsAsync(GetRecentProductIds().Where(productId => productId != Product.Id).Take(4));
        await LoadReviewStateAsync(Product.Id);
        SaveRecentProductId(Product.Id);

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            IsInWishlist = await storeService.IsInWishlistAsync(userId, id);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        Product = await storeService.GetProductAsync(Input.ProductId);
        if (Product is null)
        {
            Message = "图书不存在或已下架。";
            MessageType = "error";
            return Page();
        }

        if (!Product.IsActive)
        {
            Message = "这本书已下架，暂时不能加入购物袋。";
            MessageType = "error";
            return RedirectToPage(new { id = Input.ProductId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var success = await storeService.AddToCartAsync(userId, Input.ProductId, Input.Quantity);
        Message = success ? "已加入购物袋，去结算吧。" : "加入购物车失败，库存不足或已达上限。";
        MessageType = success ? "success" : "error";
        return RedirectToPage(new { id = Input.ProductId });
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

        return RedirectToPage(new { id = productId });
    }

    public async Task<IActionResult> OnPostReviewAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await storeService.SaveReviewAsync(userId, id, Review.Rating, Review.Content);
        Message = ok ? "评价已保存。" : "评价失败，请确认你已经购买过这本书。";
        MessageType = ok ? "success" : "error";

        return RedirectToPage(new { id });
    }

    public class AddCartInput
    {
        public int ProductId { get; set; }

        [Display(Name = "数量")]
        public int Quantity { get; set; }
    }

    public class ReviewInput
    {
        [Display(Name = "评分")]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Display(Name = "评价内容")]
        [StringLength(500)]
        public string Content { get; set; } = string.Empty;
    }

    private async Task LoadReviewStateAsync(int productId)
    {
        Reviews = await storeService.GetReviewsAsync(productId);
        ReviewSummary = await storeService.GetReviewSummaryAsync(productId);

        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        CanReview = await storeService.CanReviewProductAsync(userId, productId);
        UserReview = await storeService.GetUserReviewAsync(userId, productId);
        if (UserReview is not null)
        {
            Review = new ReviewInput
            {
                Rating = UserReview.Rating,
                Content = UserReview.Content
            };
        }
    }

    private List<int> GetRecentProductIds()
    {
        var raw = Request.Cookies[RecentProductsCookie];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .Take(RecentProductsLimit)
            .ToList();
    }

    private void SaveRecentProductId(int productId)
    {
        var ids = GetRecentProductIds();
        ids.Remove(productId);
        ids.Insert(0, productId);
        ids = ids.Take(RecentProductsLimit).ToList();

        Response.Cookies.Append(RecentProductsCookie, string.Join(",", ids), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });
    }
}

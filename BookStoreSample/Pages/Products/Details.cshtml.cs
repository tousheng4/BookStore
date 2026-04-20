using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Products;

public class DetailsModel(StoreService storeService, IWebHostEnvironment environment) : PageModel
{
    private const string RecentProductsCookie = "bookstore_recent_products";
    private const int RecentProductsLimit = 8;

    public BookProduct? Product { get; private set; }
    public bool IsInWishlist { get; private set; }
    public IReadOnlyList<BookProduct> RelatedProducts { get; private set; } = [];
    public IReadOnlyList<BookProduct> RecentlyViewedProducts { get; private set; } = [];
    public IReadOnlyList<BookReview> Reviews { get; private set; } = [];
    public IReadOnlyList<BookQuestion> Questions { get; private set; } = [];
    public ReviewSummary ReviewSummary { get; private set; } = new(0, 0);
    public bool CanReview { get; private set; }
    public bool IsAdmin { get; private set; }
    public BookReview? UserReview { get; private set; }

    [BindProperty]
    public AddCartInput Input { get; set; } = new() { Quantity = 1 };

    [BindProperty]
    public ReviewInput Review { get; set; } = new() { Rating = 5 };

    [BindProperty]
    public FollowUpInput FollowUp { get; set; } = new();

    [BindProperty]
    public QuestionInput Question { get; set; } = new();

    [BindProperty]
    public AnswerInput Answer { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public string? MessageType { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Product = await storeService.GetProductAsync(id);
        IsAdmin = User.IsInRole(UserRoles.Admin);
        if (Product is null)
        {
            return Page();
        }

        Input.ProductId = Product.Id;
        RelatedProducts = await storeService.GetRelatedProductsAsync(Product.Id, Product.Category);
        RecentlyViewedProducts = await storeService.GetProductsByIdsAsync(GetRecentProductIds().Where(productId => productId != Product.Id).Take(4));
        await LoadReviewStateAsync(Product.Id);
        Questions = await storeService.GetQuestionsAsync(Product.Id);
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
        var imageUrl = await SaveReviewImageAsync(Review.Image);
        var ok = await storeService.SaveReviewAsync(userId, id, Review.Rating, Review.Content, imageUrl);
        Message = ok ? "评价已保存。" : "评价失败，请确认你已经购买过这本书。";
        MessageType = ok ? "success" : "error";

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostFollowUpAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await storeService.SaveReviewFollowUpAsync(userId, id, FollowUp.Content);
        Message = ok ? "追评已发布。" : "追评失败，请先发布评价并填写追评内容。";
        MessageType = ok ? "success" : "error";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostQuestionAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await storeService.AskQuestionAsync(userId, id, Question.Content);
        Message = ok ? "问题已提交，等待回答。" : "问题提交失败，请确认内容不为空。";
        MessageType = ok ? "success" : "error";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAnswerQuestionAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole(UserRoles.Admin))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await storeService.AnswerQuestionAsync(Answer.QuestionId, userId, Answer.Content);
        Message = ok ? "回答已发布。" : "回答失败，请填写回答内容。";
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

        [Display(Name = "晒图")]
        public IFormFile? Image { get; set; }
    }

    public class FollowUpInput
    {
        [Display(Name = "追加评价")]
        [StringLength(500)]
        public string Content { get; set; } = string.Empty;
    }

    public class QuestionInput
    {
        [Display(Name = "你的问题")]
        [StringLength(500)]
        public string Content { get; set; } = string.Empty;
    }

    public class AnswerInput
    {
        public int QuestionId { get; set; }

        [Display(Name = "回答内容")]
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

    private async Task<string> SaveReviewImageAsync(IFormFile? image)
    {
        if (image is null || image.Length == 0)
        {
            return string.Empty;
        }

        if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
        {
            extension = ".jpg";
        }

        var relativeDirectory = Path.Combine("uploads", "reviews");
        var absoluteDirectory = Path.Combine(environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await image.CopyToAsync(stream);

        return "/" + relativeDirectory.Replace('\\', '/') + "/" + fileName;
    }
}

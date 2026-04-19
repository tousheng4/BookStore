using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;

namespace BookStoreSample.Pages.Account;

[Authorize]
public class IndexModel(
    StoreService storeService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    private const string RecentProductsCookie = "bookstore_recent_products";

    public ApplicationUser? UserInfo { get; private set; }
    public IReadOnlyList<ShippingAddress> Addresses { get; private set; } = [];
    public IReadOnlyList<WishlistItem> WishlistItems { get; private set; } = [];
    public IReadOnlyList<UserCoupon> UserCoupons { get; private set; } = [];
    public IReadOnlyList<Order> RecentOrders { get; private set; } = [];
    public IReadOnlyList<BookProduct> RecentlyViewedProducts { get; private set; } = [];
    public AccountInsightResult Insights { get; private set; } = new([], [], [], [], 0m, 0, 0, "暂无");
    public bool IsAdmin { get; private set; }

    public int AddressCount => Addresses.Count;
    public ShippingAddress? DefaultAddress => Addresses.FirstOrDefault(a => a.IsDefault) ?? Addresses.FirstOrDefault();
    public int WishlistCount => WishlistItems.Count;
    public int CouponCount => UserCoupons.Count;
    public int AvailableCouponCount => UserCoupons.Count(item =>
        item.UsedAt is null &&
        item.Coupon is not null &&
        item.Coupon.IsActive &&
        item.Coupon.StartsAt <= DateTime.UtcNow &&
        item.Coupon.EndsAt >= DateTime.UtcNow);
    public int UnreadNotificationCount { get; private set; }
    public int OrderCount { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        UserInfo = await userManager.FindByIdAsync(userId);
        IsAdmin = await userManager.IsInRoleAsync(UserInfo!, UserRoles.Admin);
        Addresses = await storeService.GetAddressesAsync(userId);
        WishlistItems = await storeService.GetWishlistAsync(userId);
        UserCoupons = await storeService.GetUserCouponsAsync(userId);
        UnreadNotificationCount = await storeService.GetUnreadNotificationCountAsync(userId);

        var orders = await storeService.GetOrdersAsync(userId, isAdmin: false);
        OrderCount = orders.Count;
        RecentOrders = orders.Take(5).ToList();
        Insights = await storeService.GetAccountInsightsAsync(userId);
        RecentlyViewedProducts = await storeService.GetProductsByIdsAsync(GetRecentProductIds().Take(4));
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
            .ToList();
    }
}

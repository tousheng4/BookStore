using BookStoreSample.Data;
using BookStoreSample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BookStoreSample.Pages.Admin;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    private const int LowStockThreshold = 5;

    public int ProductCount { get; private set; }
    public int OrderCount { get; private set; }
    public int PendingShipmentCount { get; private set; }
    public int LowStockCount { get; private set; }
    public int ReviewCount { get; private set; }
    public int LowRatingReviewCount { get; private set; }
    public int CouponClaimCount { get; private set; }
    public int CouponUsedCount { get; private set; }
    public decimal CouponDiscountTotal { get; private set; }
    public decimal PaidRevenue { get; private set; }
    public decimal CompletedRevenue { get; private set; }
    public IReadOnlyList<BookProduct> LowStockProducts { get; private set; } = [];
    public IReadOnlyList<BookProduct> BestSellers { get; private set; } = [];
    public IReadOnlyList<Order> RecentOrders { get; private set; } = [];
    public IReadOnlyList<BookReview> LowRatingReviews { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> DailyOrderChart { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> DailySalesChart { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> CategorySalesChart { get; private set; } = [];
    public IReadOnlyList<DashboardChartItem> BestsellerChart { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ProductCount = await dbContext.Products.CountAsync();
        OrderCount = await dbContext.Orders.CountAsync();
        PendingShipmentCount = await dbContext.Orders.CountAsync(order => order.Status == OrderStatuses.Paid);
        LowStockCount = await dbContext.Products.CountAsync(product => product.IsActive && product.Stock <= LowStockThreshold);
        ReviewCount = await dbContext.BookReviews.CountAsync();
        LowRatingReviewCount = await dbContext.BookReviews.CountAsync(review => review.Rating <= 2);
        CouponClaimCount = await dbContext.UserCoupons.CountAsync();
        CouponUsedCount = await dbContext.UserCoupons.CountAsync(coupon => coupon.UsedAt != null);

        var couponDiscounts = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.DiscountAmount > 0)
            .Select(order => order.DiscountAmount)
            .ToListAsync();
        CouponDiscountTotal = couponDiscounts.Sum();

        var paidAmounts = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status != OrderStatuses.Cancelled && order.Status != OrderStatuses.NewOrder)
            .Select(order => order.TotalAmount)
            .ToListAsync();
        PaidRevenue = paidAmounts.Sum();

        var completedAmounts = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatuses.Completed)
            .Select(order => order.TotalAmount)
            .ToListAsync();
        CompletedRevenue = completedAmounts.Sum();

        LowStockProducts = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.Stock <= LowStockThreshold)
            .OrderBy(product => product.Stock)
            .ThenBy(product => product.Title)
            .Take(8)
            .ToListAsync();

        BestSellers = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderByDescending(product => product.SalesCount)
            .ThenBy(product => product.Stock)
            .Take(6)
            .ToListAsync();

        RecentOrders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .Take(6)
            .ToListAsync();

        LowRatingReviews = await dbContext.BookReviews
            .AsNoTracking()
            .Include(review => review.Product)
            .Include(review => review.User)
            .Where(review => review.Rating <= 2)
            .OrderByDescending(review => review.UpdatedAt)
            .Take(6)
            .ToListAsync();

        await LoadChartsAsync();
    }

    private async Task LoadChartsAsync()
    {
        var chartStart = DateTime.UtcNow.Date.AddDays(-6);
        var chartOrders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
                .ThenInclude(item => item.Product)
            .Where(order => order.CreatedAt >= chartStart)
            .ToListAsync();

        var paidStatuses = new[] { OrderStatuses.Paid, OrderStatuses.Shipped, OrderStatuses.Received, OrderStatuses.Completed };
        var paidChartOrders = chartOrders
            .Where(order => paidStatuses.Contains(order.Status))
            .ToList();

        var dailyOrders = new List<DashboardChartItem>();
        var dailySales = new List<DashboardChartItem>();
        for (var i = 0; i < 7; i++)
        {
            var day = chartStart.AddDays(i);
            var label = day.ToLocalTime().ToString("MM-dd");
            var orderCount = chartOrders.Count(order => order.CreatedAt.Date == day);
            var salesAmount = paidChartOrders
                .Where(order => order.CreatedAt.Date == day)
                .Sum(order => order.TotalAmount);

            dailyOrders.Add(new DashboardChartItem(label, orderCount, orderCount.ToString()));
            dailySales.Add(new DashboardChartItem(label, salesAmount, $"￥{salesAmount:0.00}"));
        }

        DailyOrderChart = WithPercent(dailyOrders);
        DailySalesChart = WithPercent(dailySales);

        var categorySales = paidChartOrders
            .SelectMany(order => order.Items)
            .GroupBy(item => item.Product?.Category ?? "未分类")
            .Select(group =>
            {
                var amount = group.Sum(item => item.LineTotal);
                return new DashboardChartItem(group.Key, amount, $"￥{amount:0.00}");
            })
            .OrderByDescending(item => item.Value)
            .Take(6)
            .ToList();
        CategorySalesChart = WithPercent(categorySales);

        var paidOrderItems = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Where(item => item.Order != null && paidStatuses.Contains(item.Order.Status))
            .ToListAsync();
        var bestsellerItems = paidOrderItems
            .GroupBy(item => new { item.ProductId, item.Title })
            .Select(group =>
            {
                var salesCount = group.Sum(item => item.Quantity);
                return new DashboardChartItem(group.Key.Title, salesCount, salesCount.ToString());
            })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Label)
            .Take(6)
            .ToList();
        BestsellerChart = WithPercent(bestsellerItems);
    }

    private static IReadOnlyList<DashboardChartItem> WithPercent(IReadOnlyList<DashboardChartItem> items)
    {
        var max = items.Count == 0 ? 0 : items.Max(item => item.Value);
        return items
            .Select(item => item with
            {
                Percent = max <= 0 ? 0 : Math.Max(4, (int)Math.Round((double)(item.Value / max) * 100))
            })
            .ToList();
    }
}

public sealed record DashboardChartItem(string Label, decimal Value, string DisplayValue)
{
    public int Percent { get; init; }
}

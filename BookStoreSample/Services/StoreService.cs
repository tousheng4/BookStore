using BookStoreSample.Data;
using BookStoreSample.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreSample.Services;

public class StoreService(ApplicationDbContext dbContext)
{
    public async Task<List<BookProduct>> GetProductsAsync()
    {
        return await dbContext.Products.AsNoTracking()
            .OrderByDescending(product => product.CreatedAt)
            .ToListAsync();
    }

    public async Task<PagedProductsResult> GetAdminProductsAsync(int page = 1, int pageSize = 12)
    {
        var query = dbContext.Products.AsNoTracking()
            .OrderByDescending(product => product.CreatedAt);

        var totalCount = await query.CountAsync();
        var safePageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));
        var currentPage = Math.Min(Math.Max(1, page), totalPages);

        var items = await query
            .Skip((currentPage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return new PagedProductsResult(items, totalCount, currentPage, safePageSize);
    }

    public async Task<PagedProductsResult> GetProductsAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 12,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string sortBy = "newest",
        bool reviewedOnly = false)
    {
        var query = dbContext.Products.AsNoTracking()
            .Where(product => product.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(product =>
                product.Title.Contains(keyword) ||
                product.Author.Contains(keyword) ||
                product.Category.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(product => product.Category == category);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(product => product.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(product => product.Price <= maxPrice.Value);
        }

        if (reviewedOnly)
        {
            query = query.Where(product => product.Reviews.Any());
        }

        if (sortBy is "price_asc" or "price_desc")
        {
            var filteredItems = await query.ToListAsync();
            var orderedItems = sortBy == "price_asc"
                ? filteredItems.OrderBy(product => product.Price).ToList()
                : filteredItems.OrderByDescending(product => product.Price).ToList();

            var safePricePageSize = Math.Max(1, pageSize);
            var priceTotalPages = Math.Max(1, (int)Math.Ceiling(orderedItems.Count / (double)safePricePageSize));
            var priceCurrentPage = Math.Min(Math.Max(1, page), priceTotalPages);

            return new PagedProductsResult(
                orderedItems
                    .Skip((priceCurrentPage - 1) * safePricePageSize)
                    .Take(safePricePageSize)
                    .ToList(),
                orderedItems.Count,
                priceCurrentPage,
                safePricePageSize);
        }

        query = sortBy switch
        {
            "sales" => query.OrderByDescending(product => product.SalesCount),
            "rating" => query
                .OrderByDescending(product => product.Reviews.Any() ? product.Reviews.Average(review => review.Rating) : 0)
                .ThenByDescending(product => product.Reviews.Count),
            "stock" => query.OrderByDescending(product => product.Stock),
            _ => query.OrderByDescending(product => product.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var safePageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));
        var currentPage = Math.Min(Math.Max(1, page), totalPages);

        var items = await query
            .Skip((currentPage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return new PagedProductsResult(items, totalCount, currentPage, safePageSize);
    }

    public async Task<List<BookProduct>> GetBestSellersAsync(int take = 8)
    {
        return await dbContext.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.SalesCount)
            .ThenByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<BookProduct>> GetNewArrivalsAsync(int take = 8)
    {
        return await dbContext.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public Task<BookProduct?> GetProductAsync(int id) =>
        dbContext.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Id == id);

    public async Task<List<BookProduct>> GetProductsByIdsAsync(IEnumerable<int> ids)
    {
        var orderedIds = ids.Distinct().ToList();
        if (orderedIds.Count == 0)
        {
            return [];
        }

        var products = await dbContext.Products.AsNoTracking()
            .Where(product => orderedIds.Contains(product.Id) && product.IsActive)
            .ToListAsync();

        return products
            .OrderBy(product => orderedIds.IndexOf(product.Id))
            .ToList();
    }

    public async Task<List<BookProduct>> GetRelatedProductsAsync(int productId, string category, int take = 4)
    {
        return await dbContext.Products.AsNoTracking()
            .Where(product => product.Id != productId && product.Category == category && product.IsActive)
            .OrderByDescending(product => product.SalesCount)
            .ThenByDescending(product => product.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<BookProduct>> GetRecommendedProductsAsync(string userId, int take = 6)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var safeTake = Math.Max(1, take);
        var recommendationStatuses = new[]
        {
            OrderStatuses.Paid,
            OrderStatuses.Shipped,
            OrderStatuses.Received,
            OrderStatuses.Completed
        };
        var userOrderItems = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.Product)
            .Where(item =>
                item.Order != null &&
                item.Product != null &&
                item.Order.UserId == userId &&
                recommendationStatuses.Contains(item.Order.Status))
            .ToListAsync();

        if (userOrderItems.Count == 0)
        {
            return await GetNewArrivalsAsync(safeTake);
        }

        var purchasedProductIds = userOrderItems
            .Select(item => item.ProductId)
            .ToHashSet();
        var categoryWeights = userOrderItems
            .Where(item => item.Product is not null)
            .GroupBy(item => item.Product!.Category)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var authorWeights = userOrderItems
            .Where(item => item.Product is not null)
            .GroupBy(item => item.Product!.Author)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var candidates = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && !purchasedProductIds.Contains(product.Id))
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return [];
        }

        var paidOrderItems = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Where(item => item.Order != null && recommendationStatuses.Contains(item.Order.Status))
            .ToListAsync();
        var globalSales = paidOrderItems
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        return candidates
            .Select(product =>
            {
                categoryWeights.TryGetValue(product.Category, out var categoryScore);
                authorWeights.TryGetValue(product.Author, out var authorScore);
                globalSales.TryGetValue(product.Id, out var salesScore);
                var score = categoryScore * 10 + authorScore * 6 + salesScore * 2;
                return new
                {
                    Product = product,
                    Score = score
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Product.CreatedAt)
            .Take(safeTake)
            .Select(item => item.Product)
            .ToList();
    }

    public async Task<BookAssistantResponse> AskBookAssistantAsync(string? question, string? userId = null, int take = 3)
    {
        var queryText = (question ?? string.Empty).Trim();
        var safeTake = Math.Max(1, take);
        var products = await dbContext.Products
            .AsNoTracking()
            .Include(product => product.Reviews)
            .Where(product => product.IsActive)
            .ToListAsync();

        if (products.Count == 0)
        {
            return new BookAssistantResponse(
                "书店里暂时还没有可推荐的图书。",
                []);
        }

        var globalSalesItems = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Where(item =>
                item.Order != null &&
                (item.Order.Status == OrderStatuses.Paid ||
                 item.Order.Status == OrderStatuses.Shipped ||
                 item.Order.Status == OrderStatuses.Received ||
                 item.Order.Status == OrderStatuses.Completed))
            .ToListAsync();
        var globalSales = globalSalesItems
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        var preferredCategories = await GetUserPreferredCategoriesAsync(userId);
        var intent = BuildAssistantIntent(queryText);
        var scoredProducts = products
            .Select(product =>
            {
                globalSales.TryGetValue(product.Id, out var salesCount);
                var reviewCount = product.Reviews.Count;
                var averageRating = reviewCount == 0 ? 0 : product.Reviews.Average(review => review.Rating);
                var score = ScoreAssistantProduct(product, queryText, intent, preferredCategories, salesCount, averageRating, reviewCount);
                return new
                {
                    Product = product,
                    Score = score,
                    SalesCount = salesCount,
                    AverageRating = averageRating,
                    ReviewCount = reviewCount
                };
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.AverageRating)
            .ThenByDescending(item => item.SalesCount)
            .ThenByDescending(item => item.Product.CreatedAt)
            .Take(safeTake)
            .ToList();

        if (scoredProducts.Count == 0)
        {
            scoredProducts = products
                .Select(product =>
                {
                    globalSales.TryGetValue(product.Id, out var salesCount);
                    var reviewCount = product.Reviews.Count;
                    var averageRating = reviewCount == 0 ? 0 : product.Reviews.Average(review => review.Rating);
                    return new
                    {
                        Product = product,
                        Score = (decimal)(salesCount * 2 + averageRating + (product.Stock > 0 ? 1 : 0)),
                        SalesCount = salesCount,
                        AverageRating = averageRating,
                        ReviewCount = reviewCount
                    };
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Product.CreatedAt)
                .Take(safeTake)
                .ToList();
        }

        var suggestions = scoredProducts
            .Select(item => new BookAssistantSuggestion(
                item.Product.Id,
                item.Product.Title,
                item.Product.Author,
                item.Product.Category,
                item.Product.CoverUrl,
                item.Product.Price,
                BuildAssistantReason(item.Product, intent, item.AverageRating, item.ReviewCount, item.SalesCount)))
            .ToList();

        var message = string.IsNullOrWhiteSpace(queryText)
            ? "可以告诉我你想读的主题、预算、难度或用途。我先按当前书店里比较值得看的书给你几本。"
            : $"我按“{queryText}”帮你挑了 {suggestions.Count} 本，可以先看这几本。";

        return new BookAssistantResponse(message, suggestions);
    }

    public async Task<AccountInsightResult> GetAccountInsightsAsync(string userId)
    {
        var insightStatuses = new[]
        {
            OrderStatuses.Paid,
            OrderStatuses.Shipped,
            OrderStatuses.Received,
            OrderStatuses.Completed
        };
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
                .ThenInclude(item => item.Product)
            .Where(order =>
                order.UserId == userId &&
                insightStatuses.Contains(order.Status))
            .ToListAsync();

        var monthlyOrders = new List<AccountChartItem>();
        var monthlySpend = new List<AccountChartItem>();
        for (var i = 0; i < 6; i++)
        {
            var month = monthStart.AddMonths(i);
            var nextMonth = month.AddMonths(1);
            var monthOrders = orders
                .Where(order => order.CreatedAt >= month && order.CreatedAt < nextMonth)
                .ToList();
            var orderCount = monthOrders.Count;
            var spendAmount = monthOrders.Sum(order => order.TotalAmount);
            var label = month.ToLocalTime().ToString("MM月");

            monthlyOrders.Add(new AccountChartItem(label, orderCount, orderCount.ToString()));
            monthlySpend.Add(new AccountChartItem(label, spendAmount, $"￥{spendAmount:0.00}"));
        }

        var allItems = orders
            .SelectMany(order => order.Items)
            .Where(item => item.Product is not null)
            .ToList();
        var categoryItems = allItems
            .GroupBy(item => item.Product!.Category)
            .Select(group =>
            {
                var quantity = group.Sum(item => item.Quantity);
                return new AccountChartItem(group.Key, quantity, $"{quantity} 本");
            })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Label)
            .Take(6)
            .ToList();

        var categoryChart = WithAccountPercents(categoryItems);
        var radarItems = BuildRadarItems(categoryChart.Take(5).ToList());
        var totalSpent = orders.Sum(order => order.TotalAmount);
        var purchasedBookCount = allItems.Sum(item => item.Quantity);
        var favoriteCategory = categoryChart.FirstOrDefault()?.Label ?? "暂无";

        return new AccountInsightResult(
            WithAccountPercents(monthlyOrders),
            WithAccountPercents(monthlySpend),
            categoryChart,
            radarItems,
            totalSpent,
            orders.Count,
            purchasedBookCount,
            favoriteCategory);
    }

    public async Task<MemberCenterResult> GetMemberCenterAsync(string userId)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return new MemberCenterResult(
                "普通会员",
                0,
                0,
                null,
                300,
                0,
                "银卡会员",
                GetMemberBenefits("普通会员"),
                []);
        }

        var memberOrders = await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.UserId == userId &&
                (order.Status == OrderStatuses.Paid ||
                 order.Status == OrderStatuses.Shipped ||
                 order.Status == OrderStatuses.Received ||
                 order.Status == OrderStatuses.Completed))
            .OrderByDescending(order => order.CreatedAt)
            .Take(5)
            .ToListAsync();

        var normalizedLevel = BuildMemberLevel(user.MemberGrowth);
        var (nextLevel, nextGrowth) = GetNextMemberLevel(user.MemberGrowth);
        var currentLevelStart = GetMemberLevelStart(user.MemberGrowth);
        var progress = nextGrowth is null
            ? 100
            : (int)Math.Clamp(
                (user.MemberGrowth - currentLevelStart) * 100m / Math.Max(1, nextGrowth.Value - currentLevelStart),
                0m,
                100m);

        return new MemberCenterResult(
            normalizedLevel,
            user.MemberPoints,
            user.MemberGrowth,
            user.MembershipStartedAt,
            nextGrowth,
            progress,
            nextLevel,
            GetMemberBenefits(normalizedLevel),
            memberOrders.Select(order => new MemberOrderReward(
                order.Id,
                order.TotalAmount,
                (int)Math.Floor(order.TotalAmount),
                order.CreatedAt)).ToList());
    }

    public async Task<List<BookProduct>> GetFeaturedProductsAsync(int take = 4) =>
        await dbContext.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .OrderByDescending(product => product.Stock)
            .Take(take)
            .ToListAsync();

    public async Task<List<CategoryHighlight>> GetCategoryHighlightsAsync()
    {
        return await dbContext.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .GroupBy(product => product.Category)
            .Select(group => new CategoryHighlight
            {
                Category = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ToListAsync();
    }

    public async Task AddProductAsync(BookProduct product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        product.Description ??= string.Empty;
        product.CoverUrl ??= string.Empty;
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> UpdateProductAsync(BookProduct input, string changedBy = "system")
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(item => item.Id == input.Id);
        if (product is null)
        {
            return false;
        }

        var oldStock = product.Stock;
        product.Title = input.Title;
        product.Author = input.Author;
        product.Publisher = input.Publisher;
        product.Category = input.Category;
        product.Price = input.Price;
        product.Stock = input.Stock;
        product.Description = input.Description ?? string.Empty;
        product.CoverUrl = input.CoverUrl ?? string.Empty;
        product.IsActive = input.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        if (oldStock != product.Stock)
        {
            AddInventoryChangeLog(
                product,
                oldStock,
                product.Stock,
                "AdminAdjustment",
                changedBy,
                null,
                "后台修改库存");
        }
        if (product.IsActive && product.Stock <= 5)
        {
            await AddAdminNotificationAsync(
                "低库存提醒",
                $"《{product.Title}》当前库存为 {product.Stock}，请及时补货。",
                "LowStock",
                $"/Admin/Products/Edit/{product.Id}");
        }
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetProductActiveAsync(int id, bool isActive)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(item => item.Id == id);
        if (product is null)
        {
            return false;
        }

        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(item => item.Id == id);
        if (product is null)
        {
            return false;
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<CartItem>> GetCartAsync(string userId)
    {
        return await dbContext.CartItems
            .Include(item => item.Product)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.AddedAt)
            .ToListAsync();
    }

    public async Task<OrderBoosterResult> GetOrderBoosterAsync(string userId, decimal cartTotal, IEnumerable<int> cartProductIds, int take = 4)
    {
        var now = DateTime.UtcNow;
        var productIds = cartProductIds.ToHashSet();
        var safeTake = Math.Max(1, take);
        var userCouponIds = await dbContext.UserCoupons
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.UsedAt == null)
            .Select(item => item.CouponId)
            .ToListAsync();
        var claimedCouponIds = userCouponIds.ToHashSet();

        var coupons = await dbContext.Coupons
            .AsNoTracking()
            .Where(coupon =>
                coupon.IsActive &&
                coupon.StartsAt <= now &&
                coupon.EndsAt >= now &&
                coupon.MinimumAmount > cartTotal)
            .ToListAsync();

        var targetCoupon = coupons
            .Select(coupon => new
            {
                Coupon = coupon,
                IsClaimed = claimedCouponIds.Contains(coupon.Id),
                Gap = coupon.MinimumAmount - cartTotal
            })
            .OrderByDescending(item => item.IsClaimed)
            .ThenBy(item => item.Gap)
            .ThenByDescending(item => item.Coupon.DiscountAmount)
            .FirstOrDefault();

        if (targetCoupon is null)
        {
            return new OrderBoosterResult(null, 0m, false, []);
        }

        var gap = targetCoupon.Gap;
        var candidates = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.Stock > 0 && !productIds.Contains(product.Id))
            .ToListAsync();

        var suggestions = candidates
            .Select(product => new OrderBoosterSuggestion(
                product,
                Math.Max(0m, gap - product.Price),
                product.Price >= gap))
            .OrderByDescending(item => item.ReachesTarget)
            .ThenBy(item => item.RemainingGap)
            .ThenBy(item => Math.Abs(item.Product.Price - gap))
            .ThenByDescending(item => item.Product.SalesCount)
            .Take(safeTake)
            .ToList();

        return new OrderBoosterResult(targetCoupon.Coupon, gap, targetCoupon.IsClaimed, suggestions);
    }

    public async Task<bool> AddToCartAsync(string userId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            return false;
        }

        var product = await dbContext.Products.FirstOrDefaultAsync(item => item.Id == productId);
        if (product is null || !product.IsActive)
        {
            return false;
        }

        var existing = await dbContext.CartItems.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductId == productId);
        var nextQuantity = (existing?.Quantity ?? 0) + quantity;
        if (nextQuantity > product.Stock)
        {
            return false;
        }

        if (existing is null)
        {
            dbContext.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductId = productId,
                Quantity = quantity,
                AddedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Quantity = nextQuantity;
        }

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task UpdateCartItemAsync(string userId, int productId, int quantity)
    {
        var item = await dbContext.CartItems
            .Include(cartItem => cartItem.Product)
            .FirstOrDefaultAsync(cartItem => cartItem.UserId == userId && cartItem.ProductId == productId);

        if (item is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            dbContext.CartItems.Remove(item);
            await dbContext.SaveChangesAsync();
            return;
        }

        if (item.Product is not null && quantity <= item.Product.Stock)
        {
            item.Quantity = quantity;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task RemoveCartItemAsync(string userId, int productId)
    {
        var item = await dbContext.CartItems.FirstOrDefaultAsync(cartItem => cartItem.UserId == userId && cartItem.ProductId == productId);
        if (item is null)
        {
            return;
        }

        dbContext.CartItems.Remove(item);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<WishlistItem>> GetWishlistAsync(string userId)
    {
        return await dbContext.WishlistItems
            .Include(w => w.Product)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync();
    }

    public async Task<bool> IsInWishlistAsync(string userId, int productId)
    {
        return await dbContext.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
    }

    public async Task<bool> AddToWishlistAsync(string userId, int productId)
    {
        var exists = await dbContext.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.ProductId == productId);
        if (exists) return false;

        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null || !product.IsActive) return false;

        dbContext.WishlistItems.Add(new WishlistItem
        {
            UserId = userId,
            ProductId = productId,
            AddedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task RemoveFromWishlistAsync(string userId, int productId)
    {
        var item = await dbContext.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);
        if (item is null) return;

        dbContext.WishlistItems.Remove(item);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<BookReview>> GetReviewsAsync(int productId)
    {
        return await dbContext.BookReviews
            .AsNoTracking()
            .Include(review => review.User)
            .Where(review => review.ProductId == productId)
            .OrderByDescending(review => review.UpdatedAt)
            .ToListAsync();
    }

    public async Task<BookReview?> GetUserReviewAsync(string userId, int productId)
    {
        return await dbContext.BookReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(review => review.UserId == userId && review.ProductId == productId);
    }

    public async Task<ReviewSummary> GetReviewSummaryAsync(int productId)
    {
        var stats = await dbContext.BookReviews
            .AsNoTracking()
            .Where(review => review.ProductId == productId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Average = group.Average(review => review.Rating)
            })
            .FirstOrDefaultAsync();

        return stats is null
            ? new ReviewSummary(0, 0)
            : new ReviewSummary(stats.Count, stats.Average);
    }

    public async Task<Dictionary<int, ReviewSummary>> GetReviewSummariesAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var stats = await dbContext.BookReviews
            .AsNoTracking()
            .Where(review => ids.Contains(review.ProductId))
            .GroupBy(review => review.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Count = group.Count(),
                Average = group.Average(review => review.Rating)
            })
            .ToListAsync();

        return stats.ToDictionary(
            item => item.ProductId,
            item => new ReviewSummary(item.Count, item.Average));
    }

    public async Task<List<BookQuestion>> GetQuestionsAsync(int productId)
    {
        return await dbContext.BookQuestions
            .AsNoTracking()
            .Include(question => question.User)
            .Where(question => question.ProductId == productId)
            .OrderByDescending(question => question.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> AskQuestionAsync(string userId, int productId, string question)
    {
        var productExists = await dbContext.Products
            .AnyAsync(product => product.Id == productId && product.IsActive);
        if (!productExists)
        {
            return false;
        }

        var trimmedQuestion = (question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuestion))
        {
            return false;
        }

        dbContext.BookQuestions.Add(new BookQuestion
        {
            UserId = userId,
            ProductId = productId,
            Question = trimmedQuestion.Length > 500 ? trimmedQuestion[..500] : trimmedQuestion,
            CreatedAt = DateTime.UtcNow
        });

        await AddAdminNotificationAsync(
            "新的读者提问",
            "有读者在图书详情页提交了新问题，请及时查看并回答。",
            "BookQuestion",
            $"/Products/Details/{productId}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AnswerQuestionAsync(int questionId, string answeredBy, string answer)
    {
        var question = await dbContext.BookQuestions
            .FirstOrDefaultAsync(item => item.Id == questionId);
        if (question is null)
        {
            return false;
        }

        var trimmedAnswer = (answer ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedAnswer))
        {
            return false;
        }

        question.Answer = trimmedAnswer.Length > 500 ? trimmedAnswer[..500] : trimmedAnswer;
        question.AnsweredBy = answeredBy;
        question.AnsweredAt = DateTime.UtcNow;
        AddUserNotification(
            question.UserId,
            "你的提问已收到回答",
            "你在图书详情页提交的问题已经有人回答，快去看看吧。",
            "BookQuestionAnswered",
            $"/Products/Details/{question.ProductId}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CanReviewProductAsync(string userId, int productId)
    {
        var reviewableStatuses = new[] { OrderStatuses.Paid, OrderStatuses.Shipped, OrderStatuses.Received, OrderStatuses.Completed };
        return await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .AnyAsync(item =>
                item.ProductId == productId &&
                item.Order != null &&
                item.Order.UserId == userId &&
                reviewableStatuses.Contains(item.Order.Status));
    }

    public async Task<bool> SaveReviewAsync(string userId, int productId, int rating, string content, string imageUrl = "")
    {
        if (rating is < 1 or > 5)
        {
            return false;
        }

        if (!await CanReviewProductAsync(userId, productId))
        {
            return false;
        }

        var trimmedContent = (content ?? string.Empty).Trim();
        if (trimmedContent.Length > 500)
        {
            trimmedContent = trimmedContent[..500];
        }

        var existing = await dbContext.BookReviews
            .FirstOrDefaultAsync(review => review.UserId == userId && review.ProductId == productId);

        if (existing is null)
        {
            dbContext.BookReviews.Add(new BookReview
            {
                UserId = userId,
                ProductId = productId,
                Rating = rating,
                Content = trimmedContent,
                ImageUrl = imageUrl ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Rating = rating;
            existing.Content = trimmedContent;
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                existing.ImageUrl = imageUrl;
            }
            existing.UpdatedAt = DateTime.UtcNow;
        }

        AddUserNotification(
            userId,
            "评价已发布",
            "感谢你的反馈，评价已展示在图书详情页。",
            "Review",
            $"/Products/Details/{productId}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SaveReviewFollowUpAsync(string userId, int productId, string content)
    {
        var review = await dbContext.BookReviews
            .FirstOrDefaultAsync(item => item.UserId == userId && item.ProductId == productId);
        if (review is null)
        {
            return false;
        }

        var trimmedContent = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedContent))
        {
            return false;
        }

        review.FollowUpContent = trimmedContent.Length > 500 ? trimmedContent[..500] : trimmedContent;
        review.FollowUpAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        AddUserNotification(
            userId,
            "追评已发布",
            "你的追加评价已展示在图书详情页。",
            "ReviewFollowUp",
            $"/Products/Details/{productId}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<ShippingAddress>> GetAddressesAsync(string userId)
    {
        return await dbContext.ShippingAddresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<ShippingAddress?> GetDefaultAddressAsync(string userId)
    {
        return await dbContext.ShippingAddresses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);
    }

    public async Task<ShippingAddress?> GetAddressAsync(int id, string userId)
    {
        return await dbContext.ShippingAddresses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }

    public async Task<bool> SaveAddressAsync(string userId, ShippingAddress input)
    {
        var existing = await dbContext.ShippingAddresses.FirstOrDefaultAsync(a => a.Id == input.Id && a.UserId == userId);

        if (existing is not null)
        {
            existing.ReceiverName = input.ReceiverName;
            existing.ReceiverPhone = input.ReceiverPhone;
            existing.Province = input.Province;
            existing.City = input.City;
            existing.District = input.District;
            existing.StreetAddress = input.StreetAddress;
            existing.IsDefault = input.IsDefault;
        }
        else
        {
            var count = await dbContext.ShippingAddresses.CountAsync(a => a.UserId == userId);
            if (count >= 10) return false;

            input.UserId = userId;
            input.CreatedAt = DateTime.UtcNow;
            if (input.IsDefault || count == 0)
            {
                var currentDefaults = dbContext.ShippingAddresses.Where(a => a.UserId == userId && a.IsDefault);
                foreach (var addr in currentDefaults) addr.IsDefault = false;
            }
            dbContext.ShippingAddresses.Add(input);
        }

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAddressAsync(int id, string userId)
    {
        var address = await dbContext.ShippingAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (address is null) return false;

        var wasDefault = address.IsDefault;
        dbContext.ShippingAddresses.Remove(address);
        await dbContext.SaveChangesAsync();

        if (wasDefault)
        {
            var next = await dbContext.ShippingAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();
            if (next is not null)
            {
                next.IsDefault = true;
                await dbContext.SaveChangesAsync();
            }
        }

        return true;
    }

    public async Task SetDefaultAddressAsync(int id, string userId)
    {
        var addresses = await dbContext.ShippingAddresses.Where(a => a.UserId == userId).ToListAsync();
        foreach (var addr in addresses) addr.IsDefault = addr.Id == id;
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<UserCoupon>> GetUsableCouponsAsync(string userId, decimal orderAmount)
    {
        var now = DateTime.UtcNow;
        var userCoupons = await dbContext.UserCoupons
            .AsNoTracking()
            .Include(item => item.Coupon)
            .Where(item =>
                item.UserId == userId &&
                item.UsedAt == null &&
                item.Coupon != null &&
                item.Coupon.IsActive &&
                item.Coupon.StartsAt <= now &&
                item.Coupon.EndsAt >= now &&
                item.Coupon.MinimumAmount <= orderAmount)
            .ToListAsync();

        return userCoupons
            .OrderByDescending(item => item.Coupon!.DiscountAmount)
            .ThenBy(item => item.Coupon!.MinimumAmount)
            .ToList();
    }

    public async Task<List<CouponClaimItem>> GetCouponClaimItemsAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var claimedCouponIds = await dbContext.UserCoupons
            .Where(item => item.UserId == userId)
            .Select(item => item.CouponId)
            .ToListAsync();

        var coupons = await dbContext.Coupons
            .AsNoTracking()
            .Where(coupon => coupon.IsActive && coupon.StartsAt <= now && coupon.EndsAt >= now)
            .ToListAsync();

        return coupons
            .OrderBy(coupon => coupon.MinimumAmount)
            .ThenByDescending(coupon => coupon.DiscountAmount)
            .Select(coupon => new CouponClaimItem(coupon, claimedCouponIds.Contains(coupon.Id)))
            .ToList();
    }

    public async Task<List<UserCoupon>> GetUserCouponsAsync(string userId)
    {
        return await dbContext.UserCoupons
            .AsNoTracking()
            .Include(item => item.Coupon)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.ClaimedAt)
            .ToListAsync();
    }

    public async Task<bool> ClaimCouponAsync(string userId, int couponId)
    {
        var now = DateTime.UtcNow;
        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(item =>
            item.Id == couponId &&
            item.IsActive &&
            item.StartsAt <= now &&
            item.EndsAt >= now);

        if (coupon is null)
        {
            return false;
        }

        var exists = await dbContext.UserCoupons
            .AnyAsync(item => item.UserId == userId && item.CouponId == couponId);
        if (exists)
        {
            return false;
        }

        dbContext.UserCoupons.Add(new UserCoupon
        {
            UserId = userId,
            CouponId = couponId,
            ClaimedAt = now
        });
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<CouponManageItem>> GetCouponsAsync()
    {
        var coupons = await dbContext.Coupons
            .AsNoTracking()
            .Include(c => c.UserCoupons)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return coupons.Select(c => new CouponManageItem(
            c,
            c.UserCoupons.Count,
            c.UserCoupons.Count(uc => uc.UsedAt is not null),
            c.UserCoupons.Count(uc => uc.UsedAt is null)
        )).ToList();
    }

    public async Task<PagedCouponsResult> GetPagedCouponsAsync(int page = 1, int pageSize = 12)
    {
        var query = dbContext.Coupons
            .AsNoTracking()
            .Include(c => c.UserCoupons)
            .OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync();
        var safePageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));
        var currentPage = Math.Min(Math.Max(1, page), totalPages);

        var coupons = await query
            .Skip((currentPage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        var items = coupons.Select(c => new CouponManageItem(
            c,
            c.UserCoupons.Count,
            c.UserCoupons.Count(uc => uc.UsedAt is not null),
            c.UserCoupons.Count(uc => uc.UsedAt is null)
        )).ToList();

        return new PagedCouponsResult(items, totalCount, currentPage, safePageSize);
    }

    public async Task<Coupon?> GetCouponAsync(int id)
    {
        return await dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddCouponAsync(Coupon coupon)
    {
        dbContext.Coupons.Add(coupon);
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> UpdateCouponAsync(Coupon input)
    {
        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == input.Id);
        if (coupon is null) return false;

        coupon.Name = input.Name;
        coupon.Code = input.Code;
        coupon.MinimumAmount = input.MinimumAmount;
        coupon.DiscountAmount = input.DiscountAmount;
        coupon.IsActive = input.IsActive;
        coupon.StartsAt = input.StartsAt;
        coupon.EndsAt = input.EndsAt;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCouponAsync(int id)
    {
        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon is null) return false;

        dbContext.Coupons.Remove(coupon);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleCouponActiveAsync(int id)
    {
        var coupon = await dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon is null) return false;

        coupon.IsActive = !coupon.IsActive;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<Order?> CreateOrderAsync(
        string userId,
        string receiverName,
        string receiverPhone,
        string shippingAddress,
        int? userCouponId = null)
    {
        var cartItems = await dbContext.CartItems
            .Include(item => item.Product)
            .Where(item => item.UserId == userId)
            .ToListAsync();

        if (cartItems.Count == 0)
        {
            return null;
        }

        foreach (var cartItem in cartItems)
        {
            if (cartItem.Product is null || !cartItem.Product.IsActive || cartItem.Quantity > cartItem.Product.Stock)
            {
                return null;
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var order = new Order
        {
            UserId = userId,
            ReceiverName = receiverName,
            ReceiverPhone = receiverPhone,
            ShippingAddress = shippingAddress,
            Status = OrderStatuses.NewOrder,
            CreatedAt = DateTime.UtcNow
        };

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = null,
            ToStatus = OrderStatuses.NewOrder,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = userId
        });

        var orderInventoryLogs = new List<InventoryChangeLog>();
        foreach (var cartItem in cartItems)
        {
            var product = cartItem.Product!;
            var oldStock = product.Stock;
            product.Stock -= cartItem.Quantity;
            orderInventoryLogs.Add(AddInventoryChangeLog(
                product,
                oldStock,
                product.Stock,
                "OrderCreated",
                userId,
                null,
                $"订单创建扣减库存，数量 {cartItem.Quantity}"));
            if (product.IsActive && product.Stock <= 5)
            {
                await AddAdminNotificationAsync(
                    "低库存提醒",
                    $"《{product.Title}》下单后库存剩余 {product.Stock}，请及时补货。",
                    "LowStock",
                    $"/Admin/Products/Edit/{product.Id}");
            }
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Title = product.Title,
                Author = product.Author,
                CoverUrl = product.CoverUrl,
                Quantity = cartItem.Quantity,
                UnitPrice = product.Price
            });
        }

        var originalAmount = order.Items.Sum(item => item.LineTotal);
        UserCoupon? selectedCoupon = null;
        if (userCouponId is > 0)
        {
            selectedCoupon = await dbContext.UserCoupons
                .Include(item => item.Coupon)
                .FirstOrDefaultAsync(item =>
                    item.Id == userCouponId.Value &&
                    item.UserId == userId &&
                    item.UsedAt == null);

            if (selectedCoupon?.Coupon is null || !IsCouponUsable(selectedCoupon.Coupon, originalAmount))
            {
                await transaction.RollbackAsync();
                return null;
            }

            order.CouponName = selectedCoupon.Coupon.Name;
            order.DiscountAmount = Math.Min(selectedCoupon.Coupon.DiscountAmount, originalAmount);
        }

        order.TotalAmount = Math.Max(0, originalAmount - order.DiscountAmount);
        dbContext.Orders.Add(order);
        dbContext.CartItems.RemoveRange(cartItems);
        await dbContext.SaveChangesAsync();
        foreach (var log in orderInventoryLogs)
        {
            log.OrderId = order.Id;
        }

        if (selectedCoupon is not null)
        {
            selectedCoupon.UsedAt = DateTime.UtcNow;
            selectedCoupon.OrderId = order.Id;
        }

        await AwardMembershipAsync(userId, order);

        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();

        return await dbContext.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == order.Id);
    }

    public async Task<List<Order>> GetOrdersAsync(string userId, bool isAdmin)
    {
        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.Items)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(order => order.UserId == userId);
        }

        return await query.OrderByDescending(order => order.CreatedAt).ToListAsync();
    }

    public async Task<Order?> GetOrderAsync(int id, string userId, bool isAdmin)
    {
        var query = dbContext.Orders
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.Items)
            .Include(order => order.StatusHistory.OrderBy(h => h.ChangedAt))
            .Where(order => order.Id == id);

        if (!isAdmin)
        {
            query = query.Where(order => order.UserId == userId);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<bool> CancelOrderAsync(int id, string userId, bool isAdmin, string changedBy)
    {
        var order = await dbContext.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && (isAdmin || item.UserId == userId));

        if (order is null)
        {
            return false;
        }

        var canCancel = isAdmin
            ? OrderStatuses.CanTransition(order.Status, OrderStatuses.Cancelled)
            : OrderStatuses.CanCustomerCancel(order.Status);

        if (!canCancel)
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await RestoreOrderStockAsync(order, changedBy);
        AddOrderStatusHistory(order, OrderStatuses.Cancelled, changedBy);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> PayOrderAsync(int id, string userId)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (order is null || !OrderStatuses.CanTransition(order.Status, OrderStatuses.Paid))
        {
            return false;
        }

        AddOrderStatusHistory(order, OrderStatuses.Paid, userId);
        AddUserNotification(
            userId,
            "支付成功",
            $"订单 #{order.Id} 已支付成功，等待商家发货。",
            "Payment",
            $"/Orders/Details/{order.Id}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ConfirmReceiptAsync(int id, string userId)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (order is null || !OrderStatuses.CanTransition(order.Status, OrderStatuses.Received))
        {
            return false;
        }

        AddOrderStatusHistory(order, OrderStatuses.Received, userId);
        AddUserNotification(
            userId,
            "已确认收货",
            $"订单 #{order.Id} 已确认收货，可以为购买过的图书留下评价。",
            "Receipt",
            $"/Orders/Details/{order.Id}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RequestRefundAsync(int id, string userId, string reason)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (order is null || !CanRequestRefund(order))
        {
            return false;
        }

        var trimmedReason = (reason ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            return false;
        }

        order.RefundRequestedAt = DateTime.UtcNow;
        order.RefundReason = trimmedReason.Length > 500 ? trimmedReason[..500] : trimmedReason;
        order.RefundReviewedAt = null;
        order.RefundReviewedBy = string.Empty;
        order.RefundReviewNote = string.Empty;
        await AddAdminNotificationAsync(
            "新的退款申请",
            $"订单 #{order.Id} 提交了退款申请，请及时审核。",
            "RefundRequest",
            $"/Orders/Details/{order.Id}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveRefundAsync(int id, string changedBy, string note)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(item => item.Id == id);

        if (order is null || !order.HasPendingRefund)
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        order.RefundReviewedAt = DateTime.UtcNow;
        order.RefundReviewedBy = changedBy;
        order.RefundReviewNote = TrimRefundNote(note);
        AddOrderStatusHistory(order, OrderStatuses.Refunded, changedBy);
        AddUserNotification(
            order.UserId,
            "退款申请已通过",
            $"订单 #{order.Id} 的退款申请已通过，订单已进入已退款状态。",
            "RefundApproved",
            $"/Orders/Details/{order.Id}");
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> RejectRefundAsync(int id, string changedBy, string note)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(item => item.Id == id);

        if (order is null || !order.HasPendingRefund)
        {
            return false;
        }

        var trimmedNote = TrimRefundNote(note);
        if (string.IsNullOrWhiteSpace(trimmedNote))
        {
            return false;
        }

        order.RefundReviewedAt = DateTime.UtcNow;
        order.RefundReviewedBy = changedBy;
        order.RefundReviewNote = trimmedNote;
        AddUserNotification(
            order.UserId,
            "退款申请已拒绝",
            $"订单 #{order.Id} 的退款申请未通过，审核说明：{trimmedNote}",
            "RefundRejected",
            $"/Orders/Details/{order.Id}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ShipOrderAsync(int id, string trackingCompany, string trackingNumber, string changedBy)
    {
        var order = await dbContext.Orders.FirstOrDefaultAsync(item => item.Id == id);
        if (order is null || !OrderStatuses.CanTransition(order.Status, OrderStatuses.Shipped))
        {
            return false;
        }

        trackingCompany = (trackingCompany ?? string.Empty).Trim();
        trackingNumber = (trackingNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trackingCompany) || string.IsNullOrWhiteSpace(trackingNumber))
        {
            return false;
        }

        order.TrackingCompany = trackingCompany.Length > 80 ? trackingCompany[..80] : trackingCompany;
        order.TrackingNumber = trackingNumber.Length > 80 ? trackingNumber[..80] : trackingNumber;
        order.ShippedAt = DateTime.UtcNow;
        AddOrderStatusHistory(order, OrderStatuses.Shipped, changedBy);
        AddUserNotification(
            order.UserId,
            "订单已发货",
            $"订单 #{order.Id} 已由 {order.TrackingCompany} 发出，物流单号 {order.TrackingNumber}。",
            "Shipping",
            $"/Orders/Details/{order.Id}");
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateOrderStatusAsync(int id, string status, string changedBy)
    {
        var order = await dbContext.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (order is null)
        {
            return false;
        }

        if (!OrderStatuses.CanTransition(order.Status, status))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        if (status == OrderStatuses.Cancelled)
        {
            await RestoreOrderStockAsync(order, changedBy);
        }

        if (status == OrderStatuses.Shipped && order.ShippedAt is null)
        {
            order.ShippedAt = DateTime.UtcNow;
        }

        AddOrderStatusHistory(order, status, changedBy);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    private async Task RestoreOrderStockAsync(Order order, string changedBy)
    {
        foreach (var item in order.Items)
        {
            var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product is not null)
            {
                var oldStock = product.Stock;
                product.Stock += item.Quantity;
                AddInventoryChangeLog(
                    product,
                    oldStock,
                    product.Stock,
                    "OrderCancelled",
                    changedBy,
                    order.Id,
                    $"订单取消回滚库存，数量 {item.Quantity}");
            }
        }
    }

    private static void AddOrderStatusHistory(Order order, string status, string changedBy)
    {
        var fromStatus = order.Status;
        order.Status = status;
        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = fromStatus,
            ToStatus = status,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy
        });
    }

    private InventoryChangeLog AddInventoryChangeLog(
        BookProduct product,
        int quantityBefore,
        int quantityAfter,
        string changeType,
        string changedBy,
        int? orderId,
        string note)
    {
        var log = new InventoryChangeLog
        {
            ProductId = product.Id,
            ProductTitle = product.Title,
            QuantityBefore = quantityBefore,
            QuantityAfter = quantityAfter,
            QuantityChanged = quantityAfter - quantityBefore,
            ChangeType = changeType,
            ChangedBy = changedBy,
            OrderId = orderId,
            Note = note,
            ChangedAt = DateTime.UtcNow
        };

        dbContext.InventoryChangeLogs.Add(log);
        return log;
    }

    private static bool IsCouponUsable(Coupon coupon, decimal orderAmount)
    {
        var now = DateTime.UtcNow;
        return coupon.IsActive &&
               coupon.StartsAt <= now &&
               coupon.EndsAt >= now &&
               coupon.MinimumAmount <= orderAmount &&
               coupon.DiscountAmount > 0;
    }

    public static bool CanRequestRefund(Order order)
    {
        return order.RefundRequestedAt is null &&
               (order.Status == OrderStatuses.Paid || order.Status == OrderStatuses.Received);
    }

    private static string TrimRefundNote(string note)
    {
        var trimmedNote = (note ?? string.Empty).Trim();
        return trimmedNote.Length > 500 ? trimmedNote[..500] : trimmedNote;
    }

    public async Task<List<UserNotification>> GetNotificationsAsync(string userId, int take = 50)
    {
        return await dbContext.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadNotificationCountAsync(string userId)
    {
        return await dbContext.UserNotifications
            .CountAsync(notification => notification.UserId == userId && notification.ReadAt == null);
    }

    public async Task<bool> MarkNotificationReadAsync(string userId, int notificationId)
    {
        var notification = await dbContext.UserNotifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId);
        if (notification is null)
        {
            return false;
        }

        notification.ReadAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllNotificationsReadAsync(string userId)
    {
        var notifications = await dbContext.UserNotifications
            .Where(item => item.UserId == userId && item.ReadAt == null)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.ReadAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<List<InventoryChangeLog>> GetInventoryChangeLogsAsync(int take = 100)
    {
        return await dbContext.InventoryChangeLogs
            .AsNoTracking()
            .Include(log => log.Product)
            .Include(log => log.Order)
            .OrderByDescending(log => log.ChangedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<PagedInventoryLogsResult> GetPagedInventoryChangeLogsAsync(int page = 1, int pageSize = 12)
    {
        var query = dbContext.InventoryChangeLogs
            .AsNoTracking()
            .Include(log => log.Product)
            .Include(log => log.Order)
            .OrderByDescending(log => log.ChangedAt);

        var totalCount = await query.CountAsync();
        var safePageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));
        var currentPage = Math.Min(Math.Max(1, page), totalPages);

        var items = await query
            .Skip((currentPage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return new PagedInventoryLogsResult(items, totalCount, currentPage, safePageSize);
    }

    private async Task<HashSet<string>> GetUserPreferredCategoriesAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var preferredItems = await dbContext.OrderItems
            .AsNoTracking()
            .Include(item => item.Order)
            .Include(item => item.Product)
            .Where(item =>
                item.Order != null &&
                item.Product != null &&
                item.Order.UserId == userId &&
                (item.Order.Status == OrderStatuses.Paid ||
                 item.Order.Status == OrderStatuses.Shipped ||
                 item.Order.Status == OrderStatuses.Received ||
                 item.Order.Status == OrderStatuses.Completed))
            .ToListAsync();

        return preferredItems
            .GroupBy(item => item.Product!.Category)
            .OrderByDescending(group => group.Sum(item => item.Quantity))
            .Take(3)
            .Select(group => group.Key)
            .ToHashSet();
    }

    private static BookAssistantIntent BuildAssistantIntent(string queryText)
    {
        var text = queryText.ToLowerInvariant();
        var categories = new HashSet<string>();
        if (text.Contains("编程") || text.Contains("开发") || text.Contains("代码") || text.Contains("程序"))
        {
            categories.Add("编程开发");
        }
        if (text.Contains("ai") || text.Contains("人工智能") || text.Contains("机器学习") || text.Contains("深度学习"))
        {
            categories.Add("人工智能");
        }
        if (text.Contains("小说") || text.Contains("文学") || text.Contains("故事"))
        {
            categories.Add("文学小说");
        }
        if (text.Contains("商业") || text.Contains("管理") || text.Contains("创业") || text.Contains("运营"))
        {
            categories.Add("商业管理");
        }
        if (text.Contains("设计") || text.Contains("审美") || text.Contains("创意"))
        {
            categories.Add("设计创意");
        }
        if (text.Contains("历史") || text.Contains("人文") || text.Contains("文化"))
        {
            categories.Add("历史人文");
        }
        if (text.Contains("教材") || text.Contains("考试") || text.Contains("教辅") || text.Contains("学习"))
        {
            categories.Add("教材教辅");
        }

        return new BookAssistantIntent(
            categories,
            text.Contains("便宜") || text.Contains("低价") || text.Contains("预算") || text.Contains("入门") || text.Contains("新手"),
            text.Contains("进阶") || text.Contains("深入") || text.Contains("高级") || text.Contains("专业"),
            text.Contains("热门") || text.Contains("畅销") || text.Contains("大家都买"),
            text.Contains("最新") || text.Contains("新书") || text.Contains("刚上架"));
    }

    private static decimal ScoreAssistantProduct(
        BookProduct product,
        string queryText,
        BookAssistantIntent intent,
        HashSet<string> preferredCategories,
        int salesCount,
        double averageRating,
        int reviewCount)
    {
        var score = 0m;
        var text = queryText.ToLowerInvariant();
        var searchable = $"{product.Title} {product.Author} {product.Publisher} {product.Category} {product.Description}".ToLowerInvariant();
        var tokens = text.Split(
            [' ', ',', '，', '。', '?', '？', '、', ';', '；'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens.Where(token => token.Length > 1))
        {
            if (searchable.Contains(token))
            {
                score += 8;
            }
        }

        if (intent.Categories.Contains(product.Category))
        {
            score += 34;
        }
        if (preferredCategories.Contains(product.Category))
        {
            score += 8;
        }
        if (intent.WantsCheap && product.Price <= 80)
        {
            score += 10;
        }
        if (intent.WantsAdvanced && (product.Description.Contains("进阶") || product.Description.Contains("深入") || product.Title.Contains("高级")))
        {
            score += 10;
        }
        if (intent.WantsPopular)
        {
            score += salesCount * 3;
        }
        else
        {
            score += Math.Min(salesCount, 20);
        }
        if (intent.WantsNew)
        {
            score += product.CreatedAt >= DateTime.UtcNow.AddMonths(-3) ? 12 : 0;
        }

        score += (decimal)averageRating * 2;
        score += Math.Min(reviewCount, 10);
        score += product.Stock > 0 ? 4 : -20;
        return score;
    }

    private static string BuildAssistantReason(BookProduct product, BookAssistantIntent intent, double averageRating, int reviewCount, int salesCount)
    {
        var reasons = new List<string>();
        if (intent.Categories.Contains(product.Category))
        {
            reasons.Add($"匹配你提到的“{product.Category}”方向");
        }
        if (averageRating > 0)
        {
            reasons.Add($"评分 {averageRating:0.0}");
        }
        if (salesCount > 0)
        {
            reasons.Add($"已有 {salesCount} 次成交");
        }
        if (reviewCount > 0 && reasons.Count < 3)
        {
            reasons.Add($"{reviewCount} 条评价可参考");
        }
        if (reasons.Count == 0)
        {
            reasons.Add("主题和当前问题比较接近");
        }

        return string.Join("，", reasons.Take(3));
    }

    private async Task AwardMembershipAsync(string userId, Order order)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return;
        }

        var growthToAdd = (int)Math.Floor(order.TotalAmount);
        if (growthToAdd <= 0)
        {
            return;
        }

        var oldLevel = BuildMemberLevel(user.MemberGrowth);
        user.MemberPoints += growthToAdd;
        user.MemberGrowth += growthToAdd;
        user.MembershipStartedAt ??= DateTime.UtcNow;
        user.MemberLevel = BuildMemberLevel(user.MemberGrowth);

        AddUserNotification(
            userId,
            "会员积分到账",
            $"本次下单获得 {growthToAdd} 积分，当前等级为 {user.MemberLevel}。",
            "MembershipPoints",
            "/Account/Membership");

        if (user.MemberLevel != oldLevel)
        {
            AddUserNotification(
                userId,
                "会员等级已升级",
                $"恭喜升级为 {user.MemberLevel}，新的会员权益已经生效。",
                "MembershipLevelUp",
                "/Account/Membership");
        }
    }

    private static string BuildMemberLevel(int growth)
    {
        if (growth >= 2000) return "黑钻会员";
        if (growth >= 1000) return "金卡会员";
        if (growth >= 300) return "银卡会员";
        return "普通会员";
    }

    private static int GetMemberLevelStart(int growth)
    {
        if (growth >= 2000) return 2000;
        if (growth >= 1000) return 1000;
        if (growth >= 300) return 300;
        return 0;
    }

    private static (string? Level, int? Growth) GetNextMemberLevel(int growth)
    {
        if (growth < 300) return ("银卡会员", 300);
        if (growth < 1000) return ("金卡会员", 1000);
        if (growth < 2000) return ("黑钻会员", 2000);
        return (null, null);
    }

    private static IReadOnlyList<MemberBenefit> GetMemberBenefits(string level)
    {
        var baseBenefits = new List<MemberBenefit>
        {
            new("积分回馈", "每消费 1 元获得 1 积分，可用于后续活动兑换。"),
            new("会员提醒", "积分到账、等级升级会通过通知中心提醒。")
        };

        if (level is "银卡会员" or "金卡会员" or "黑钻会员")
        {
            baseBenefits.Add(new MemberBenefit("优先凑单", "购物袋凑单助手会优先匹配可用优惠门槛。"));
        }
        if (level is "金卡会员" or "黑钻会员")
        {
            baseBenefits.Add(new MemberBenefit("专属推荐", "根据历史订单偏好强化猜你喜欢和 AI 选书推荐。"));
        }
        if (level is "黑钻会员")
        {
            baseBenefits.Add(new MemberBenefit("高阶权益", "适合后续扩展生日券、会员专享券和专属客服。"));
        }

        return baseBenefits;
    }

    private void AddUserNotification(
        string userId,
        string title,
        string message,
        string type,
        string linkUrl)
    {
        dbContext.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            LinkUrl = linkUrl,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task AddAdminNotificationAsync(
        string title,
        string message,
        string type,
        string linkUrl)
    {
        var adminRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Name == UserRoles.Admin);
        if (adminRole is null)
        {
            return;
        }

        var adminUserIds = await dbContext.UserRoles
            .Where(userRole => userRole.RoleId == adminRole.Id)
            .Select(userRole => userRole.UserId)
            .ToListAsync();

        foreach (var adminUserId in adminUserIds)
        {
            var duplicateUnread = await dbContext.UserNotifications.AnyAsync(notification =>
                notification.UserId == adminUserId &&
                notification.ReadAt == null &&
                notification.Type == type &&
                notification.LinkUrl == linkUrl);
            if (duplicateUnread)
            {
                continue;
            }

            AddUserNotification(adminUserId, title, message, type, linkUrl);
        }
    }

    private static IReadOnlyList<AccountChartItem> WithAccountPercents(IReadOnlyList<AccountChartItem> items)
    {
        var max = items.Count == 0 ? 0 : items.Max(item => item.Value);
        return items
            .Select(item => item with
            {
                Percent = max <= 0 ? 0 : Math.Max(4, (int)Math.Round((double)(item.Value / max) * 100))
            })
            .ToList();
    }

    private static IReadOnlyList<AccountRadarItem> BuildRadarItems(IReadOnlyList<AccountChartItem> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        const double center = 50;
        const double axisRadius = 44;
        const double valueRadius = 38;
        return items
            .Select((item, index) =>
            {
                var angle = (-90 + index * 360.0 / items.Count) * Math.PI / 180;
                var axisX = center + axisRadius * Math.Cos(angle);
                var axisY = center + axisRadius * Math.Sin(angle);
                var dotRadius = valueRadius * item.Percent / 100.0;
                var dotX = center + dotRadius * Math.Cos(angle);
                var dotY = center + dotRadius * Math.Sin(angle);

                return new AccountRadarItem(
                    item.Label,
                    item.Percent,
                    axisX,
                    axisY,
                    dotX,
                    dotY);
            })
            .ToList();
    }
}

public sealed record PagedProductsResult(
    IReadOnlyList<BookProduct> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record ReviewSummary(int Count, double AverageRating)
{
    public string AverageText => Count == 0 ? "暂无评分" : AverageRating.ToString("0.0");
}

public sealed record CouponClaimItem(Coupon Coupon, bool IsClaimed);

public sealed record OrderBoosterResult(
    Coupon? TargetCoupon,
    decimal GapAmount,
    bool IsClaimed,
    IReadOnlyList<OrderBoosterSuggestion> Suggestions);

public sealed record OrderBoosterSuggestion(
    BookProduct Product,
    decimal RemainingGap,
    bool ReachesTarget);

public sealed record BookAssistantResponse(string Message, IReadOnlyList<BookAssistantSuggestion> Suggestions);

public sealed record BookAssistantSuggestion(
    int Id,
    string Title,
    string Author,
    string Category,
    string CoverUrl,
    decimal Price,
    string Reason);

public sealed record BookAssistantIntent(
    HashSet<string> Categories,
    bool WantsCheap,
    bool WantsAdvanced,
    bool WantsPopular,
    bool WantsNew);
public sealed record CouponManageItem(
    Coupon Coupon,
    int TotalClaimed,
    int TotalUsed,
    int TotalUnused
);

public sealed record PagedCouponsResult(
    IReadOnlyList<CouponManageItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record PagedInventoryLogsResult(
    IReadOnlyList<InventoryChangeLog> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record AccountInsightResult(
    IReadOnlyList<AccountChartItem> MonthlyOrders,
    IReadOnlyList<AccountChartItem> MonthlySpend,
    IReadOnlyList<AccountChartItem> CategoryPreferences,
    IReadOnlyList<AccountRadarItem> RadarItems,
    decimal TotalSpent,
    int PaidOrderCount,
    int PurchasedBookCount,
    string FavoriteCategory)
{
    public string MonthlySpendPolyline => BuildPolyline(MonthlySpend);

    public string RadarPolygon => RadarItems.Count == 0
        ? string.Empty
        : string.Join(", ", RadarItems.Select(item => $"{item.DotX:0.##}% {item.DotY:0.##}%"));

    private static string BuildPolyline(IReadOnlyList<AccountChartItem> items)
    {
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items.Count == 1)
        {
            var y = 94 - items[0].Percent * 86 / 100;
            return $"0,{y} 100,{y}";
        }

        return string.Join(" ", items.Select((item, index) =>
        {
            var x = index * 100 / (items.Count - 1);
            var y = 94 - item.Percent * 86 / 100;
            return $"{x},{y}";
        }));
    }
}

public sealed record MemberCenterResult(
    string Level,
    int Points,
    int Growth,
    DateTime? StartedAt,
    int? NextLevelGrowth,
    int ProgressPercent,
    string? NextLevelName,
    IReadOnlyList<MemberBenefit> Benefits,
    IReadOnlyList<MemberOrderReward> RecentRewards);

public sealed record MemberBenefit(string Title, string Description);

public sealed record MemberOrderReward(int OrderId, decimal Amount, int Points, DateTime CreatedAt);

public sealed record AccountChartItem(string Label, decimal Value, string DisplayValue)
{
    public int Percent { get; init; }
}

public sealed record AccountRadarItem(
    string Label,
    int Percent,
    double AxisX,
    double AxisY,
    double DotX,
    double DotY);

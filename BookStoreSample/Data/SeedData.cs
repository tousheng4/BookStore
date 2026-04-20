using BookStoreSample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using System.Data;

namespace BookStoreSample.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await EnsureReviewTableAsync(dbContext);
        await EnsureOrderLogisticsColumnsAsync(dbContext);
        await EnsureProductStatusColumnAsync(dbContext);
        await EnsureQuestionTableAsync(dbContext);
        await EnsureCouponTablesAsync(dbContext);
        await EnsureNotificationTableAsync(dbContext);
        await EnsureUserMembershipColumnsAsync(dbContext);
        await EnsureInventoryChangeLogTableAsync(dbContext);
        await EnsureOrderCouponColumnsAsync(dbContext);
        await EnsureOrderRefundColumnsAsync(dbContext);

        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { UserRoles.Admin, UserRoles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                DisplayName = "系统管理员",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(adminUser, "admin123");
            await userManager.AddToRoleAsync(adminUser, UserRoles.Admin);
        }

        var seedProducts = BuildSeedProducts();
        var existingTitles = await dbContext.Products
            .Select(product => product.Title)
            .ToListAsync();

        var missingProducts = seedProducts
            .Where(product => !existingTitles.Contains(product.Title))
            .ToList();

        if (missingProducts.Count > 0)
        {
            dbContext.Products.AddRange(missingProducts);
            await dbContext.SaveChangesAsync();
        }

        await SeedReviewsAsync(dbContext, userManager);
        await SeedQuestionsAsync(dbContext, userManager);
        await SeedCouponsAsync(dbContext);
        await SeedLjcOrdersAsync(dbContext, userManager);
        await BackfillMembershipAsync(dbContext);
    }

    private static async Task EnsureUserMembershipColumnsAsync(ApplicationDbContext dbContext)
    {
        await EnsureColumnAsync(dbContext, "AspNetUsers", "AvatarUrl", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "AspNetUsers", "MemberPoints", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(dbContext, "AspNetUsers", "MemberGrowth", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(dbContext, "AspNetUsers", "MemberLevel", "TEXT NOT NULL DEFAULT '普通会员'");
        await EnsureColumnAsync(dbContext, "AspNetUsers", "MembershipStartedAt", "TEXT NULL");
    }

    private static async Task EnsureReviewTableAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BookReviews" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BookReviews" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "ProductId" INTEGER NOT NULL,
                "Rating" INTEGER NOT NULL,
                "Content" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_BookReviews_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_BookReviews_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BookReviews_UserId_ProductId"
            ON "BookReviews" ("UserId", "ProductId");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_BookReviews_ProductId"
            ON "BookReviews" ("ProductId");
            """);

        await EnsureColumnAsync(dbContext, "BookReviews", "ImageUrl", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "BookReviews", "FollowUpContent", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "BookReviews", "FollowUpAt", "TEXT NULL");
    }

    private static async Task EnsureQuestionTableAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BookQuestions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BookQuestions" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "ProductId" INTEGER NOT NULL,
                "Question" TEXT NOT NULL,
                "Answer" TEXT NOT NULL DEFAULT '',
                "AnsweredBy" TEXT NOT NULL DEFAULT '',
                "CreatedAt" TEXT NOT NULL,
                "AnsweredAt" TEXT NULL,
                CONSTRAINT "FK_BookQuestions_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_BookQuestions_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_BookQuestions_ProductId_CreatedAt"
            ON "BookQuestions" ("ProductId", "CreatedAt");
            """);
    }

    private static async Task EnsureOrderLogisticsColumnsAsync(ApplicationDbContext dbContext)
    {
        await EnsureColumnAsync(dbContext, "Orders", "TrackingCompany", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "Orders", "TrackingNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "Orders", "ShippedAt", "TEXT NULL");
    }

    private static async Task EnsureProductStatusColumnAsync(ApplicationDbContext dbContext)
    {
        await EnsureColumnAsync(dbContext, "Products", "IsActive", "INTEGER NOT NULL DEFAULT 1");
    }

    private static async Task EnsureCouponTablesAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Coupons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Coupons" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Code" TEXT NOT NULL,
                "MinimumAmount" TEXT NOT NULL,
                "DiscountAmount" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "StartsAt" TEXT NOT NULL,
                "EndsAt" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Coupons_Code"
            ON "Coupons" ("Code");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserCoupons" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserCoupons" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "CouponId" INTEGER NOT NULL,
                "ClaimedAt" TEXT NOT NULL,
                "UsedAt" TEXT NULL,
                "OrderId" INTEGER NULL,
                CONSTRAINT "FK_UserCoupons_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_UserCoupons_Coupons_CouponId" FOREIGN KEY ("CouponId") REFERENCES "Coupons" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_UserCoupons_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE SET NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserCoupons_UserId_CouponId"
            ON "UserCoupons" ("UserId", "CouponId");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_UserCoupons_CouponId"
            ON "UserCoupons" ("CouponId");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserCoupons_OrderId"
            ON "UserCoupons" ("OrderId");
            """);
    }

    private static async Task EnsureOrderCouponColumnsAsync(ApplicationDbContext dbContext)
    {
        await EnsureColumnAsync(dbContext, "Orders", "CouponName", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "Orders", "DiscountAmount", "TEXT NOT NULL DEFAULT '0'");
    }

    private static async Task EnsureNotificationTableAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserNotifications" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserNotifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Type" TEXT NOT NULL,
                "LinkUrl" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ReadAt" TEXT NULL,
                CONSTRAINT "FK_UserNotifications_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_UserNotifications_UserId_ReadAt_CreatedAt"
            ON "UserNotifications" ("UserId", "ReadAt", "CreatedAt");
            """);
    }

    private static async Task EnsureInventoryChangeLogTableAsync(ApplicationDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "InventoryChangeLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InventoryChangeLogs" PRIMARY KEY AUTOINCREMENT,
                "ProductId" INTEGER NOT NULL,
                "ProductTitle" TEXT NOT NULL,
                "QuantityBefore" INTEGER NOT NULL,
                "QuantityAfter" INTEGER NOT NULL,
                "QuantityChanged" INTEGER NOT NULL,
                "ChangeType" TEXT NOT NULL,
                "ChangedBy" TEXT NOT NULL,
                "OrderId" INTEGER NULL,
                "Note" TEXT NOT NULL,
                "ChangedAt" TEXT NOT NULL,
                CONSTRAINT "FK_InventoryChangeLogs_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_InventoryChangeLogs_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE SET NULL
            );
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_InventoryChangeLogs_ChangedAt"
            ON "InventoryChangeLogs" ("ChangedAt");
            """);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_InventoryChangeLogs_ProductId"
            ON "InventoryChangeLogs" ("ProductId");
            """);
    }

    private static async Task EnsureOrderRefundColumnsAsync(ApplicationDbContext dbContext)
    {
        await EnsureColumnAsync(dbContext, "Orders", "RefundRequestedAt", "TEXT NULL");
        await EnsureColumnAsync(dbContext, "Orders", "RefundReason", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "Orders", "RefundReviewedAt", "TEXT NULL");
        await EnsureColumnAsync(dbContext, "Orders", "RefundReviewedBy", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(dbContext, "Orders", "RefundReviewNote", "TEXT NOT NULL DEFAULT ''");
    }

    private static async Task EnsureColumnAsync(
        ApplicationDbContext dbContext,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        if (await ColumnExistsAsync(dbContext, tableName, columnName))
        {
            return;
        }

        var sql = $"""
            ALTER TABLE "{EscapeIdentifier(tableName)}"
            ADD COLUMN "{EscapeIdentifier(columnName)}" {columnDefinition};
            """;
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task<bool> ColumnExistsAsync(
        ApplicationDbContext dbContext,
        string tableName,
        string columnName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{EscapeSqlLiteral(tableName)}') WHERE name = @columnName;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@columnName";
            parameter.Value = columnName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string EscapeIdentifier(string value) => value.Replace("\"", "\"\"");

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    private static async Task SeedReviewsAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        var reviewers = new[]
        {
            new ReviewerSeed("reader01", "林小满"),
            new ReviewerSeed("reader02", "周明远"),
            new ReviewerSeed("reader03", "许知夏"),
            new ReviewerSeed("reader04", "陈一舟"),
            new ReviewerSeed("reader05", "沈青禾")
        };

        var reviewerUsers = new Dictionary<string, ApplicationUser>();
        foreach (var reviewer in reviewers)
        {
            var user = await userManager.FindByNameAsync(reviewer.UserName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = reviewer.UserName,
                    DisplayName = reviewer.DisplayName,
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(user, "reader123");
                await userManager.AddToRoleAsync(user, UserRoles.Customer);
            }

            reviewerUsers[reviewer.UserName] = user;
        }

        var reviews = BuildSeedReviews();
        var titles = reviews.Select(review => review.Title).Distinct().ToList();
        var productsByTitle = await dbContext.Products
            .Where(product => titles.Contains(product.Title))
            .ToDictionaryAsync(product => product.Title);

        foreach (var review in reviews)
        {
            if (!productsByTitle.TryGetValue(review.Title, out var product) ||
                !reviewerUsers.TryGetValue(review.UserName, out var user))
            {
                continue;
            }

            var exists = await dbContext.BookReviews
                .AnyAsync(item => item.UserId == user.Id && item.ProductId == product.Id);
            if (exists)
            {
                continue;
            }

            dbContext.BookReviews.Add(new BookReview
            {
                UserId = user.Id,
                ProductId = product.Id,
                Rating = review.Rating,
                Content = review.Content,
                CreatedAt = DateTime.UtcNow.AddMinutes(-review.MinutesAgo),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-review.MinutesAgo)
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedQuestionsAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        var questionUsers = new[]
        {
            new ReviewerSeed("reader01", "林小满"),
            new ReviewerSeed("reader02", "周明远"),
            new ReviewerSeed("ljc", "ljc")
        };

        var usersByName = new Dictionary<string, ApplicationUser>();
        foreach (var seedUser in questionUsers)
        {
            var user = await userManager.FindByNameAsync(seedUser.UserName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = seedUser.UserName,
                    DisplayName = seedUser.DisplayName,
                    Email = $"{seedUser.UserName}@example.com",
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(user, seedUser.UserName == "ljc" ? "ljc123" : "reader123");
                await userManager.AddToRoleAsync(user, UserRoles.Customer);
            }

            usersByName[seedUser.UserName] = user;
        }

        var questionSeeds = new[]
        {
            new QuestionSeed("reader01", "ASP.NET Core Razor Pages 实战", "这本书适合刚开始做 Razor Pages 项目的人吗？", "适合。它从页面模型、表单提交和身份认证讲起，和这个书店项目的结构也比较接近。", "admin", 45),
            new QuestionSeed("ljc", "推荐系统设计", "如果想给书店做猜你喜欢，这本书讲得够落地吗？", "够用。它会讲召回、排序和冷启动，能帮助你理解现在站内推荐逻辑还能怎么升级。", "admin", 62),
            new QuestionSeed("reader02", "机器学习实战导论", "需要很强的数学基础吗？", "不需要一开始就很强，但最好了解一点概率统计。书里的案例比较适合边做边补基础。", "admin", 78),
            new QuestionSeed("ljc", "夜航书简", "这本是偏治愈还是偏悬疑？", "更偏治愈和安静的文学表达，悬疑感不强，适合晚上慢慢读。", "reader01", 96),
            new QuestionSeed("reader01", "数据库查询优化", "适合后端初级开发看吗？", "", "", 110)
        };

        var titles = questionSeeds.Select(seed => seed.Title).Distinct().ToList();
        var productsByTitle = await dbContext.Products
            .Where(product => titles.Contains(product.Title))
            .ToDictionaryAsync(product => product.Title);

        foreach (var seed in questionSeeds)
        {
            if (!productsByTitle.TryGetValue(seed.Title, out var product) ||
                !usersByName.TryGetValue(seed.UserName, out var user))
            {
                continue;
            }

            var exists = await dbContext.BookQuestions.AnyAsync(question =>
                question.UserId == user.Id &&
                question.ProductId == product.Id &&
                question.Question == seed.Question);
            if (exists)
            {
                continue;
            }

            var createdAt = DateTime.UtcNow.AddMinutes(-seed.MinutesAgo);
            dbContext.BookQuestions.Add(new BookQuestion
            {
                UserId = user.Id,
                ProductId = product.Id,
                Question = seed.Question,
                Answer = seed.Answer,
                AnsweredBy = seed.AnsweredBy,
                CreatedAt = createdAt,
                AnsweredAt = string.IsNullOrWhiteSpace(seed.Answer)
                    ? null
                    : createdAt.AddMinutes(12)
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCouponsAsync(ApplicationDbContext dbContext)
    {
        var coupons = new[]
        {
            new Coupon
            {
                Name = "新客满 99 减 10",
                Code = "NEW10",
                MinimumAmount = 99m,
                DiscountAmount = 10m,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddMonths(2)
            },
            new Coupon
            {
                Name = "精选图书满 199 减 25",
                Code = "BOOK25",
                MinimumAmount = 199m,
                DiscountAmount = 25m,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddMonths(2)
            },
            new Coupon
            {
                Name = "囤书满 299 减 45",
                Code = "READ45",
                MinimumAmount = 299m,
                DiscountAmount = 45m,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddMonths(2)
            }
        };

        var existingCodes = await dbContext.Coupons.Select(coupon => coupon.Code).ToListAsync();
        var missingCoupons = coupons
            .Where(coupon => !existingCodes.Contains(coupon.Code))
            .ToList();

        if (missingCoupons.Count == 0)
        {
            return;
        }

        dbContext.Coupons.AddRange(missingCoupons);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLjcOrdersAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByNameAsync("ljc");
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = "ljc",
                DisplayName = "ljc",
                Email = "ljc@example.com",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-8)
            };

            await userManager.CreateAsync(user, "ljc123");
            await userManager.AddToRoleAsync(user, UserRoles.Customer);
        }

        var hasSeedOrders = await dbContext.Orders.AnyAsync(order =>
            order.UserId == user.Id &&
            order.ReceiverName == "ljc 演示客户");
        if (hasSeedOrders)
        {
            return;
        }

        var productTitles = new[]
        {
            "ASP.NET Core Razor Pages 实战",
            "C# 高性能开发",
            "数据库查询优化",
            "机器学习实战导论",
            "推荐系统设计",
            "大模型工程化指南",
            "夜航书简",
            "岛屿来信",
            "增长策略手册",
            "产品经理实务",
            "版式设计原理",
            "界面视觉系统",
            "中国古代城市史",
            "数据库系统概论"
        };
        var products = await dbContext.Products
            .Where(product => productTitles.Contains(product.Title))
            .ToDictionaryAsync(product => product.Title);

        var now = DateTime.UtcNow;
        var orderSeeds = new[]
        {
            new LjcOrderSeed(
                OrderStatuses.Completed,
                now.AddMonths(-5).AddDays(-2),
                null,
                null,
                "",
                10m,
                "新客满 99 减 10",
                [
                    new("ASP.NET Core Razor Pages 实战", 1),
                    new("C# 高性能开发", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.Completed,
                now.AddMonths(-4).AddDays(-6),
                null,
                null,
                "",
                0m,
                "",
                [
                    new("数据库查询优化", 1),
                    new("机器学习实战导论", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.Received,
                now.AddMonths(-3).AddDays(-4),
                null,
                null,
                "",
                25m,
                "精选图书满 199 减 25",
                [
                    new("推荐系统设计", 2),
                    new("大模型工程化指南", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.Shipped,
                now.AddMonths(-2).AddDays(-3),
                null,
                null,
                "",
                0m,
                "",
                [
                    new("夜航书简", 1),
                    new("岛屿来信", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.Paid,
                now.AddMonths(-1).AddDays(-7),
                null,
                null,
                "",
                45m,
                "囤书满 299 减 45",
                [
                    new("增长策略手册", 1),
                    new("产品经理实务", 1),
                    new("版式设计原理", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.NewOrder,
                now.AddDays(-8),
                null,
                null,
                "",
                0m,
                "",
                [
                    new("界面视觉系统", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.Cancelled,
                now.AddDays(-18),
                null,
                null,
                "",
                0m,
                "",
                [
                    new("中国古代城市史", 1)
                ]),
            new LjcOrderSeed(
                OrderStatuses.Refunded,
                now.AddDays(-28),
                now.AddDays(-24),
                now.AddDays(-23),
                "买重复了，申请退款。",
                0m,
                "",
                [
                    new("数据库系统概论", 1)
                ])
        };

        foreach (var seed in orderSeeds)
        {
            var order = new Order
            {
                UserId = user.Id,
                ReceiverName = "ljc 演示客户",
                ReceiverPhone = "13800000001",
                ShippingAddress = "广东省 深圳市 南山区 科技园演示地址 1 号",
                Status = seed.Status,
                CreatedAt = seed.CreatedAt,
                TrackingCompany = seed.Status == OrderStatuses.Shipped ||
                                  seed.Status == OrderStatuses.Received ||
                                  seed.Status == OrderStatuses.Completed
                    ? "顺丰速运"
                    : string.Empty,
                TrackingNumber = seed.Status == OrderStatuses.Shipped ||
                                 seed.Status == OrderStatuses.Received ||
                                 seed.Status == OrderStatuses.Completed
                    ? $"SF{seed.CreatedAt:MMddHHmmss}"
                    : string.Empty,
                ShippedAt = seed.Status == OrderStatuses.Shipped ||
                            seed.Status == OrderStatuses.Received ||
                            seed.Status == OrderStatuses.Completed
                    ? seed.CreatedAt.AddDays(1)
                    : null,
                CouponName = seed.CouponName,
                DiscountAmount = seed.DiscountAmount,
                RefundRequestedAt = seed.RefundRequestedAt,
                RefundReviewedAt = seed.RefundReviewedAt,
                RefundReason = seed.RefundReason,
                RefundReviewedBy = seed.RefundReviewedAt is null ? string.Empty : "admin",
                RefundReviewNote = seed.RefundReviewedAt is null ? string.Empty : "已核实，退款通过。"
            };

            foreach (var itemSeed in seed.Items)
            {
                if (!products.TryGetValue(itemSeed.Title, out var product))
                {
                    continue;
                }

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Title = product.Title,
                    Author = product.Author,
                    CoverUrl = product.CoverUrl,
                    Quantity = itemSeed.Quantity,
                    UnitPrice = product.Price
                });
            }

            if (order.Items.Count == 0)
            {
                continue;
            }

            var originalAmount = order.Items.Sum(item => item.LineTotal);
            order.TotalAmount = Math.Max(0, originalAmount - order.DiscountAmount);
            AddSeedStatusHistory(order, seed.Status, seed.CreatedAt, user.Id);
            dbContext.Orders.Add(order);
        }

        dbContext.UserNotifications.Add(new UserNotification
        {
            UserId = user.Id,
            Title = "演示订单已准备好",
            Message = "已为你的账号补充多种状态的订单数据，可以查看订单、个人画像和推荐效果。",
            Type = "SeedOrders",
            LinkUrl = "/Orders/Index",
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync();
    }

    private static void AddSeedStatusHistory(Order order, string finalStatus, DateTime createdAt, string changedBy)
    {
        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = null,
            ToStatus = OrderStatuses.NewOrder,
            ChangedAt = createdAt,
            ChangedBy = changedBy
        });

        var currentStatus = OrderStatuses.NewOrder;
        foreach (var status in GetSeedStatusPath(finalStatus))
        {
            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = currentStatus,
                ToStatus = status,
                ChangedAt = createdAt.AddDays(order.StatusHistory.Count),
                ChangedBy = changedBy
            });
            currentStatus = status;
        }
    }

    private static IEnumerable<string> GetSeedStatusPath(string finalStatus)
    {
        if (finalStatus == OrderStatuses.Paid) return [OrderStatuses.Paid];
        if (finalStatus == OrderStatuses.Shipped) return [OrderStatuses.Paid, OrderStatuses.Shipped];
        if (finalStatus == OrderStatuses.Received) return [OrderStatuses.Paid, OrderStatuses.Shipped, OrderStatuses.Received];
        if (finalStatus == OrderStatuses.Completed) return [OrderStatuses.Paid, OrderStatuses.Shipped, OrderStatuses.Received, OrderStatuses.Completed];
        if (finalStatus == OrderStatuses.Cancelled) return [OrderStatuses.Cancelled];
        if (finalStatus == OrderStatuses.Refunded) return [OrderStatuses.Paid, OrderStatuses.Refunded];
        return [];
    }

    private static async Task BackfillMembershipAsync(ApplicationDbContext dbContext)
    {
        var memberStatuses = new[]
        {
            OrderStatuses.Paid,
            OrderStatuses.Shipped,
            OrderStatuses.Received,
            OrderStatuses.Completed
        };
        var users = await dbContext.Users.ToListAsync();
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => memberStatuses.Contains(order.Status))
            .ToListAsync();

        foreach (var user in users)
        {
            var userOrders = orders.Where(order => order.UserId == user.Id).ToList();
            var growth = (int)Math.Floor(userOrders.Sum(order => order.TotalAmount));
            if (growth <= user.MemberGrowth)
            {
                if (string.IsNullOrWhiteSpace(user.MemberLevel))
                {
                    user.MemberLevel = BuildSeedMemberLevel(user.MemberGrowth);
                }
                continue;
            }

            user.MemberGrowth = growth;
            user.MemberPoints = Math.Max(user.MemberPoints, growth);
            user.MemberLevel = BuildSeedMemberLevel(growth);
            user.MembershipStartedAt ??= userOrders.OrderBy(order => order.CreatedAt).FirstOrDefault()?.CreatedAt ?? user.CreatedAt;
        }

        await dbContext.SaveChangesAsync();
    }

    private static string BuildSeedMemberLevel(int growth)
    {
        if (growth >= 2000) return "黑钻会员";
        if (growth >= 1000) return "金卡会员";
        if (growth >= 300) return "银卡会员";
        return "普通会员";
    }

    private static List<ReviewSeed> BuildSeedReviews()
    {
        return
        [
            new("reader01", "C# 高性能开发", 5, "示例围绕性能瓶颈展开，适合已经写过 C# 项目的人查漏补缺。", 12),
            new("reader02", "ASP.NET Core Razor Pages 实战", 4, "和这个书店项目的技术栈很贴近，页面模型和表单处理讲得比较顺。", 24),
            new("reader03", "Python 自动化指南", 4, "脚本任务覆盖得比较完整，用来做文件整理和接口小工具很顺手。", 36),
            new("reader04", "Java 企业级开发", 3, "内容偏传统企业应用，基础扎实，但新框架部分可以再展开一点。", 48),
            new("reader05", "Go 微服务设计", 5, "服务拆分、接口边界和部署思路讲得清楚，适合做微服务入门后的第二本。", 60),
            new("reader01", "Rust 系统编程入门", 2, "所有权部分讲得太快，零基础读起来有压力，更适合有系统编程经验的人。", 72),
            new("reader02", "前端工程化实践", 4, "构建、规范和协作流程都覆盖到了，适合团队项目落地参考。", 84),
            new("reader03", "TypeScript 应用架构", 5, "类型设计和模块组织讲得很细，读完能明显改善大型前端项目结构。", 96),
            new("reader04", "算法与数据结构精选", 3, "题目选得不错，但部分解析略短，需要自己补充推导。", 108),
            new("reader05", "数据库查询优化", 5, "索引、执行计划和慢查询案例都很实用，后台系统开发很值得看。", 120),
            new("reader01", "机器学习实战导论", 4, "从概念到小案例的节奏比较稳，适合第一次做机器学习项目。", 132),
            new("reader02", "深度学习项目课", 3, "项目感不错，但对训练细节解释不够充分，最好配合官方文档看。", 144),
            new("reader03", "自然语言处理基础", 4, "分词、表示学习和任务流程讲得清楚，做 NLP 入门报告够用了。", 156),
            new("reader04", "推荐系统设计", 5, "召回、排序和冷启动都有覆盖，和书店的推荐场景也能对应起来。", 168),
            new("reader05", "生成式 AI 产品设计", 2, "产品视角有启发，但技术落地部分偏浅，期待更多真实案例。", 180),
            new("reader01", "大模型工程化指南", 4, "部署、监控和提示词工程都提到了，适合做大模型应用前的框架梳理。", 192),
            new("reader02", "夜航书简", 5, "文字安静克制，适合晚上慢慢读，书信感让情绪很容易沉进去。", 204),
            new("reader03", "岛屿来信", 4, "海岛和书信的氛围很完整，故事不急，但余味不错。", 216),
            new("reader04", "长街与旧梦", 3, "城市记忆写得细，节奏偏慢，喜欢快情节的人可能会觉得拖。", 228),
            new("reader05", "缓慢燃烧的夏天", 5, "夏天的潮湿感和人物关系写得很细腻，是文学类里很喜欢的一本。", 240),
            new("reader01", "冬夜旅馆", 2, "气氛有了，但人物动机不够清楚，读到后半段有些散。", 252),
            new("reader02", "增长策略手册", 4, "指标拆解和增长实验讲得实用，适合做运营复盘时翻。", 264),
            new("reader03", "产品经理实务", 5, "需求分析、原型和沟通部分都很接地气，新人产品经理很适合。", 276),
            new("reader04", "用户研究指南", 4, "访谈和问卷部分写得具体，能直接拿去设计调研计划。", 288),
            new("reader05", "项目管理节奏", 3, "节奏管理的观点不错，但案例不够多，读完还需要自己实践。", 300),
            new("reader01", "版式设计原理", 5, "网格、留白和层级讲得清楚，对页面排版很有帮助。", 312),
            new("reader02", "界面视觉系统", 4, "设计系统和组件一致性讲得好，适合前端和设计一起看。", 324),
            new("reader03", "字体与阅读体验", 5, "对字号、行距和阅读节奏的解释很细，做书店页面也能用上。", 336),
            new("reader04", "中国古代城市史", 4, "从城市格局看历史变迁，线索清楚，通识阅读很舒服。", 348),
            new("reader05", "数据库系统概论", 3, "知识点完整，教材感比较强，复习很合适但阅读趣味一般。", 360)
        ];
    }

    private static List<BookProduct> BuildSeedProducts()
    {
        var covers = new[]
        {
            "/images/books/cover-1.jpg",
            "/images/books/cover-2.jpg",
            "/images/books/cover-3.jpg",
            "/images/books/cover-4.jpg",
            "/images/books/cover-5.jpg",
            "/images/books/cover-6.jpg",
            "/images/books/cover-7.jpg",
            "/images/books/cover-8.jpg"
        };

        var series = new[]
        {
            new SeedSeries("编程开发", "程序设计出版社", new[]
            {
                "C# 高性能开发", "ASP.NET Core Razor Pages 实战", "Python 自动化指南", "Java 企业级开发", "Go 微服务设计",
                "Rust 系统编程入门", "前端工程化实践", "TypeScript 应用架构", "Node.js 服务端开发", "算法与数据结构精选",
                "Linux 运维与脚本", "云原生应用开发", "数据库查询优化", "软件测试方法论", "设计模式重构笔记"
            }),
            new SeedSeries("人工智能", "未来科技出版社", new[]
            {
                "机器学习实战导论", "深度学习项目课", "自然语言处理基础", "推荐系统设计", "计算机视觉应用",
                "强化学习简明课", "生成式 AI 产品设计", "知识图谱实践", "数据标注与模型评估", "大模型工程化指南",
                "AI Agent 开发范式", "智能搜索系统", "模型部署与推理优化", "多模态应用开发", "AI 产品经理手册"
            }),
            new SeedSeries("文学小说", "春山文艺出版社", new[]
            {
                "夜航书简", "岛屿来信", "长街与旧梦", "午后风景", "山海之间",
                "缓慢燃烧的夏天", "冬夜旅馆", "向海而居", "纸上花园", "远方的信箱",
                "沉默的灯塔", "落雪以前", "橙色黄昏", "城市边缘故事", "静水流深"
            }),
            new SeedSeries("商业管理", "现代管理出版社", new[]
            {
                "增长策略手册", "产品经理实务", "品牌方法论", "用户研究指南", "组织协作设计",
                "创业公司财务课", "运营增长案例", "项目管理节奏", "商业模型画布实践", "领导力沟通训练",
                "企业数字化转型", "零售经营分析", "内容营销策略", "决策分析与复盘", "供应链管理基础"
            }),
            new SeedSeries("设计创意", "视觉文化出版社", new[]
            {
                "版式设计原理", "界面视觉系统", "字体与阅读体验", "交互设计笔记", "品牌色彩应用",
                "设计调研方法", "信息图形表达", "网页排版实践", "创意文案写作", "产品摄影与构图",
                "服务设计工作坊", "移动端体验设计", "设计系统搭建", "视觉叙事表达", "可用性测试指南"
            }),
            new SeedSeries("历史人文", "知行人文社", new[]
            {
                "中国古代城市史", "近现代社会观察", "世界文明图谱", "历史现场笔记", "思想史入门",
                "阅读中国建筑", "丝路与海洋", "制度与变迁", "博物馆漫游指南", "城市文化地图",
                "经典文本导读", "人物传记选读", "考古发现故事", "地方志里的中国", "艺术史小史"
            }),
            new SeedSeries("教材教辅", "高校教材社", new[]
            {
                "数据库系统概论", "软件工程项目实践", "操作系统教程", "计算机网络基础", "离散数学导学",
                "高等数学要点", "概率统计习题精讲", "大学物理实验", "数字电路基础", "编译原理导读"
            })
        };

        var authors = new[]
        {
            "张三", "李四", "王五", "赵六", "周宁", "林岚", "许知远", "沈青", "顾言", "陈默",
            "苏禾", "何川", "徐朗", "秦墨", "韩笙", "唐果", "叶青山", "周以南", "程未", "陆清和"
        };

        var descriptions = new Dictionary<string, string>
        {
            // 编程开发
            ["C# 高性能开发"] = "本书从 C# 运行时底层原理出发，深入讲解了如何编写高性能代码。涵盖内存管理、JIT 编译优化、Span<T> 与 Memory<T> 的使用、数组与集合的高效操作、异步编程性能陷阱等核心主题，并结合实际项目场景给出优化方案。适合已有一定基础的 C# 开发者迈向高级阶段的实用指南。",
            ["ASP.NET Core Razor Pages 实战"] = "本书以一个完整的产品级书店应用贯穿始终，系统讲解了 Razor Pages 的页面组织、路由绑定、数据验证、组件化开发、与 Entity Framework Core 的集成，以及身份认证与授权等企业级功能。既适合入门学习，也适合作为项目参考。",
            ["Python 自动化指南"] = "从文件处理、Excel 操作、邮件自动化到网页爬虫，本书用大量实例展示了 Python 在日常办公自动化中的强大能力。每章均配以完整可运行的代码示例，读者可以直接将这些脚本应用到实际工作中，大幅提升日常效率。",
            ["Java 企业级开发"] = "本书围绕 Spring Boot 与 Spring Cloud 生态展开，讲解了构建企业级应用的核心要素：RESTful API 设计、数据持久化、缓存策略、服务拆分与容器化部署。配以详尽的配置说明和避坑指南，是 Java 开发者构建微服务架构的实战手册。",
            ["Go 微服务设计"] = "Go 语言以其简洁的语法和出色的并发性能成为微服务开发的热门选择。本书从服务拆分原则出发，讲解了 gRPC 通信、服务发现、容错处理、日志与链路追踪等关键主题，帮助读者快速掌握用 Go 构建生产级微服务的能力。",
            ["Rust 系统编程入门"] = "Rust 以其内存安全保证和零成本抽象吸引了越来越多系统程序员的关注。本书从 Rust 基本语法开始，逐步深入所有权系统、生命周期、 trait 与泛型，并配以实际系统编程案例，是进入 Rust 世界的有力起点。",
            ["前端工程化实践"] = "本书涵盖了现代前端工程化的完整链路：模块化打包（Vite/Webpack）、CSS 原子化方案、组件库设计、自动化测试、性能监控与 CI/CD 流水线。帮助前端团队建立规范化的开发流程，提升整体交付质量。",
            ["TypeScript 应用架构"] = "TypeScript 为大型前端项目提供了可靠的类型保障。本书从类型系统高级特性出发，讲解了如何在复杂业务场景下设计可维护的代码结构，以及与 React/Vue 等框架的最佳集成方式，是提升前端架构能力的进阶读物。",
            ["Node.js 服务端开发"] = "本书以 Express 和 Koa 框架为载体，详细讲解了 Node.js 服务端开发的方方面面：路由设计、中间件组织、会话管理、错误处理、安全防护以及与数据库的交互方式。适合想用 JavaScript 构建后端服务的开发者。",
            ["算法与数据结构精选"] = "本书从实际面试与竞赛中的高频考点出发，用清晰的图解和详尽的步骤拆解，帮助读者彻底理解数组、链表、树、图等核心结构，以及排序、搜索、动态规划等经典算法的思路与实现。",
            ["Linux 运维与脚本"] = "从 Shell 脚本基础到自动化运维工具链（Ansible/Nagios），本书提供了 Linux 服务器管理的全景图。包含日志分析、性能调优、备份恢复、安全加固等实用技能，是运维工程师和 DevOps 从业者的案头参考。",
            ["云原生应用开发"] = "云原生已成为现代软件开发的事实标准。本书讲解了容器（Docker）、编排（Kubernetes）、服务网格（Istio）、声明式基础设施以及 GitOps 工作流，帮助开发者将应用快速交付到云端并实现弹性伸缩。",
            ["数据库查询优化"] = "慢查询是大多数应用性能问题的根源。本书从 SQL 执行原理出发，讲解了索引设计、查询计划分析、连接策略优化，以及 NoSQL 与关系型数据库的选型思路，帮助开发者从根本上提升数据层的访问效率。",
            ["软件测试方法论"] = "本书系统梳理了从单元测试、集成测试到端到端测试的完整测试金字塔策略，涵盖了 TDD/BDD 主流方法、Mock 与测试数据准备策略，以及如何在 CI 流程中落地自动化测试，是提升代码质量的实践指南。",
            ["设计模式重构笔记"] = "本书以真实代码重构为线索，逐章讲解了 SOLID 原则和 GoF 设计模式的具体应用场景。通过 before/after 对比，帮助读者在日常开发中识别代码坏味道并用恰当的模式进行改造，培养良好的面向对象设计直觉。",

            // 人工智能
            ["机器学习实战导论"] = "机器学习正在深刻改变各行各业的技术格局。本书以 Python 为工具，从线性回归、逻辑回归、决策树等基础算法讲起，逐步深入支持向量机和神经网络，用真实数据集贯穿每个模型的训练、调参与部署全过程。",
            ["深度学习项目课"] = "本书以 Keras 和 PyTorch 双框架并行的方式，讲解了 CNN、RNN、注意力机制等核心模型在图像分类、文本生成、语音识别等场景中的实战应用。适合已有基础希望提升项目实战能力的读者。",
            ["自然语言处理基础"] = "NLP 是人工智能最具挑战也最具想象力的领域之一。本书从文本预处理、词向量表示、语言模型讲起，覆盖了情感分析、命名实体识别、文本生成等典型任务，讲解深入浅出，适合作为 NLP 入门与进阶的桥梁读物。",
            ["推荐系统设计"] = "推荐系统是内容平台的核心竞争力。本书系统讲解了协同过滤、内容推荐、深度学习推荐模型的原理与工程实现，以及 A/B 测试和推荐效果评估的实战方法，适合想做或正在做推荐相关工作的开发者。",
            ["计算机视觉应用"] = "从 OpenCV 基础到深度学习模型，本书覆盖了图像分类、目标检测、语义分割、人体姿态估计等主流视觉任务。每章配有完整的 Python 实现代码，帮助读者快速将视觉能力集成到自己的产品中。",
            ["强化学习简明课"] = "强化学习让智能体通过与环境交互学会决策。本书用通俗的语言讲解了马尔可夫决策过程、Q-learning、Policy Gradient 等核心算法，并用代码演示了如何在游戏、机器人控制等场景中训练智能体。",
            ["生成式 AI 产品设计"] = "大语言模型和扩散模型正在重新定义产品交互方式。本书分析了 GPT、Claude、Stable Diffusion 等主流模型的能力边界，并从产品视角讲解了如何设计 prompt、如何构建 RAG 系统、如何评估生成效果。",
            ["知识图谱实践"] = "知识图谱为 AI 系统提供了结构化的知识支撑。本书从知识抽取、实体链接、知识存储（图数据库 Neo4j）讲起，完整展示了构建和应用知识图谱的工程路径，适合搜索与问答系统相关开发者。",
            ["数据标注与模型评估"] = "好的数据比好的算法更重要。本书系统讲解了数据标注规范制定、标注工具选择、标注质量控制，以及 precision/recall/F1/AUC 等评估指标的深刻含义和实际使用场景。",
            ["大模型工程化指南"] = "大模型的训练和部署面临独特的工程挑战。本书从模型压缩、量化、推理加速讲起，涵盖 vLLM、TensorRT-LLM 等主流推理框架的使用，以及如何构建高并发、低延迟的大模型服务。",
            ["AI Agent 开发范式"] = "AI Agent 是大模型落地的重要方向。本书讲解了 ReAct、Plan-and-Execute 等 Agent 设计模式，工具调用（Function Calling）机制，以及如何使用 LangChain 和 AutoGPT 框架构建自主决策智能体。",
            ["智能搜索系统"] = "搜索是信息获取的核心入口。本书从倒排索引和 BM25 讲起，延伸到向量检索（Embedding）、语义搜索、搜索排序优化，以及如何利用大模型提升搜索结果的相关性。",
            ["模型部署与推理优化"] = "训练只是第一步，部署才是见真章的地方。本书覆盖了模型序列化、推理引擎选择（ONNX/TensorRT/TVM）、批处理策略、GPU 内存优化等部署全链路的关键技术点。",
            ["多模态应用开发"] = "GPT-4V 和 Gemini 让 AI 同时理解和生成图像与文本成为可能。本书讲解了多模态大模型的原理，以及如何开发基于图像理解、图文生成、视觉问答等能力的应用产品。",
            ["AI 产品经理手册"] = "AI 产品与传统互联网产品有本质不同。本书从 AI 能力边界评估、模型选型、数据准备、功能设计到用户反馈闭环，全面讲解了 AI 产品经理在工作中需要掌握的核心方法论。",

            // 文学小说
            ["夜航书简"] = "一位常年值夜班的图书管理员，在闭馆后的书架间写下一封封无法寄出的信。信里是对一位陌生读者的观察、猜想与怀念，关于一本书如何改变一个人，关于安静的陪伴如何成为另一种形式的深爱。",
            ["岛屿来信"] = "主人公意外继承了远方小岛上的一座旧书店，信件与书包裹着三代人的离散故事缓缓展开。岛屿、大海、旧书店、战争与和平，在缓慢的叙事中编织出一张关于阅读与记忆的网。",
            ["长街与旧梦"] = "一条即将被拆迁的老街，串起了几十户人家的悲欢离合。作者用细腻的笔触还原了街巷中消失的生活方式：巷口的早点摊、邻里的闲话、逢年过节的仪式，以及无法带走的乡愁。",
            ["午后风景"] = "在一个平常的午后，一个女人遇见了一只会说话的黑猫。它陪伴她度过了整整一个夏天，说着关于时间、记忆和选择的谜语。故事在一个意外的午后戛然而止，留给读者无尽的回味。",
            ["山海之间"] = "两个因地质考察而相遇的年轻人，用十年时间踏遍了西北的高山与荒漠。书里记录了他们对自然的敬畏、对彼此的依恋，以及人在天地间的渺小与坚韧。",
            ["缓慢燃烧的夏天"] = "高考前最后一个暑假，几个少年在小镇的夏天里慢慢燃烧着他们的友谊与迷茫。爱情、离别、梦想与现实，在炙热的阳光下缓缓浮现，成为每个人记忆中最漫长的季节。",
            ["冬夜旅馆"] = "一座深山里的老旅馆，冬季大雪封山后与外界隔绝。住客们各怀心事，在漫长的冬夜里轮流讲述自己的故事。悬疑与温情并存，是一部关于人性幽微的群像小说。",
            ["向海而居"] = "一对情侣离开城市，在海边小村租下一个小院定居。日子过得缓慢而充实，养狗、种菜、修缮老屋。但生活的真相总是比想象复杂，关于选择、关于留下与离开的故事缓缓流淌。",
            ["纸上花园"] = "一位年迈的植物学教授，在生命的最后几年用文字建造了一座花园。每一种植物背后都有一段私密的记忆，植物与故事交织在一起，形成了一部关于时间、生命与爱的独特叙事。",
            ["远方的信箱"] = "在电子邮件还没有普及的年代，两个陌生人因为一个写错地址的信封开始了长达二十年的书信往来。书信内容从最初的寒暄渐渐深入，最终成为彼此生命中最诚实的一面镜子。",
            ["沉默的灯塔"] = "一个被遗忘的灯塔守望者，在孤岛上度过了大半生。小说从他退休离开海岛那天开始，用闪回的方式展开他一生的故事：战争、爱情、失去，以及那片大海如何塑造了他。",
            ["落雪以前"] = "故事发生在一座北方小城的冬天，一个女人在整理亡夫遗物时发现了一封从未寄出的信。雪一层层覆盖了往事，但真相总会随着融雪浮出水面。冷冽而克制，却后劲十足。",
            ["橙色黄昏"] = "一位退休的平面设计师，开始用画笔记录城市里即将消失的角落。街道、屋顶、小摊、路灯——在他的笔下，日常风景获得了新的生命，也让他重新理解了自己走过的这一生。",
            ["城市边缘故事"] = "城中村、批发市场、老工厂——城市的边缘地带总是藏着最多元、最真实的生活。本书用九个短篇故事，讲述了那些被主流叙事忽略的城市生命力的迸发与挣扎。",
            ["静水流深"] = "一个三代同堂的家庭，表面平静的生活下暗流涌动。作者用克制的笔触描写了代际之间的差异与理解、误解与和解，以及血缘之爱那无法言说的复杂与深沉。",

            // 商业管理
            ["增长策略手册"] = "增长不是一句口号，而是一套可学习的方法论。本书系统梳理了从用户获取、激活、留存到变现、推荐的完整增长漏斗，辅以拼多多、字节跳动等国内外案例，深入讲解了增长实验设计与数据分析的实战技巧。",
            ["产品经理实务"] = "产品经理是离产品最近的人，也是一切决策落地的支点。本书从需求发现、PRD 写作、项目管理、数据验证讲到与研发、设计、运营的高效协作，覆盖了产品经理日常工作的全场景。",
            ["品牌方法论"] = "品牌不是 Logo 和口号，而是用户心中对产品感受的总和。本书从品牌定位理论出发，讲解了品牌命名、视觉识别、品牌叙事，以及在社交媒体时代如何让品牌保持一致性又具有灵活性。",
            ["用户研究指南"] = "不了解用户，就像在黑暗中射箭。本书系统讲解了用户访谈、问卷调查、可用性测试、A/B 测试等研究方法的实操细节，帮助产品团队建立起以用户为中心的决策文化。",
            ["组织协作设计"] = "好产品背后是好团队。本书从组织架构设计、跨部门协作机制、会议与决策流程、会议革命等角度出发，讲解了如何打造一个高效协作、持续创新的工作环境。",
            ["创业公司财务课"] = "创业公司死亡的第一原因往往是现金流断裂。本书用通俗的语言讲解了如何读懂三张财务报表、如何做财务预测、如何控制成本，以及何时该融资、如何与投资人打交道。",
            ["运营增长案例"] = "本书精选了十三个国内互联网公司的增长运营案例，涵盖电商、社交、内容、出行等领域。每个案例均从业务背景、核心策略、执行细节到效果评估进行完整复盘。",
            ["项目管理节奏"] = "项目延期和范围蔓延是所有团队都会遇到的挑战。本书讲解了敏捷和看板的实战应用、站会与复盘的正确姿势、风险管理方法，以及如何让团队在压力下保持节奏感。",
            ["商业模型画布实践"] = "商业模型画布是整理商业思路的利器。本书不仅讲解了画布的九个构成要素，还通过大量练习帮助读者对自己的业务进行系统化梳理，找到商业模式中的漏洞与机会。",
            ["领导力沟通训练"] = "Leadership 不是职位赋予的，而是行为带来的。本书从非暴力沟通、教练式提问、冲突调解、向上管理四个维度出发，配以大量情景对话练习，帮助职场人提升影响力。",
            ["企业数字化转型"] = "数字化转型不是上一套系统，而是用数字技术重构业务逻辑。本书从战略规划、技术选型、组织文化变革、项目落地等维度全面讲解了企业数字化转型的完整路径与避坑指南。",
            ["零售经营分析"] = "新零售时代，线上线下融合已成标配。本书从选址逻辑、货架陈列、定价策略、会员体系讲到数字化运营，帮助传统零售从业者理解数据驱动决策的落地方法。",
            ["内容营销策略"] = "好内容是最持久最便宜的增长引擎。本书从内容定位、选题策划、平台选择、内容生产工业化讲到 SEO 与私域流量运营，是内容营销从业者的实战手册。",
            ["决策分析与复盘"] = "没有复盘的学习是不完整的。本书系统讲解了 PEST、SWOT、波特五力等战略分析工具的使用，以及 AAR（行动后复盘）方法在团队决策改进中的应用。",
            ["供应链管理基础"] = "供应链是企业竞争力最隐蔽也最关键的维度之一。本书从供应商管理、库存策略、物流网络讲到供应链风险控制，用大量制造业与电商案例帮助读者理解供应链优化的本质。",

            // 设计创意
            ["版式设计原理"] = "版式设计决定了信息传递的效率与美感。本书从网格系统、视觉层次、信息分组讲起，辅以大量期刊、书籍、网页版式案例，帮助读者建立起系统的版式设计思维。",
            ["界面视觉系统"] = "一套好的视觉系统是产品设计一致性的保障。本书讲解了色彩体系、字体规范、图标系统、组件状态等原子化设计要素，以及如何构建和维护一套可扩展的设计系统。",
            ["字体与阅读体验"] = "字体是设计中最低调也最关键的要素之一。本书从字体分类、字重选择、行距段距讲到中英文排版混排的注意事项，帮助设计师在文字层面提升作品的阅读品质。",
            ["交互设计笔记"] = "交互设计的本质是理解人的行为和预期。本书从用户心智模型、交互范式、手势与反馈讲起，覆盖了 web、移动端、可穿戴设备等多种场景的交互设计原则。",
            ["品牌色彩应用"] = "色彩是品牌识别中最快速传达情绪的元素。本书讲解了色彩心理学、配色方案制定、品牌色在多场景中的应用，以及如何建立一套既有个性又具适应性的品牌色彩系统。",
            ["设计调研方法"] = "设计始于理解，而非灵感。本书系统梳理了设计调研的常用方法：竞品分析、用户访谈、情境调查、设计民族志，以及如何将调研结果转化为设计洞察。",
            ["信息图形表达"] = "数据可视化是让复杂信息一目了然的有效手段。本书从图表类型选择、视觉编码原则讲起，涵盖了折线图、饼图、热力图等常用图形的正确使用方式，以及如何避免误导性可视化。",
            ["网页排版实践"] = "网页排版有纸张不曾面对的挑战：多终端适配、字体回退、阅读模式与暗黑模式。本书从 CSS 排版核心属性出发，讲解了现代网页排版的完整技术方案。",
            ["创意文案写作"] = "文案是设计作品的最后一道包装，也是用户接触品牌的第一印象。本书从文案定位、结构组织、长短文案写作技巧讲到品牌文风塑造，是文案创作者和设计师的必读参考。",
            ["产品摄影与构图"] = "电商平台上，产品图是转化率最直接的影响因素。本书讲解了相机与灯光基础、构图法则、产品布光技巧，以及如何通过后期处理让产品图片兼具真实感与吸引力。",
            ["服务设计工作坊"] = "服务设计以用户旅程为主线，系统性改善服务体验。本书用完整的 workshop 形式，手把手教读者从用户旅程图、利益相关者地图到服务蓝图的设计全过程。",
            ["移动端体验设计"] = "移动端的屏幕约束带来独特的设计挑战。本书从手势交互、导航模式、一手操作适配讲起，涵盖 iOS 和 Android 平台设计规范的异同与融合。",
            ["设计系统搭建"] = "设计系统是设计团队协作的基础设施，也是设计质量一致的保障。本书从设计原则、组件规范、文档编写讲到设计系统的推广与演进，提供了完整的搭建方法论。",
            ["视觉叙事表达"] = "人是故事的动物，视觉同样可以讲故事。本书讲解了如何在品牌传播、广告创意、社交媒体内容中构建视觉叙事，以及如何通过图像序列传递情绪和信息。",
            ["可用性测试指南"] = "可用性测试是验证设计是否有效的最直接手段。本书从测试计划制定、任务设计、测试主持技巧讲起，覆盖了远程测试与眼动追踪等进阶方法。",

            // 历史人文
            ["中国古代城市史"] = "长安、洛阳、开封、宋应昌——中国古代城市不只是砖瓦与城墙，更是政治权力、经济发展与文化交融的集中体现。本书选取十座代表性城市，讲述了它们从规划建设到兴衰演变的故事。",
            ["近现代社会观察"] = "近代中国经历了三千年未有之大变局。本书从社会结构变迁、底层民众生活、新旧思想碰撞等视角出发，用具体人物和事件串联起近代化进程中的众生相。",
            ["世界文明图谱"] = "从两河流域的楔形文字到安第斯高原的印加帝国，本书用横向比较的视角审视了不同文明的兴起、发展和相互影响，帮助读者在大历史框架下理解人类社会的多样性。",
            ["历史现场笔记"] = "历史不只是书本上的年代和事件，更是发生在真实空间中的故事。本书作者走访了国内外数十处历史遗址，在现场与史料之间架起桥梁，让历史变得可触可感。",
            ["思想史入门"] = "从轴心时代百家争鸣，到宋明理学，再到近代启蒙思想，中国思想史是一条绵延两千年的河流。本书选取关键节点和代表人物，勾勒出思想演变的清晰脉络。",
            ["阅读中国建筑"] = "建筑是凝固的历史。本书从城墙、宫殿、寺庙、园林到民居，讲解了中国传统建筑的空间布局、结构和审美，以及背后承载的礼制观念与生活方式。",
            ["丝路与海洋"] = "丝绸之路不只是东西方贸易的通道，更是文明交流的桥梁。本书重新审视了路上丝绸之路与海上丝绸之路的历史，以及它们在当代「一带一路」倡议下的新意义。",
            ["制度与变迁"] = "从井田制到科举制，从郡县制到行省制，中国古代国家制度的演进深刻影响了社会面貌。本书选取十个关键制度，讲述其起源、运作逻辑与历史影响。",
            ["博物馆漫游指南"] = "博物馆是了解一个城市和文明的最佳入口。本书精选国内外二十余座博物馆，从馆藏特色、展览逻辑、参观策略讲到如何解读文物背后的历史信息。",
            ["城市文化地图"] = "每座城市都有自己独特的气质和记忆。本书通过城市空间、街道命名、方言俚语、饮食习俗等维度，解读了北京、上海、成都、厦门等城市的文化密码。",
            ["经典文本导读"] = "《史记》《资治通鉴》《诗经》《楚辞》——这些经典之所以流传千年，是因为它们持续提供着理解人性和社会的洞见。本书选取十部经典，讲解其核心价值与阅读路径。",
            ["人物传记选读"] = "历史的走向往往由少数关键人物的选择所决定。本书选取十位在中国历史上有重要影响的人物（帝王、士人、商人、科学家各类型），讲述他们的人生与时代。",
            ["考古发现故事"] = "考古不只是学术工作，更是挖掘和拼凑人类过去的侦探故事。本书讲述了三星堆、兵马俑、海昏侯墓等重大考古发现的发现过程与重要收获。",
            ["地方志里的中国"] = "中国地域辽阔，各地风土人情差异巨大。本书借助地方志这一独特文献资源，展现了不同地区的历史记忆、民俗文化与社会变迁。",
            ["艺术史小史"] = "艺术是文明最直观的精神表达。本书从彩陶、青铜、壁画、书法到文人画、工笔画，系统梳理了中国艺术风格演变的关键节点及其与社会背景的关联。",

            // 教材教辅
            ["数据库系统概论"] = "数据库是几乎所有信息系统的基础。本书系统讲解了关系模型、SQL 查询、数据库设计范式、事务与并发控制、索引原理等核心知识，配有大量习题和实验，是计算机专业学生的经典教材。",
            ["软件工程项目实践"] = "软件工程不是写代码，而是将软件开发变成一项可组织、可管理、可预期的活动。本书从需求分析、架构设计、编码规范、测试策略讲到项目管理方法，配有完整项目案例。",
            ["操作系统教程"] = "操作系统是计算机系统的核心软件。本书从进程与线程、内存管理、文件系统、设备驱动讲起，系统讲解了操作系统的基本原理与设计思想，是理解计算机系统的重要基础。",
            ["计算机网络基础"] = "互联网已经改变了我们生活的方方面面，理解网络原理是每个 IT 从业者的必修课。本书从物理层到应用层逐层讲解，配有详尽的协议分析和实验指导。",
            ["离散数学导学"] = "离散数学是计算机科学的数学语言，涵盖集合论、图论、布尔代数、排列组合等主题。本书用大量例子和习题帮助学生建立离散思维，为后续算法学习打下坚实基础。",
            ["高等数学要点"] = "微积分是理工科学生的重要基础。本书不追求证明的完整性，而是着重讲解概念的实际含义和应用方法，配以几何直观和大量工程应用案例，让抽象的数学变得可理解。",
            ["概率统计习题精讲"] = "概率统计是数据分析和机器学习的数学基础。本书精选了教材中的典型习题，给予详细的思路分析和解答，帮助学生建立概率思维和统计分析能力。",
            ["大学物理实验"] = "物理实验是训练科学思维和实验技能的重要环节。本书涵盖了力学、热学、电磁学、光学等领域的十二个基础实验，详细讲解了实验原理、数据处理和误差分析方法。",
            ["数字电路基础"] = "数字电路是计算机硬件的基石。本书从布尔代数、逻辑门、组合逻辑电路、时序逻辑电路讲起，系统讲解了数字系统的工作原理，配有丰富的电路分析与设计练习。",
            ["编译原理导读"] = "编译器让人类可以用高级语言编程，是计算机科学最伟大的发明之一。本书从词法分析、语法分析、语义分析讲到代码生成与优化，用简单语言讲解编译器的核心工作原理。"
        };

        var products = new List<BookProduct>();
        var index = 0;

        foreach (var seriesItem in series)
        {
            foreach (var title in seriesItem.Titles)
            {
                products.Add(new BookProduct
                {
                    Title = title,
                    Author = authors[index % authors.Length],
                    Publisher = seriesItem.Publisher,
                    Category = seriesItem.Category,
                    Price = 38 + (index % 9) * 6 + (index % 3) * 0.8m,
                    Stock = 12 + (index % 35),
                    Description = descriptions.TryGetValue(title, out var desc) ? desc : string.Empty,
                    CoverUrl = covers[index % covers.Length],
                    CreatedAt = DateTime.UtcNow.AddMinutes(-index),
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-index)
                });

                index++;
            }
        }

        return products;
    }

    private sealed record ReviewerSeed(string UserName, string DisplayName);

    private sealed record ReviewSeed(string UserName, string Title, int Rating, string Content, int MinutesAgo);

    private sealed record QuestionSeed(string UserName, string Title, string Question, string Answer, string AnsweredBy, int MinutesAgo);

    private sealed record SeedSeries(string Category, string Publisher, string[] Titles);

    private sealed record LjcOrderSeed(
        string Status,
        DateTime CreatedAt,
        DateTime? RefundRequestedAt,
        DateTime? RefundReviewedAt,
        string RefundReason,
        decimal DiscountAmount,
        string CouponName,
        LjcOrderItemSeed[] Items);

    private sealed record LjcOrderItemSeed(string Title, int Quantity);
}

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
        await EnsureCouponTablesAsync(dbContext);
        await EnsureNotificationTableAsync(dbContext);
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
        await SeedCouponsAsync(dbContext);
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
            ["编程开发"] = "围绕真实开发任务展开，适合课堂项目、练习题和独立实现时反复查阅。",
            ["人工智能"] = "从基础概念到落地案例都有覆盖，适合做课程展示、阅读报告和应用设计。",
            ["文学小说"] = "节奏舒缓、文字完整，适合在连续阅读中进入故事氛围。",
            ["商业管理"] = "聚焦策略、组织和增长实践，适合项目汇报与案例分析场景。",
            ["设计创意"] = "兼顾视觉表达与产品思路，适合作为界面、品牌与创意练习参考。",
            ["历史人文"] = "用清晰叙述组织知识线索，适合拓展阅读与通识学习。",
            ["教材教辅"] = "按课程重点梳理知识结构，适合课堂学习、复习和作业准备。"
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
                    Description = descriptions[seriesItem.Category],
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

    private sealed record SeedSeries(string Category, string Publisher, string[] Titles);
}

using System.Text;

var covers = new[]
{
    "https://images.unsplash.com/photo-1512820790803-83ca734da794?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1521587760476-6c12a4b040da?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1516979187457-637abb4f9353?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1481627834876-b7833e8f5570?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1507842217343-583bb7270b66?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1455885666463-9f41fe9b13d4?auto=format&fit=crop&w=900&q=80",
    "https://images.unsplash.com/photo-1526243741027-444d633d7365?auto=format&fit=crop&w=900&q=80"
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

static string Esc(string value) => value.Replace("\\", "\\\\").Replace("'", "''");

var sb = new StringBuilder();
sb.AppendLine("SET NAMES utf8mb4;");
sb.AppendLine("USE online_bookshop;");
var id = 1;
var index = 0;
foreach (var seriesItem in series)
{
    foreach (var title in seriesItem.Titles)
    {
        var author = authors[index % authors.Length];
        var price = 38 + (index % 9) * 6 + (index % 3) * 0.8m;
        var stock = 12 + (index % 35);
        var description = descriptions[seriesItem.Category];
        var cover = covers[index % covers.Length];
        sb.AppendLine($"UPDATE Products SET Title='{Esc(title)}', Author='{Esc(author)}', Publisher='{Esc(seriesItem.Publisher)}', Price={price:0.00}, Stock={stock}, Category='{Esc(seriesItem.Category)}', Description='{Esc(description)}', CoverUrl='{Esc(cover)}', UpdatedAt=UTC_TIMESTAMP() WHERE Id={id};");
        id++;
        index++;
    }
}
sb.AppendLine("SELECT Id, Title, Category FROM Products ORDER BY Id LIMIT 5;");
Console.OutputEncoding = new UTF8Encoding(false);
Console.Write(sb.ToString());

file sealed record SeedSeries(string Category, string Publisher, string[] Titles);

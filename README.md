# BookStoreSample 线上书房

BookStoreSample 是一个基于 ASP.NET Core Razor Pages 的在线书店示例项目。项目围绕图书浏览、购物车、下单、订单流转、优惠券、会员、评价、读者问答、通知中心和后台管理等业务流程展开，适合作为 ASP.NET Core + EF Core + SQLite 的课程项目、练习项目或电商业务样例。

## 技术栈

- .NET 8
- ASP.NET Core Razor Pages
- ASP.NET Core Identity
- Entity Framework Core 8
- SQLite
- 原生 HTML/CSS/JavaScript

## 项目特点

- 前台用户侧覆盖完整购书流程：浏览图书、收藏、加入购物袋、凑单、领券、结算、下单、支付、确认收货、退款、评价、追评、晒图。
- 个人中心包含订单、收藏、优惠券、地址、最近浏览、会员成长、数据图表和头像上传。
- 后台管理包含图书管理、订单管理、优惠券管理、评价管理、库存流水和经营看板。
- 内置本地启发式 AI 选书助手，可根据用户提问和历史偏好推荐图书。
- 启动时自动初始化 SQLite 数据库、角色、管理员账号、种子图书、评论、问答、优惠券和演示订单。
- 针对 SQLite 的 decimal 排序/聚合限制做了兼容处理，尽量减少手动删库或迁移操作。

## 快速开始

### 环境要求

- 安装 .NET 8 SDK
- Windows、macOS 或 Linux 均可运行

### 运行项目

在仓库根目录执行：

```powershell
dotnet run --project BookStoreSample\BookStoreSample.csproj --urls http://localhost:59432
```

浏览器打开：

```text
http://localhost:59432
```

### 编译检查

```powershell
dotnet build BookStoreSample\BookStoreSample.csproj
```

## 默认账号

项目启动时会自动创建以下演示账号：

| 角色 | 用户名 | 密码 | 说明 |
| --- | --- | --- | --- |
| 管理员 | `admin` | `admin123` | 可进入后台管理 |
| 普通用户 | `ljc` | `ljc123` | 带有演示订单和会员数据 |
| 普通读者 | `reader01` / `reader02` 等 | `reader123` | 用于评论和问答演示 |

也可以通过注册页自行创建普通用户。

## 数据库

SQLite 数据库文件位于：

```text
BookStoreSample/Data/bookstore.db
```

程序启动时会执行 `SeedData.InitializeAsync`，自动完成：

- 创建数据库和基础表。
- 创建 Identity 角色和默认管理员。
- 补齐新增业务字段，例如物流、优惠券、退款、通知、会员、头像、追评和晒图字段。
- 插入种子图书、评论、问答、优惠券和演示订单。

## 目录结构

```text
BookStoreSample/
├─ Data/                 # EF Core DbContext 与种子数据
├─ Models/               # 业务实体模型
├─ Pages/                # Razor Pages 页面
│  ├─ Account/           # 登录、注册、个人中心、地址、优惠券、会员等
│  ├─ Admin/             # 后台看板、商品、优惠券、评价、库存管理
│  ├─ Cart/              # 购物袋与结算
│  ├─ Orders/            # 订单列表与订单详情
│  └─ Products/          # 图书列表、详情、AI 选书助手
├─ Security/             # 用户 Claims 扩展
├─ Services/             # StoreService 业务服务
└─ wwwroot/              # 静态资源与上传文件目录
```

## 核心功能概览

- 首页：精选图书、新书、热销图书和分类入口。
- 图书列表：搜索、筛选、排序、猜你喜欢、优惠券入口、AI 选书助手。
- 图书详情：购买、收藏、读者问答、评价、追评、晒图、同类推荐、最近浏览。
- 购物袋：数量修改、移除商品、凑单助手、一键加入凑单图书。
- 结算与订单：地址选择、优惠券抵扣、库存校验、订单创建。
- 订单流转：支付、发货、确认收货、取消、退款申请、退款审核、物流信息。
- 个人中心：头像、资料、会员卡、订单摘要、优惠券、收藏、地址、最近浏览、消费图表。
- 会员中心：积分、成长值、等级、升级进度、权益和积分到账记录。
- 通知中心：支付、发货、收货、评价、追评、退款、库存、会员等通知。
- 后台管理：商品、订单、优惠券、评价、库存流水和经营数据图表。

更完整的功能说明见：[BookStoreSample功能介绍.md](BookStoreSample功能介绍.md)。

## 上传文件

项目支持以下上传内容：

- 图书封面：后台商品管理上传。
- 评价晒图：图书详情评价时上传。
- 用户头像：编辑个人资料时上传。

上传文件会保存到 `wwwroot/uploads` 下的不同子目录。

## 说明

本项目的 AI 选书助手是本地启发式推荐，并未接入外部大模型 API。它会根据用户输入关键词、图书类别、销量、评分、库存和用户历史偏好进行推荐。


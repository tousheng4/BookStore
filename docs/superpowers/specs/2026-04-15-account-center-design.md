# 个人中心模块设计

## 概述

在 Account 模块下新增个人中心 Hub 页面（`/Account/Index`），作为用户个人信息的汇总入口。同时调整导航结构，将地址和订单从直接可访问改为通过个人中心进入。

## 页面结构

### 新增页面

| 页面 | 路由 | 说明 |
|------|------|------|
| 个人中心 Hub | `/Account/Index` | 展示用户信息、地址/收藏/订单摘要 |
| 个人中心 PageModel | `Pages/Account/Index.cshtml.cs` | 提供 OnGet 数据加载 |

### 保留页面（详情页）

| 页面 | 路由 |
|------|------|
| 地址列表 | `/Account/Addresses/Index` |
| 地址编辑 | `/Account/Addresses/Edit` |
| 收藏列表 | `/Account/Wishlist` |
| 订单列表 | `/Orders/Index` |
| 订单详情 | `/Orders/Details` |

## 导航变更

### 顶部导航 (`_Layout.cshtml`)

**移除：**
- `地址` → `asp-page="/Account/Addresses/Index"`
- `订单` → `asp-page="/Orders/Index"`

**新增：**
- `个人中心` → `asp-page="/Account/Index"`

### Footer 链接

同步移除"收货地址"和"订单中心"的直链，改为指向个人中心。

## 个人中心 Hub 页面设计

### 布局：两栏式

- **左侧**：用户信息卡片
  - DisplayName（用户名）
  - Email
  - 角色（Badge 形式）
  - 注册时间

- **右侧**：三个信息概览卡片
  - **收货地址** — 显示地址数量 + 默认地址摘要 + "管理地址"按钮
  - **我的收藏** — 显示收藏数量 + 最多3本图书封面缩略 + "查看收藏"按钮
  - **我的订单** — 显示订单数量 + 最近1条订单状态 + "查看全部"按钮

### 数据加载（OnGet）

在一次请求中加载：
1. `ApplicationUser` 基本信息（通过 UserManager）
2. `ShippingAddress` 列表（含默认地址）
3. `WishlistItem` 列表（含关联 Product）
4. `Order` 列表（最近5条，含状态）

### 路由

```
/Account/Index                 → 个人中心 Hub
/Account/Addresses/Index       → 地址列表详情（保留）
/Account/Addresses/Edit        → 地址编辑（保留）
/Account/Wishlist              → 收藏详情（保留）
/Orders/Index                  → 订单列表（保留）
/Orders/Details                → 订单详情（保留）
```

## 技术要点

- `Index.cshtml.cs` 使用 Primary Constructor，注入 `StoreService` 和 `UserManager<ApplicationUser>`
- `OnGet` 异步加载所有数据，避免 N+1 查询
- 现有详情页无需任何改动
- 所有现有页面保持 `[Authorize]` 保护

## 文件变更清单

| 操作 | 文件 |
|------|------|
| 新增 | `Pages/Account/Index.cshtml` |
| 新增 | `Pages/Account/Index.cshtml.cs` |
| 修改 | `Pages/Shared/_Layout.cshtml`（导航和 Footer） |

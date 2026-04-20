# BookStoreSample 数据库表说明

本文档说明 BookStoreSample 当前 SQLite 数据库中的主要数据表、每张表的用途、字段含义和表之间的关系。

数据库文件默认位于：

```text
BookStoreSample/Data/bookstore.db
```

项目使用 ASP.NET Core Identity，因此数据库中既包含书店业务表，也包含 Identity 认证授权相关表。

## 1. 表总览

### 业务表

| 表名 | 用途 |
| --- | --- |
| `Products` | 图书商品表，保存图书基础信息、价格、库存、销量、上下架状态 |
| `CartItems` | 购物袋明细表，保存用户加入购物袋的图书和数量 |
| `Orders` | 订单主表，保存订单收货信息、状态、金额、物流、优惠券和退款信息 |
| `OrderItems` | 订单明细表，保存订单中的每一本图书快照 |
| `OrderStatusHistory` | 订单状态历史表，保存订单状态流转记录 |
| `ShippingAddresses` | 收货地址表，保存用户维护的配送地址 |
| `WishlistItems` | 收藏表，保存用户收藏的图书 |
| `BookReviews` | 图书评价表，保存评分、评价内容、晒图、追评 |
| `BookQuestions` | 读者问答表，保存图书详情页提问和回答 |
| `Coupons` | 优惠券表，保存优惠券规则和有效期 |
| `UserCoupons` | 用户优惠券表，保存用户领取、使用优惠券的记录 |
| `UserNotifications` | 用户通知表，保存订单、退款、评价、会员等通知 |
| `InventoryChangeLogs` | 库存流水表，记录库存变化来源 |

### Identity 表

| 表名 | 用途 |
| --- | --- |
| `AspNetUsers` | 用户表，保存登录账号、资料、头像和会员信息 |
| `AspNetRoles` | 角色表，保存管理员、普通用户等角色 |
| `AspNetUserRoles` | 用户角色关联表 |
| `AspNetUserClaims` | 用户 Claim 表 |
| `AspNetRoleClaims` | 角色 Claim 表 |
| `AspNetUserLogins` | 第三方登录表 |
| `AspNetUserTokens` | 用户 Token 表 |

## 2. 业务表说明

## `Products` 图书商品表

保存书店中的图书商品信息。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 图书唯一编号 |
| `Title` | string，最大 120 | 图书名称 |
| `Author` | string，最大 60 | 作者 |
| `Publisher` | string，最大 80 | 出版社 |
| `Price` | decimal(10,2) | 图书售价 |
| `Stock` | int | 当前库存 |
| `Category` | string，最大 40 | 图书分类 |
| `Description` | string | 图书详情描述 |
| `CoverUrl` | string，最大 500 | 封面图片地址，可为外链或上传后的本地路径 |
| `CreatedAt` | DateTime | 创建时间 / 上架时间 |
| `UpdatedAt` | DateTime | 最后更新时间 |
| `SalesCount` | int | 销量统计，用于热销排序和推荐 |
| `IsActive` | bool，默认 true | 是否上架。下架图书不在前台列表和推荐中展示，也不能购买 |

主要关系：

- 一本图书可对应多条购物袋记录：`Products.Id` -> `CartItems.ProductId`
- 一本图书可对应多条订单明细：`Products.Id` -> `OrderItems.ProductId`
- 一本图书可被多个用户收藏：`Products.Id` -> `WishlistItems.ProductId`
- 一本图书可拥有多条评价：`Products.Id` -> `BookReviews.ProductId`
- 一本图书可拥有多条问答：`Products.Id` -> `BookQuestions.ProductId`

## `CartItems` 购物袋明细表

保存用户购物袋中的商品。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 购物袋记录编号 |
| `UserId` | string，外键 | 所属用户，对应 `AspNetUsers.Id` |
| `ProductId` | int，外键 | 图书编号，对应 `Products.Id` |
| `Quantity` | int | 加入购物袋的数量 |
| `AddedAt` | DateTime | 加入购物袋时间 |

约束：

- `UserId + ProductId` 唯一，同一用户同一本书只保留一条购物袋记录。

主要关系：

- 多条购物袋记录属于一个用户。
- 多条购物袋记录指向不同图书。

## `Orders` 订单主表

保存订单的主信息，包括收货、状态、金额、物流、优惠券和退款信息。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 订单编号 |
| `UserId` | string，外键 | 下单用户，对应 `AspNetUsers.Id` |
| `ReceiverName` | string，最大 50 | 收货人姓名 |
| `ReceiverPhone` | string，最大 30 | 收货人电话 |
| `ShippingAddress` | string，最大 300 | 收货地址完整文本快照 |
| `Status` | string，最大 30 | 订单状态，例如新订单、已支付、已发货、已收货、已完成、已取消、已退款 |
| `CreatedAt` | DateTime | 下单时间 |
| `TotalAmount` | decimal(10,2) | 订单最终金额，已扣除优惠券 |
| `TrackingCompany` | string，最大 80 | 物流公司 |
| `TrackingNumber` | string，最大 80 | 物流单号 |
| `ShippedAt` | DateTime? | 发货时间 |
| `CouponName` | string，最大 80 | 使用的优惠券名称快照 |
| `DiscountAmount` | decimal(10,2) | 优惠券抵扣金额 |
| `RefundRequestedAt` | DateTime? | 用户申请退款时间 |
| `RefundReason` | string，最大 500 | 用户填写的退款原因 |
| `RefundReviewedAt` | DateTime? | 管理员审核退款时间 |
| `RefundReviewedBy` | string，最大 100 | 审核退款的管理员标识 |
| `RefundReviewNote` | string，最大 500 | 管理员退款审核备注 |

主要关系：

- 一个用户可以有多笔订单：`AspNetUsers.Id` -> `Orders.UserId`
- 一笔订单可以有多条明细：`Orders.Id` -> `OrderItems.OrderId`
- 一笔订单可以有多条状态历史：`Orders.Id` -> `OrderStatusHistory.OrderId`
- 一笔订单可被一张用户优惠券关联：`Orders.Id` -> `UserCoupons.OrderId`
- 库存流水可关联订单：`Orders.Id` -> `InventoryChangeLogs.OrderId`

## `OrderItems` 订单明细表

保存订单中的图书明细。它保存的是下单时的商品快照，即使后续图书信息修改，订单明细仍保留下单时的信息。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 订单明细编号 |
| `OrderId` | int，外键 | 所属订单，对应 `Orders.Id` |
| `ProductId` | int，外键 | 对应图书，对应 `Products.Id` |
| `Title` | string，最大 120 | 下单时的图书名称快照 |
| `UnitPrice` | decimal(10,2) | 下单时单价 |
| `Quantity` | int | 购买数量 |
| `Author` | string，最大 60 | 下单时作者快照 |
| `CoverUrl` | string，最大 500 | 下单时封面快照 |

派生含义：

- 行小计通常由 `UnitPrice * Quantity` 计算得到。

## `OrderStatusHistory` 订单状态历史表

记录订单状态每一次变化，便于在订单详情中展示流转轨迹。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 状态历史编号 |
| `OrderId` | int，外键 | 所属订单 |
| `FromStatus` | string?，最大 30 | 变更前状态。创建订单时可为空 |
| `ToStatus` | string，最大 30 | 变更后状态 |
| `ChangedAt` | DateTime | 状态变更时间 |
| `ChangedBy` | string，最大 100 | 操作人标识，可为用户 Id 或管理员标识 |

主要关系：

- 多条状态历史属于一笔订单。

## `ShippingAddresses` 收货地址表

保存用户维护的收货地址。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 地址编号 |
| `UserId` | string，外键 | 所属用户 |
| `ReceiverName` | string，最大 50 | 收货人姓名 |
| `ReceiverPhone` | string，最大 30 | 收货人电话 |
| `Province` | string，最大 30 | 省份 |
| `City` | string，最大 30 | 城市 |
| `District` | string，最大 30 | 区县 |
| `StreetAddress` | string，最大 200 | 详细地址 |
| `IsDefault` | bool | 是否默认地址 |
| `CreatedAt` | DateTime | 创建时间 |

主要关系：

- 一个用户可维护多个收货地址。
- 结算时可选择地址，订单会保存收货地址快照到 `Orders.ShippingAddress`。

## `WishlistItems` 收藏表

保存用户收藏的图书。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 收藏记录编号 |
| `UserId` | string，外键 | 收藏用户 |
| `ProductId` | int，外键 | 收藏的图书 |
| `AddedAt` | DateTime | 收藏时间 |

约束：

- `UserId + ProductId` 唯一，同一用户不能重复收藏同一本书。

## `BookReviews` 图书评价表

保存用户对图书的评分、评价、晒图和追评。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 评价编号 |
| `UserId` | string，外键 | 评价用户 |
| `ProductId` | int，外键 | 被评价图书 |
| `Rating` | int | 评分，通常为 1 到 5 |
| `Content` | string，最大 500 | 评价内容 |
| `ImageUrl` | string，最大 500 | 晒图图片地址 |
| `FollowUpContent` | string，最大 500 | 追评内容 |
| `FollowUpAt` | DateTime? | 追评时间 |
| `CreatedAt` | DateTime | 评价创建时间 |
| `UpdatedAt` | DateTime | 评价最后更新时间 |

约束：

- `UserId + ProductId` 唯一，同一用户对同一本书只保留一条评价。

主要关系：

- 一条评价属于一个用户。
- 一条评价属于一本图书。

## `BookQuestions` 读者问答表

保存图书详情页的读者提问和回答。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 问答编号 |
| `UserId` | string，外键 | 提问用户 |
| `ProductId` | int，外键 | 对应图书 |
| `Question` | string，最大 500 | 问题内容 |
| `Answer` | string，最大 500 | 回答内容 |
| `AnsweredBy` | string，最大 100 | 回答人标识，通常为管理员或回答用户 |
| `CreatedAt` | DateTime | 提问时间 |
| `AnsweredAt` | DateTime? | 回答时间 |

索引：

- `ProductId + CreatedAt`，用于按图书查询问答并按时间排序。

主要关系：

- 一条问答属于一个用户。
- 一条问答属于一本图书。

## `Coupons` 优惠券表

保存优惠券活动配置。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 优惠券编号 |
| `Name` | string，最大 80 | 优惠券名称 |
| `Code` | string，最大 30，唯一 | 优惠券编码 |
| `MinimumAmount` | decimal(10,2) | 使用门槛，订单金额达到该值才可使用 |
| `DiscountAmount` | decimal(10,2) | 抵扣金额 |
| `IsActive` | bool | 是否启用 |
| `StartsAt` | DateTime | 生效时间 |
| `EndsAt` | DateTime | 过期时间 |
| `CreatedAt` | DateTime | 创建时间 |

主要关系：

- 一张优惠券可被多个用户领取：`Coupons.Id` -> `UserCoupons.CouponId`

## `UserCoupons` 用户优惠券表

保存用户领取和使用优惠券的记录。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 用户优惠券记录编号 |
| `UserId` | string，外键 | 领取用户 |
| `CouponId` | int，外键 | 优惠券编号 |
| `ClaimedAt` | DateTime | 领取时间 |
| `UsedAt` | DateTime? | 使用时间，为空表示未使用 |
| `OrderId` | int?，外键 | 使用该优惠券的订单编号 |

约束：

- `UserId + CouponId` 唯一，同一用户不能重复领取同一张优惠券。

删除行为：

- 如果关联订单被删除，`OrderId` 会被置空。

## `UserNotifications` 用户通知表

保存系统发送给用户的通知。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 通知编号 |
| `UserId` | string，外键 | 接收通知的用户 |
| `Title` | string，最大 120 | 通知标题 |
| `Message` | string，最大 500 | 通知内容 |
| `Type` | string，最大 40 | 通知类型，例如订单、退款、评价、会员、库存等 |
| `LinkUrl` | string，最大 300 | 点击通知后跳转的地址 |
| `CreatedAt` | DateTime | 通知创建时间 |
| `ReadAt` | DateTime? | 已读时间，为空表示未读 |

索引：

- `UserId + ReadAt + CreatedAt`，用于查询用户未读通知和通知列表。

## `InventoryChangeLogs` 库存流水表

记录库存变化，用于后台追踪库存变更来源。

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `Id` | int，主键 | 库存流水编号 |
| `ProductId` | int，外键 | 对应图书 |
| `ProductTitle` | string，最大 120 | 图书名称快照 |
| `QuantityBefore` | int | 变化前库存 |
| `QuantityAfter` | int | 变化后库存 |
| `QuantityChanged` | int | 变化数量，可正可负 |
| `ChangeType` | string，最大 40 | 变化类型，例如下单扣减、取消回滚、后台调整 |
| `ChangedBy` | string，最大 100 | 操作人标识 |
| `OrderId` | int?，外键 | 关联订单，可为空 |
| `Note` | string，最大 300 | 备注 |
| `ChangedAt` | DateTime | 变化时间 |

删除行为：

- 如果关联订单被删除，`OrderId` 会被置空。

## 3. Identity 表说明

## `AspNetUsers` 用户表

`AspNetUsers` 是 ASP.NET Core Identity 的用户表，项目通过 `ApplicationUser` 扩展了头像和会员字段。

### Identity 标准字段

| 字段 | 用途 |
| --- | --- |
| `Id` | 用户唯一 Id |
| `UserName` | 登录用户名 |
| `NormalizedUserName` | 标准化用户名，用于查询 |
| `Email` | 邮箱 |
| `NormalizedEmail` | 标准化邮箱 |
| `EmailConfirmed` | 邮箱是否确认 |
| `PasswordHash` | 密码哈希 |
| `SecurityStamp` | 安全戳，用于密码和登录状态校验 |
| `ConcurrencyStamp` | 并发戳 |
| `PhoneNumber` | 手机号 |
| `PhoneNumberConfirmed` | 手机号是否确认 |
| `TwoFactorEnabled` | 是否启用双因素认证 |
| `LockoutEnd` | 锁定结束时间 |
| `LockoutEnabled` | 是否允许锁定 |
| `AccessFailedCount` | 登录失败次数 |

### 项目扩展字段

| 字段 | 类型/说明 | 用途 |
| --- | --- | --- |
| `DisplayName` | string | 用户显示昵称 |
| `AvatarUrl` | string，最大 500 | 用户头像地址 |
| `CreatedAt` | DateTime | 注册时间 |
| `MemberPoints` | int | 会员积分 |
| `MemberGrowth` | int | 会员成长值 |
| `MemberLevel` | string，最大 40 | 会员等级，例如普通会员、银卡会员、金卡会员、黑钻会员 |
| `MembershipStartedAt` | DateTime? | 会员开始时间，通常为首次产生有效消费的时间 |

主要关系：

- 一个用户可有多条购物袋记录。
- 一个用户可有多笔订单。
- 一个用户可有多个收货地址。
- 一个用户可收藏多本图书。
- 一个用户可发布多条评价。
- 一个用户可提交多条问答。
- 一个用户可领取多张优惠券。
- 一个用户可收到多条通知。

## `AspNetRoles` 角色表

保存系统角色。

| 字段 | 用途 |
| --- | --- |
| `Id` | 角色唯一 Id |
| `Name` | 角色名称，例如 `Admin`、`Customer` |
| `NormalizedName` | 标准化角色名称 |
| `ConcurrencyStamp` | 并发戳 |

项目中主要角色：

- `Admin`：管理员。
- `Customer`：普通用户。

## `AspNetUserRoles` 用户角色关联表

保存用户和角色的多对多关系。

| 字段 | 用途 |
| --- | --- |
| `UserId` | 用户 Id，对应 `AspNetUsers.Id` |
| `RoleId` | 角色 Id，对应 `AspNetRoles.Id` |

典型用途：

- 判断用户是否为管理员。
- 后台页面通过角色策略限制访问。

## `AspNetUserClaims` 用户 Claim 表

保存某个用户单独拥有的 Claim。

| 字段 | 用途 |
| --- | --- |
| `Id` | 主键 |
| `UserId` | 用户 Id |
| `ClaimType` | Claim 类型 |
| `ClaimValue` | Claim 值 |

项目中通过自定义 ClaimsPrincipalFactory 将用户显示名写入登录 Claims，便于导航栏展示。

## `AspNetRoleClaims` 角色 Claim 表

保存角色级别的 Claim。

| 字段 | 用途 |
| --- | --- |
| `Id` | 主键 |
| `RoleId` | 角色 Id |
| `ClaimType` | Claim 类型 |
| `ClaimValue` | Claim 值 |

当前项目主要使用角色本身进行授权，角色 Claim 暂未作为核心业务功能。

## `AspNetUserLogins` 第三方登录表

保存外部登录提供商绑定信息。

| 字段 | 用途 |
| --- | --- |
| `LoginProvider` | 登录提供商，例如 Google、GitHub 等 |
| `ProviderKey` | 第三方平台用户唯一标识 |
| `ProviderDisplayName` | 第三方平台显示名称 |
| `UserId` | 本地用户 Id |

当前项目未接入第三方登录，但 Identity 默认会创建该表。

## `AspNetUserTokens` 用户 Token 表

保存用户相关 Token。

| 字段 | 用途 |
| --- | --- |
| `UserId` | 用户 Id |
| `LoginProvider` | Token 所属提供商 |
| `Name` | Token 名称 |
| `Value` | Token 值 |

当前项目未重点使用该表，但它是 Identity 默认表。

## 4. 非持久化模型说明

代码中还有部分模型用于页面展示或旧版本示例，并不是当前主业务 `DbSet` 表。

| 模型 | 说明 |
| --- | --- |
| `CategoryHighlight` | 首页分类统计展示模型，由查询结果临时生成，不是数据库表 |
| `UserAccount` | 旧式用户账户模型，当前认证使用 ASP.NET Core Identity 的 `ApplicationUser` |
| `UserRoles` | 角色常量类，不是数据库表 |
| `OrderStatuses` | 订单状态常量类，不是数据库表 |

## 5. 主要业务关系图文字版

```text
AspNetUsers
  ├─ CartItems ── Products
  ├─ Orders ── OrderItems ── Products
  │    └─ OrderStatusHistory
  ├─ ShippingAddresses
  ├─ WishlistItems ── Products
  ├─ BookReviews ── Products
  ├─ BookQuestions ── Products
  ├─ UserCoupons ── Coupons
  └─ UserNotifications

InventoryChangeLogs
  ├─ Products
  └─ Orders
```

## 6. 常见业务字段补充说明

### 金额字段

以下金额字段使用 `decimal(10,2)` 精度：

- `Products.Price`
- `Orders.TotalAmount`
- `Orders.DiscountAmount`
- `OrderItems.UnitPrice`
- `Coupons.MinimumAmount`
- `Coupons.DiscountAmount`

由于 SQLite 对 decimal 的部分排序和聚合支持有限，项目中部分逻辑会先查询到内存后再进行排序或汇总。

### 订单状态字段

`Orders.Status` 和 `OrderStatusHistory.FromStatus/ToStatus` 保存订单状态字符串。状态常量定义在 `OrderStatuses` 中。

### 图片地址字段

以下字段保存图片路径或 URL：

- `Products.CoverUrl`：图书封面。
- `OrderItems.CoverUrl`：订单明细中的封面快照。
- `BookReviews.ImageUrl`：评价晒图。
- `AspNetUsers.AvatarUrl`：用户头像。

本地上传文件通常位于：

```text
BookStoreSample/wwwroot/uploads
```

### 软状态字段

项目没有对所有实体使用统一软删除字段，但部分业务通过状态控制显示：

- `Products.IsActive` 控制图书上下架。
- `Coupons.IsActive` 控制优惠券是否启用。
- `UserNotifications.ReadAt` 为空表示未读，非空表示已读。
- `UserCoupons.UsedAt` 为空表示未使用，非空表示已使用。


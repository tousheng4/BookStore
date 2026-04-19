using BookStoreSample.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookStoreSample.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<BookProduct> Products => Set<BookProduct>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ShippingAddress> ShippingAddresses => Set<ShippingAddress>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<BookReview> BookReviews => Set<BookReview>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<UserCoupon> UserCoupons => Set<UserCoupon>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<InventoryChangeLog> InventoryChangeLogs => Set<InventoryChangeLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BookProduct>(entity =>
        {
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.Author).HasMaxLength(60);
            entity.Property(x => x.Publisher).HasMaxLength(80);
            entity.Property(x => x.Category).HasMaxLength(40);
            entity.Property(x => x.CoverUrl).HasMaxLength(500);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
        });

        builder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.CartItems)
                .HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.CartItems)
                .HasForeignKey(x => x.ProductId);
        });

        builder.Entity<Order>(entity =>
        {
            entity.Property(x => x.TotalAmount).HasPrecision(10, 2);
            entity.Property(x => x.Status).HasMaxLength(30);
            entity.Property(x => x.ReceiverName).HasMaxLength(50);
            entity.Property(x => x.ReceiverPhone).HasMaxLength(30);
            entity.Property(x => x.ShippingAddress).HasMaxLength(300);
            entity.Property(x => x.TrackingCompany).HasMaxLength(80);
            entity.Property(x => x.TrackingNumber).HasMaxLength(80);
            entity.Property(x => x.CouponName).HasMaxLength(80);
            entity.Property(x => x.DiscountAmount).HasPrecision(10, 2);
            entity.Property(x => x.RefundReason).HasMaxLength(500);
            entity.Property(x => x.RefundReviewedBy).HasMaxLength(100);
            entity.Property(x => x.RefundReviewNote).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.UserId);
        });

        builder.Entity<OrderStatusHistory>(entity =>
        {
            entity.Property(x => x.FromStatus).HasMaxLength(30);
            entity.Property(x => x.ToStatus).HasMaxLength(30);
            entity.Property(x => x.ChangedBy).HasMaxLength(100);
            entity.HasOne(x => x.Order)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.OrderId);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.Property(x => x.UnitPrice).HasPrecision(10, 2);
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.Author).HasMaxLength(60);
            entity.Property(x => x.CoverUrl).HasMaxLength(500);
            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId);
        });

        builder.Entity<ShippingAddress>(entity =>
        {
            entity.Property(x => x.ReceiverName).HasMaxLength(50);
            entity.Property(x => x.ReceiverPhone).HasMaxLength(30);
            entity.Property(x => x.Province).HasMaxLength(30);
            entity.Property(x => x.City).HasMaxLength(30);
            entity.Property(x => x.District).HasMaxLength(30);
            entity.Property(x => x.StreetAddress).HasMaxLength(200);
            entity.HasOne(x => x.User)
                .WithMany(x => x.ShippingAddresses)
                .HasForeignKey(x => x.UserId);
        });

        builder.Entity<WishlistItem>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.WishlistItems)
                .HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.WishlistItems)
                .HasForeignKey(x => x.ProductId);
        });

        builder.Entity<BookReview>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            entity.Property(x => x.Content).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.ProductId);
        });

        builder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.Code).HasMaxLength(30);
            entity.Property(x => x.MinimumAmount).HasPrecision(10, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(10, 2);
        });

        builder.Entity<UserCoupon>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.CouponId }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserCoupons)
                .HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Coupon)
                .WithMany(x => x.UserCoupons)
                .HasForeignKey(x => x.CouponId);
            entity.HasOne(x => x.Order)
                .WithOne()
                .HasForeignKey<UserCoupon>(x => x.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UserNotification>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(120);
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.Property(x => x.Type).HasMaxLength(40);
            entity.Property(x => x.LinkUrl).HasMaxLength(300);
            entity.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.UserId);
        });

        builder.Entity<InventoryChangeLog>(entity =>
        {
            entity.Property(x => x.ProductTitle).HasMaxLength(120);
            entity.Property(x => x.ChangeType).HasMaxLength(40);
            entity.Property(x => x.ChangedBy).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(300);
            entity.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId);
            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

using Microsoft.AspNetCore.Identity;

namespace BookStoreSample.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<ShippingAddress> ShippingAddresses { get; set; } = new List<ShippingAddress>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<BookReview> Reviews { get; set; } = new List<BookReview>();
    public ICollection<UserCoupon> UserCoupons { get; set; } = new List<UserCoupon>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}

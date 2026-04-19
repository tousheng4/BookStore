namespace BookStoreSample.Models;

public class UserCoupon
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CouponId { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
    public int? OrderId { get; set; }

    public ApplicationUser? User { get; set; }
    public Coupon? Coupon { get; set; }
    public Order? Order { get; set; }
}

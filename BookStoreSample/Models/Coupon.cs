using System.ComponentModel.DataAnnotations;

namespace BookStoreSample.Models;

public class Coupon
{
    public int Id { get; set; }

    [Display(Name = "优惠券名称")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "优惠码")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "满减门槛")]
    public decimal MinimumAmount { get; set; }

    [Display(Name = "优惠金额")]
    public decimal DiscountAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime StartsAt { get; set; } = DateTime.UtcNow;

    public DateTime EndsAt { get; set; } = DateTime.UtcNow.AddMonths(1);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserCoupon> UserCoupons { get; set; } = new List<UserCoupon>();
}

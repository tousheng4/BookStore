namespace BookStoreSample.Models;

public class Order
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string Status { get; set; } = OrderStatuses.NewOrder;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string TrackingCompany { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTime? ShippedAt { get; set; }
    public string CouponName { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public DateTime? RefundRequestedAt { get; set; }
    public string RefundReason { get; set; } = string.Empty;
    public DateTime? RefundReviewedAt { get; set; }
    public string RefundReviewedBy { get; set; } = string.Empty;
    public string RefundReviewNote { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public List<OrderStatusHistory> StatusHistory { get; set; } = new();

    public decimal OriginalAmount => Items.Sum(item => item.LineTotal);
    public bool HasPendingRefund => RefundRequestedAt is not null && RefundReviewedAt is null;
}

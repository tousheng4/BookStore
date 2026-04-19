namespace BookStoreSample.Models;

public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = string.Empty;

    public Order? Order { get; set; }
}

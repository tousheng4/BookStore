namespace BookStoreSample.Models;

public class InventoryChangeLog
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int QuantityChanged { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public int? OrderId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public BookProduct? Product { get; set; }
    public Order? Order { get; set; }
}

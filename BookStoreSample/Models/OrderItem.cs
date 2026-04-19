namespace BookStoreSample.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string Author { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;

    public Order? Order { get; set; }
    public BookProduct? Product { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

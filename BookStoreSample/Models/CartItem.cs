namespace BookStoreSample.Models;

public class CartItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
    public BookProduct? Product { get; set; }

    public decimal LineTotal => (Product?.Price ?? 0m) * Quantity;
}

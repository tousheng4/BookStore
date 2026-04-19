namespace BookStoreSample.Models;

public class WishlistItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
    public BookProduct? Product { get; set; }
}

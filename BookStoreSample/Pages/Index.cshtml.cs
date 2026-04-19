using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages;

public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<BookProduct> FeaturedProducts { get; private set; } = [];
    public IReadOnlyList<CategoryHighlight> Categories { get; private set; } = [];
    public IReadOnlyList<BookProduct> BestSellers { get; private set; } = [];
    public IReadOnlyList<BookProduct> NewArrivals { get; private set; } = [];

    public async Task OnGetAsync()
    {
        FeaturedProducts = await storeService.GetFeaturedProductsAsync(6);
        Categories = await storeService.GetCategoryHighlightsAsync();
        BestSellers = await storeService.GetBestSellersAsync(8);
        NewArrivals = await storeService.GetNewArrivalsAsync(8);
    }
}

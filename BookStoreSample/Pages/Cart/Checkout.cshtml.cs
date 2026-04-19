using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Cart;

[Authorize]
public class CheckoutModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<CartItem> Items { get; private set; } = [];
    public IReadOnlyList<ShippingAddress> Addresses { get; private set; } = [];
    public IReadOnlyList<UserCoupon> UsableCoupons { get; private set; } = [];
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
    public decimal DiscountAmount =>
        UsableCoupons.FirstOrDefault(item => item.Id == Input.SelectedCouponId)?.Coupon?.DiscountAmount ?? 0m;
    public decimal PayableAmount => Math.Max(0, TotalAmount - DiscountAmount);

    [BindProperty]
    public CheckoutInput Input { get; set; } = new();

    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Items = await storeService.GetCartAsync(userId);
        Addresses = await storeService.GetAddressesAsync(userId);
        UsableCoupons = await storeService.GetUsableCouponsAsync(userId, TotalAmount);

        if (Addresses.Count > 0 && Input.SelectedAddressId == 0)
        {
            Input.SelectedAddressId = Addresses.FirstOrDefault(a => a.IsDefault)?.Id
                                      ?? Addresses.First().Id;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Items = await storeService.GetCartAsync(userId);
        Addresses = await storeService.GetAddressesAsync(userId);
        UsableCoupons = await storeService.GetUsableCouponsAsync(userId, TotalAmount);

        string receiverName, receiverPhone, shippingAddress;

        if (Input.SelectedAddressId > 0)
        {
            var addr = Addresses.FirstOrDefault(a => a.Id == Input.SelectedAddressId);
            if (addr is null)
            {
                Message = "请选择一个有效的收货地址。";
                return Page();
            }
            receiverName = addr.ReceiverName;
            receiverPhone = addr.ReceiverPhone;
            shippingAddress = addr.FullAddress;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Input.ReceiverName) ||
                string.IsNullOrWhiteSpace(Input.ReceiverPhone) ||
                string.IsNullOrWhiteSpace(Input.ShippingAddress))
            {
                Message = "请填写完整的收货信息。";
                return Page();
            }
            receiverName = Input.ReceiverName;
            receiverPhone = Input.ReceiverPhone;
            shippingAddress = Input.ShippingAddress;
        }

        var order = await storeService.CreateOrderAsync(
            userId,
            receiverName,
            receiverPhone,
            shippingAddress,
            Input.SelectedCouponId > 0 ? Input.SelectedCouponId : null);
        if (order is null)
        {
            Message = "订单提交失败，请确认购物车商品和库存。";
            return Page();
        }

        return RedirectToPage("/Orders/Details", new { id = order.Id });
    }

    public class CheckoutInput
    {
        [Display(Name = "选择地址")]
        public int SelectedAddressId { get; set; }

        [Display(Name = "选择优惠券")]
        public int SelectedCouponId { get; set; }

        [Display(Name = "收货人")]
        public string ReceiverName { get; set; } = string.Empty;

        [Display(Name = "联系电话")]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Display(Name = "收货地址")]
        public string ShippingAddress { get; set; } = string.Empty;
    }
}

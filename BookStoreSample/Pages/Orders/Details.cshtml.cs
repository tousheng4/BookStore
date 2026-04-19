using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Orders;

[Authorize]
public class DetailsModel(StoreService storeService) : PageModel
{
    public Order? Order { get; private set; }
    public bool IsAdmin { get; private set; }

    [BindProperty]
    public string NewStatus { get; set; } = string.Empty;

    [BindProperty]
    public ShipInput Shipping { get; set; } = new();

    [BindProperty]
    public RefundInput Refund { get; set; } = new();

    [BindProperty]
    public RefundReviewInput RefundReview { get; set; } = new();

    public string? Message { get; private set; }

    public bool CanCancel =>
        Order is not null &&
        (IsAdmin
            ? OrderStatuses.CanTransition(Order.Status, OrderStatuses.Cancelled)
            : OrderStatuses.CanCustomerCancel(Order.Status));

    public bool CanPay =>
        Order is not null &&
        !IsAdmin &&
        OrderStatuses.CanTransition(Order.Status, OrderStatuses.Paid);

    public bool CanConfirmReceipt =>
        Order is not null &&
        !IsAdmin &&
        OrderStatuses.CanTransition(Order.Status, OrderStatuses.Received);

    public bool CanShip =>
        Order is not null &&
        IsAdmin &&
        OrderStatuses.CanTransition(Order.Status, OrderStatuses.Shipped);

    public bool CanRequestRefund =>
        Order is not null &&
        !IsAdmin &&
        StoreService.CanRequestRefund(Order);

    public bool CanReviewRefund =>
        Order is not null &&
        IsAdmin &&
        Order.HasPendingRefund;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (!IsAdmin)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(NewStatus))
        {
            Message = "请选择目标状态。";
            await LoadOrderAsync(id);
            return Page();
        }

        var ok = await storeService.UpdateOrderStatusAsync(id, NewStatus, userId);
        Message = ok ? "订单状态已更新。" : "更新失败，请确认状态流转是否允许。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        var ok = await storeService.CancelOrderAsync(id, userId, IsAdmin, userId);
        Message = ok ? "订单已取消，库存已回滚。" : "当前订单不能取消。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (IsAdmin)
        {
            return Forbid();
        }

        var ok = await storeService.PayOrderAsync(id, userId);
        Message = ok ? "支付成功，订单已进入待发货流程。" : "当前订单不能支付。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmReceiptAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (IsAdmin)
        {
            return Forbid();
        }

        var ok = await storeService.ConfirmReceiptAsync(id, userId);
        Message = ok ? "已确认收货，感谢你的购买。" : "当前订单不能确认收货。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostShipAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (!IsAdmin)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(Shipping.TrackingCompany) ||
            string.IsNullOrWhiteSpace(Shipping.TrackingNumber))
        {
            Message = "请填写物流公司和物流单号。";
            await LoadOrderAsync(id);
            return Page();
        }

        var ok = await storeService.ShipOrderAsync(id, Shipping.TrackingCompany, Shipping.TrackingNumber, userId);
        Message = ok ? "订单已发货，物流信息已保存。" : "发货失败，请确认订单状态是否为已支付。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostRequestRefundAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (IsAdmin)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(Refund.Reason))
        {
            Message = "请填写退款原因。";
            await LoadOrderAsync(id);
            return Page();
        }

        var ok = await storeService.RequestRefundAsync(id, userId, Refund.Reason);
        Message = ok ? "退款申请已提交，等待管理员审核。" : "当前订单不能申请退款。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostApproveRefundAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (!IsAdmin)
        {
            return Forbid();
        }

        var ok = await storeService.ApproveRefundAsync(id, userId, RefundReview.Note);
        Message = ok ? "退款申请已通过，订单已进入已退款状态。" : "退款审核失败，请确认申请仍在待审核状态。";

        await LoadOrderAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostRejectRefundAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);

        if (!IsAdmin)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(RefundReview.Note))
        {
            Message = "拒绝退款时请填写审核说明。";
            await LoadOrderAsync(id);
            return Page();
        }

        var ok = await storeService.RejectRefundAsync(id, userId, RefundReview.Note);
        Message = ok ? "退款申请已拒绝。" : "退款审核失败，请确认申请仍在待审核状态。";

        await LoadOrderAsync(id);
        return Page();
    }

    private async Task LoadOrderAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IsAdmin = User.IsInRole(UserRoles.Admin);
        Order = await storeService.GetOrderAsync(id, userId, IsAdmin);
    }

    public class ShipInput
    {
        [Display(Name = "物流公司")]
        public string TrackingCompany { get; set; } = string.Empty;

        [Display(Name = "物流单号")]
        public string TrackingNumber { get; set; } = string.Empty;
    }

    public class RefundInput
    {
        [Display(Name = "退款原因")]
        public string Reason { get; set; } = string.Empty;
    }

    public class RefundReviewInput
    {
        [Display(Name = "审核说明")]
        public string Note { get; set; } = string.Empty;
    }
}

namespace BookStoreSample.Models;

public static class OrderStatuses
{
    public const string NewOrder = "新订单";
    public const string Paid = "已支付";
    public const string Shipped = "已发货";
    public const string Received = "已收货";
    public const string Completed = "已完成";
    public const string Cancelled = "已取消";
    public const string Refunded = "已退款";

    public static readonly string[] All = [NewOrder, Paid, Shipped, Received, Completed, Cancelled, Refunded];

    public static bool CanTransition(string from, string to)
    {
        if (from == NewOrder) return to == Paid || to == Cancelled;
        if (from == Paid) return to == Shipped || to == Cancelled;
        if (from == Shipped) return to == Received;
        if (from == Received) return to == Completed;
        return false;
    }

    public static bool CanCustomerCancel(string status) => status == NewOrder;

    public static string[] GetNextStatuses(string current)
    {
        if (current == NewOrder) return [Paid, Cancelled];
        if (current == Paid) return [Shipped, Cancelled];
        if (current == Shipped) return [Received];
        if (current == Received) return [Completed];
        return [];
    }
}

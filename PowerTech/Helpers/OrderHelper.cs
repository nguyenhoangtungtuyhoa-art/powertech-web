namespace PowerTech.Helpers
{
    public static class OrderHelper
    {
        public static string GetStatusBadgeClass(string status)
        {
            return status switch
            {
                "Pending" => "bg-warning text-dark",
                "Processing" or "Confirmed" => "bg-primary text-white",
                "Shipped" or "Shipping" => "bg-info text-white",
                "Completed" => "bg-success text-white",
                "Cancelled" => "bg-danger text-white",
                _ => "bg-secondary text-white"
            };
        }

        public static string GetStatusDisplayName(string status)
        {
            return status switch
            {
                "Pending" => "Chờ xử lý",
                "Processing" => "Đang xử lý",
                "Confirmed" => "Đã xác nhận",
                "Shipped" or "Shipping" => "Đang giao hàng",
                "Completed" => "Đã hoàn tất",
                "Cancelled" => "Đã hủy",
                _ => status
            };
        }
    }
}

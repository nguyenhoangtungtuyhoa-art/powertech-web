using Microsoft.AspNetCore.SignalR;

namespace PowerTech.Hubs
{
    public class OrderHub : Hub
    {
        // Khi một đơn hàng được cập nhật (bởi Shipper, Admin hoặc Warehouse)
        public async Task UpdateOrderStatus(int orderId, string status, string paymentStatus)
        {
            await Clients.All.SendAsync("ReceiveOrderUpdate", orderId, status, paymentStatus);
        }

        // Thông báo đẩy cho các Admin khi có đơn hàng mới hoặc đơn giao thành công
        public async Task SendAdminNotification(string title, string message, string type)
        {
            await Clients.All.SendAsync("ReceiveAdminNotification", title, message, type);
        }
    }
}

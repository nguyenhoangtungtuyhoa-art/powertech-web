using Microsoft.EntityFrameworkCore;
using PowerTech.Models.Entities;

namespace PowerTech.Data.Seeders
{
    public static class OrderSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // IDs from AspNetUsers table
            string customerId = "32490401-5517-4F67-AFA2-2A3E7066C4A8"; // Nguyen Van A
            
            // Check if any orders exist for this user, if not, seed some
            if (!await context.Orders.AnyAsync(o => o.UserId == customerId))
            {
                var products = await context.Products.Take(3).ToListAsync();
                if (products.Count < 2) return;

                // 1. Completed Order
                var order1 = new Order
                {
                    OrderCode = "ORD-2026-001",
                    UserId = customerId,
                    ReceiverName = "Nguyen Van A",
                    PhoneNumber = "0901111111",
                    ShippingAddress = "TP. Hồ Chí Minh",
                    OrderStatus = "Completed",
                    PaymentStatus = "Paid",
                    PaymentMethod = "Momo",
                    Subtotal = products[0].Price,
                    ShippingFee = 30000,
                    TotalAmount = products[0].Price + 30000,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                };
                context.Orders.Add(order1);
                await context.SaveChangesAsync();
                
                context.OrderItems.Add(new OrderItem {
                    OrderId = order1.Id, ProductId = products[0].Id, Quantity = 1, UnitPrice = products[0].Price, LineTotal = products[0].Price,
                    ProductNameSnapshot = products[0].Name, ProductSkuSnapshot = products[0].SKU
                });
                context.Payments.Add(new Payment {
                    OrderId = order1.Id, PaymentMethod = "Momo", PaymentStatus = "Paid", Amount = order1.TotalAmount, TransactionCode = "MOMO-123456",
                    PaidAt = DateTime.UtcNow.AddDays(-5), CreatedAt = DateTime.UtcNow.AddDays(-5)
                });

                // 2. Pending Order
                var order2 = new Order
                {
                    OrderCode = "ORD-2026-002",
                    UserId = customerId,
                    ReceiverName = "Nguyen Van A",
                    PhoneNumber = "0901111111",
                    ShippingAddress = "Hà Nội",
                    OrderStatus = "Pending",
                    PaymentStatus = "Pending",
                    PaymentMethod = "COD",
                    Subtotal = products[1].Price,
                    ShippingFee = 50000,
                    TotalAmount = products[1].Price + 50000,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                context.Orders.Add(order2);
                await context.SaveChangesAsync();
                
                context.OrderItems.Add(new OrderItem {
                    OrderId = order2.Id, ProductId = products[1].Id, Quantity = 1, UnitPrice = products[1].Price, LineTotal = products[1].Price,
                    ProductNameSnapshot = products[1].Name, ProductSkuSnapshot = products[1].SKU
                });

                // 3. One more for Dashboard stats
                if (products.Count > 2)
                {
                     var order3 = new Order
                    {
                        OrderCode = "ORD-2026-003",
                        UserId = customerId,
                        ReceiverName = "Nguyen Van A",
                        PhoneNumber = "0901111111",
                        ShippingAddress = "Đà Nẵng",
                        OrderStatus = "Confirmed",
                        PaymentStatus = "Pending",
                        PaymentMethod = "Banking",
                        Subtotal = products[2].Price,
                        ShippingFee = 40000,
                        TotalAmount = products[2].Price + 40000,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Orders.Add(order3);
                    await context.SaveChangesAsync();
                    context.OrderItems.Add(new OrderItem {
                        OrderId = order3.Id, ProductId = products[2].Id, Quantity = 1, UnitPrice = products[2].Price, LineTotal = products[2].Price,
                        ProductNameSnapshot = products[2].Name, ProductSkuSnapshot = products[2].SKU
                    });
                }

                await context.SaveChangesAsync();
            }
        }
    }
}

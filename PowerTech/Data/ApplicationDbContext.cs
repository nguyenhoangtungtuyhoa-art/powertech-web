using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PowerTech.Models.Entities;

namespace PowerTech.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<SpecificationDefinition> SpecificationDefinitions { get; set; }
    public DbSet<ProductSpecification> ProductSpecifications { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<UserAddress> UserAddresses { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<StockTransaction> StockTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 1. Category Configuration
        builder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique();

            // Self-referencing relationship
            entity.HasOne(e => e.ParentCategory)
                .WithMany(e => e.SubCategories)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict); // avoid cycle cascade
        });

        // 2. Brand Configuration
        builder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // 3. Product Configuration
        builder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SKU).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(255);
            
            entity.HasIndex(e => e.SKU).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();

            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.DiscountPrice).HasPrecision(18, 2);

            // Relationships
            entity.HasOne(e => e.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(e => e.BrandId)
                .OnDelete(DeleteBehavior.Restrict);

            // Constraints via Check Constraints
            entity.ToTable(t => {
                t.HasCheckConstraint("CK_Product_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Product_DiscountPrice", "[DiscountPrice] IS NULL OR ([DiscountPrice] >= 0 AND [DiscountPrice] <= [Price])");
                t.HasCheckConstraint("CK_Product_StockQuantity", "[StockQuantity] >= 0");
                t.HasCheckConstraint("CK_Product_SoldQuantity", "[SoldQuantity] >= 0");
                t.HasCheckConstraint("CK_Product_WarrantyMonths", "[WarrantyMonths] >= 0");
            });
        });

        // 4. ProductImage Configuration
        builder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImageUrl).IsRequired();

            entity.HasOne(e => e.Product)
                .WithMany(p => p.ProductImages)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // Delete product -> delete images
        });

        // 5. SpecificationDefinition Configuration
        builder.Entity<SpecificationDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SpecName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(20);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.SpecificationDefinitions)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t => t.HasCheckConstraint("CK_SpecDef_DataType", "[DataType] IN ('text', 'number', 'boolean')"));
        });

        // 6. ProductSpecification Configuration
        builder.Entity<ProductSpecification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValueNumber).HasPrecision(18, 4);

            // Unique composite index to prevent duplicate specs per product
            entity.HasIndex(e => new { e.ProductId, e.SpecDefinitionId }).IsUnique();

            entity.HasOne(e => e.Product)
                .WithMany(p => p.ProductSpecifications)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SpecificationDefinition)
                .WithMany(sd => sd.ProductSpecifications)
                .HasForeignKey(e => e.SpecDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t => t.HasCheckConstraint("CK_ProductSpec_ValuePresence", "[ValueText] IS NOT NULL OR [ValueNumber] IS NOT NULL OR [ValueBoolean] IS NOT NULL"));
        });

        // 7. Order Configuration
        builder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReceiverName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ShippingAddress).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OrderStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PaymentStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => e.OrderCode).IsUnique();

            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.ShippingFee).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Order_Subtotal", "[Subtotal] >= 0");
                t.HasCheckConstraint("CK_Order_ShippingFee", "[ShippingFee] >= 0");
                t.HasCheckConstraint("CK_Order_DiscountAmount", "[DiscountAmount] >= 0");
                t.HasCheckConstraint("CK_Order_TotalAmount", "[TotalAmount] >= 0");
            });
        });

        // 8. OrderItem Configuration
        builder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_OrderItem_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_OrderItem_UnitPrice", "[UnitPrice] >= 0");
                t.HasCheckConstraint("CK_OrderItem_LineTotal", "[LineTotal] >= 0");
            });
        });

        // 9. Payment Configuration
        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PaymentStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Payment_Amount", "[Amount] >= 0");
            });
        });

        // 10. UserAddress Configuration
        builder.Entity<UserAddress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReceiverName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.StreetAddress).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserAddresses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. Cart Configuration
        builder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithOne(u => u.Cart)
                .HasForeignKey<Cart>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. CartItem Configuration
        builder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

            entity.HasOne(e => e.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(e => e.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_CartItem_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_CartItem_UnitPrice", "[UnitPrice] >= 0");
            });
        });

        // 13. Review Configuration
        builder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Comment).IsRequired().HasMaxLength(1000);
            
            entity.HasOne(e => e.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
        });

        // 14. SupportTicket Configuration
        builder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => e.TicketCode).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.SupportTickets)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssignedTo)
                .WithMany(u => u.AssignedTickets)
                .HasForeignKey(e => e.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.SupportTickets)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 15. StockTransaction Configuration
        builder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PerformedByUser)
                .WithMany()
                .HasForeignKey(e => e.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

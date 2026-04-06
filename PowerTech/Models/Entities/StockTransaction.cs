using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerTech.Models.Entities
{
    public class StockTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public string PerformedByUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TransactionType { get; set; } = "IMPORT"; // IMPORT, EXPORT, ADJUSTMENT, SALE, RETURN

        [Required]
        public int Quantity { get; set; }

        [StringLength(50)]
        public string? ReferenceType { get; set; } // PurchaseReceipt, Order

        public int? ReferenceId { get; set; }

        public int? BeforeQuantity { get; set; }

        public int? AfterQuantity { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey(nameof(ProductId))]
        public virtual Product Product { get; set; } = null!;

        [ForeignKey(nameof(PerformedByUserId))]
        public virtual ApplicationUser PerformedByUser { get; set; } = null!;
    }
}

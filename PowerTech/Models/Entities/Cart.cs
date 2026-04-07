using System.ComponentModel.DataAnnotations;

namespace PowerTech.Models.Entities
{
    public class Cart
    {
        public int Id { get; set; }

        public string? UserId { get; set; }

        public string? CookieId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ApplicationUser? User { get; set; }
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}

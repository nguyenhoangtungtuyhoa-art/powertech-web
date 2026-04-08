using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PowerTech.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        public bool IsActive { get; set; } = true;

        public bool MustChangePassword { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public string? CreatedByUserId { get; set; }
        public decimal WalletBalance { get; set; } = 0;

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

        public virtual ICollection<UserAddress> UserAddresses { get; set; } = new List<UserAddress>();

        public virtual Cart? Cart { get; set; }
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
        public virtual ICollection<SupportTicket> AssignedTickets { get; set; } = new List<SupportTicket>();
    }
}

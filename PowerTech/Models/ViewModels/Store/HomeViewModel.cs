using PowerTech.Models.Entities;

namespace PowerTech.Models.ViewModels.Store
{
    public class HomeViewModel
    {
        public List<Category> FeaturedCategories { get; set; } = new List<Category>();
        public List<Product> FeaturedProducts { get; set; } = new List<Product>();
        public List<Product> NewProducts { get; set; } = new List<Product>();
        public List<Product> DiscountProducts { get; set; } = new List<Product>();
        public List<Brand> TopBrands { get; set; } = new List<Brand>();
        public List<CategorySection> CategorySections { get; set; } = new List<CategorySection>();
        public List<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
    }

    public class MenuCategory
    {
        public Category Category { get; set; } = null!;
        public List<Category> Children { get; set; } = new List<Category>();
        public List<Brand> Brands { get; set; } = new List<Brand>();
    }

    public class CategorySection
    {
        public Category Category { get; set; } = null!;
        public List<Product> Products { get; set; } = new List<Product>();
    }
}

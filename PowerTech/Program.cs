using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PowerTech.Data;
using PowerTech.Data.Seeders;
using PowerTech.Models.Entities;
using PowerTech.Services.Interfaces;
using PowerTech.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultUI()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// Register Custom Services
builder.Services.AddScoped<ICartService, CartService>();

// Configure Access Denied Path
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Store/Home/AccessDenied";
    options.LoginPath = "/Identity/Account/Login";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (app.Environment.IsDevelopment())
        {
            // await context.Database.MigrateAsync();
        }

        await DbFixer.FixSchemaAsync(context);

        await DbSeeder.SeedRolesAndAdminAsync(services);
        // await CatalogSeeder.SeedCatalogAsync(context); // Tắt do DB thật đã có đủ dữ liệu Catalog
        // await OrderSeeder.SeedAsync(context); // Tắt do không muốn tạo thêm order ảo liên tục
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi xảy ra trong quá trình seed dữ liệu.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Handle 404 and other non-success status codes
app.UseStatusCodePagesWithReExecute("/Store/Home/Error/{0}");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{area=Store}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<PowerTech.Hubs.OrderHub>("/orderHub");
app.MapRazorPages();

app.Run();
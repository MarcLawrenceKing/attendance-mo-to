using appdev_final_req.Data;
using appdev_final_req.Models.Entitiess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// we will create a sql server in the app
builder.Services.AddDbContext<ApplicationDbContext>(options =>

options.UseSqlServer(builder.Configuration.GetConnectionString("LinuxAttendancePortal")));

// dependency injection for identity library
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = false;              // Username-only login
    options.SignIn.RequireConfirmedAccount = false;       // No need for email confirmation
})
.AddEntityFrameworkStores<ApplicationDbContext>()         // Use your DB to store users
.AddDefaultTokenProviders();                              // Enables password reset, etc.

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/Index"; // redirect here if not logged in
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

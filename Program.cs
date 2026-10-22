using EMMS.Controllers;
using EMMS.Data;
using EMMS.Data.Repository;
using EMMS.Models;
using EMMS.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();
// Every unsafe request (POST/PUT/DELETE) must carry an antiforgery token; GETs are exempt
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
// The header site.js sends on fetch / jQuery ajax POSTs
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddMemoryCache();
builder.Services.AddScoped<AssetManagementRepo>();
builder.Services.AddScoped<AssetMovementRepo>();
builder.Services.AddScoped<JobManagementRepo>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<IAssetTagSequence, AssetTagSequenceRepo>();
builder.Services.AddScoped<AssetTagService>();
builder.Services.AddScoped<MovementService>();
builder.Services.AddScoped<CylinderOrderService>();
builder.Services.AddScoped<WorkRequestService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AssetSummaryReportService>();
builder.Services.AddScoped<AssetListReportService>();
builder.Services.AddScoped<AssetRegistrationService>();
builder.Services.AddScoped<CylinderReportService>();
builder.Services.AddScoped<MovementQueueService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

using System.Threading.RateLimiting;
using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Data;
using PuzzleWebApp.Middlewares;
using PuzzleWebApp.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".PuzzleWebApp.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(1);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add rate limit
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("admin-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (httpContext.Request.Path.Equals("/Home/RateLimitError", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("rate-limit-error-page");
        }

        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(1),
            PermitLimit = 100,
            QueueLimit = 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        const int retryAfterSeconds = 1;
        response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        var returnUrl = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            ? $"{request.Path}{request.QueryString}"
            : "/";
        var rateLimitUrl = $"/Home/RateLimitError?returnUrl={Uri.EscapeDataString(returnUrl)}&retryAfterSeconds={retryAfterSeconds}";
        var isFetchRequest = request.Headers["X-Requested-With"] == "XMLHttpRequest"
            || request.Headers["Sec-Fetch-Dest"] == "empty"
            || request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

        if (isFetchRequest)
        {
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            await response.WriteAsJsonAsync(new
            {
                message = "Terlalu banyak permintaan dibuat dalam masa yang singkat.",
                redirectUrl = rateLimitUrl
            }, cancellationToken);
        }
        else
        {
            response.Redirect(rateLimitUrl);
        }
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    db.Database.Migrate(); // Automatically applies any pending migrations to the database
    CrosswordScheduleSchema.EnsureUpToDate(db.Database);

    AdminUserSeed.EnsureSeeded(
        db,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>());
    CrosswordSeed.EnsureSeeded(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Middlewares
app.UseMiddleware<CspMiddleware>();

app.UseRateLimiter();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();


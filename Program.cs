using System.Threading.RateLimiting;
using System.Globalization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(2);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add rate limit
builder.Services.AddRateLimiter(options =>
{
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
    EnsureSubmissionColumn(db);
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
app.UseRateLimiter();
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

static void EnsureSubmissionColumn(AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    connection.Open();
    try
    {
        using var checkColumn = connection.CreateCommand();
        checkColumn.CommandText = "SELECT COUNT(*) FROM pragma_table_info('CrosswordAttempts') WHERE name = 'SubmittedAtUtc'";
        var exists = Convert.ToInt32(checkColumn.ExecuteScalar()) > 0;
        if (!exists)
        {
            using var addColumn = connection.CreateCommand();
            addColumn.CommandText = "ALTER TABLE CrosswordAttempts ADD COLUMN SubmittedAtUtc TEXT NULL";
            addColumn.ExecuteNonQuery();
        }
    }
    finally
    {
        connection.Close();
    }
}

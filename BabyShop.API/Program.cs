using System.Text;
using System.Threading.RateLimiting;
using BabyShop.API;
using BabyShop.API.Filters;
using BabyShop.API.HealthChecks;
using BabyShop.API.Middleware;
using BabyShop.Application.Interfaces;
using BabyShop.Application.Interfaces.Repositories;
using BabyShop.Application.Interfaces.Services;
using BabyShop.Application.Services;
using BabyShop.Core.Interfaces;
using BabyShop.Infrastructure.Data;
using BabyShop.Infrastructure.Mappers;
using BabyShop.Infrastructure.Repositories;
using BabyShop.Infrastructure.Services;
using Dapper;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- Dapper type handlers ----------
SqlMapper.AddTypeHandler(new GenderTypeHandler());
SqlMapper.AddTypeHandler(new AgeRangeTypeHandler());

// ---------- Logging: ساخت‌یافته + scope برای correlation-id ----------
// در Production لاگ‌ها JSON هستند تا به هر سیستم مانیتورینگی قابل خوردن باشند؛
// در Development خروجی تک‌خطی و خوانا می‌ماند.
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        options.IncludeScopes = true;
        options.SingleLine = true;
    });
}
else
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.IncludeScopes = true;
        options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
    });
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ---------- Swagger ----------
const string BearerSchemeId = "Bearer";

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BabyShop API",
        Version = "v1",
        Description = "BabyShop E-Commerce API — JWT via Authorization: Bearer {token}"
    });

    c.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
    {
        Description =
            "JWT Authorization header using the Bearer scheme.\n\n" +
            "1) از /api/Auth/login توکن بگیر\n" +
            "2) فقط خود JWT را اینجا paste کن (Swagger خودش Bearer را اضافه می‌کند)\n" +
            "3) Authorize را بزن، بعد endpoint را Execute کن",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.OperationFilter<AuthorizeCheckOperationFilter>();
});

// ---------- CORS: فقط دامنه‌های اعلام‌شده ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                     ?? builder.Configuration["Cors:Origins"]?
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     ?? Array.Empty<string>();

var isDevelopment = builder.Environment.IsDevelopment();

if (!isDevelopment && allowedOrigins.Length == 0)
    throw new InvalidOperationException(
        "Cors:Origins must be configured in Production (comma separated list of frontend origins).");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // فقط در Development، بدون تنظیم دامنه
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// ---------- Rate limiting: جلوگیری از brute-force و flood ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // مسیرهای حساس احراز هویت: کد ۶ رقمی قابل حدس نباشد.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // سقف کلی روی همهٔ درخواست‌ها، جدا از هر endpoint (برای جلوگیری از flood)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید."
        }, cancellationToken);
    };
});

// ---------- Database ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) ||
    (!isDevelopment && connectionString.Contains("DESKTOP-", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection must be overridden in Production " +
        "(environment variable ConnectionStrings__DefaultConnection).");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("BabyShop.Infrastructure")));

// ---------- Hangfire ----------
var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings:HangfireConnection is not configured.");

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnection));

builder.Services.AddHangfireServer(options =>
{
    options.ServerName = "BabyShopServer";
    options.Queues = new[] { "default", "critical" };
    options.WorkerCount = Math.Max(2, Environment.ProcessorCount * 2);
});

// ---------- JWT ----------
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
    jwtKey.Length < 32)
{
    if (!isDevelopment)
        throw new InvalidOperationException(
            "Jwt:Key must be set via the Jwt__Key environment variable in Production " +
            "(minimum 32 characters).");

    // کلید فقط-برای-توسعه؛ در Production هرگز استفاده نمی‌شود.
    jwtKey ??= "DevOnly_BabyShop_Jwt_Key_AtLeast_32_Chars_Long_2026!";
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Admin", "Manager"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("Admin", "Manager", "User"));
});

// ---------- Health checks ----------
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("OK"),
        tags: new[] { "live" })
    .AddCheck<SqlServerHealthCheck>("sqlserver", tags: new[] { "ready", "db" });


// ---------- Repositories ----------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IBasketRepository, BasketRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

// ---------- Services ----------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBasketService, BasketService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IDiscountService, DiscountService>();
builder.Services.AddScoped<IRbacHandler, RbacHandler>();

var app = builder.Build();

// ---------- Middleware pipeline ----------
// ترتیب مهم است: correlation باید اول باشد تا خطاهای بقیهٔ موارد هم لاگ شوند.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (isDevelopment)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BabyShop API V1");
        c.RoutePrefix = string.Empty;
        c.EnablePersistAuthorization();
    });
}

app.UseCors("AppCors");

if (!isDevelopment)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();

// سلامت سرویس: /health فقط خودِ پروسه، /health/ready دیتابیس را هم چک می‌کند.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

// ---------- زمان‌بندی job های دوره‌ای Hangfire ----------
try
{
    RegisterRecurringJobs();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Failed to register Hangfire recurring jobs.");
}

// ---------- آماده‌سازی دیتابیس (migration/seed) ----------
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DbInitializer.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "An error occurred while initializing the database.");
    }
}

app.Run();

// job ها باید Idempotent باشند تا چند نمونه از اپ در یک زمان مشکلی ایجاد نکند.
static void RegisterRecurringJobs()
{
    RecurringJob.AddOrUpdate<IJobService>(
        "daily-sales-report", svc => svc.GenerateDailySalesReportAsync(), Cron.Daily(hour: 8, minute: 0));

    RecurringJob.AddOrUpdate<IJobService>(
        "low-stock-check", svc => svc.CheckLowStockAsync(), Cron.Hourly());

    RecurringJob.AddOrUpdate<IJobService>(
        "abandoned-carts-cleanup", svc => svc.CleanupAbandonedCartsAsync(), Cron.Weekly(dayOfWeek: DayOfWeek.Saturday, hour: 4));

    RecurringJob.AddOrUpdate<IJobService>(
        "monthly-report", svc => svc.SendMonthlyReportAsync(), Cron.Monthly(day: 1, hour: 7));
}
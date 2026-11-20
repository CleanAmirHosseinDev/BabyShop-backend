using BabyShop.Application.Interfaces.Services;
using BabyShop.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BabyShop.Application.Services;

public class JobService : IJobService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<JobService> _logger;

    public JobService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IUserRepository userRepository,
        ILogger<JobService> logger)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    // ============ متدهای قبلی ============

    public async Task SendMonthlyReportAsync()
    {
        _logger.LogInformation("شروع گزارش ماهانه");

        try
        {
            var now = DateTime.UtcNow;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var allOrders = await _orderRepository.GetAllAsync();
            var ordersInMonth = allOrders
                .Where(o => o.CreatedAt >= firstDayOfMonth && o.CreatedAt <= lastDayOfMonth)
                .ToList();

            var totalSales = ordersInMonth.Sum(o => o.TotalAmount);
            var totalOrders = ordersInMonth.Count;

            var topProducts = ordersInMonth
                .SelectMany(o => o.Items)
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    ProductName = g.First().ProductName,
                    TotalSold = g.Sum(i => i.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(5)
                .ToList();

            var report = new StringBuilder();
            report.AppendLine("═══════════════════════════");
            report.AppendLine("   گزارش ماهانه فروشگاه");
            report.AppendLine($"   ماه: {now:yyyy/MM}");
            report.AppendLine("═══════════════════════════");
            report.AppendLine($"کل فروش: {totalSales:N0} تومان");
            report.AppendLine($"تعداد سفارشات: {totalOrders}");
            report.AppendLine("───────────────────────────");
            report.AppendLine("محصولات پرفروش:");

            for (int i = 0; i < topProducts.Count; i++)
            {
                report.AppendLine($"   {i + 1}. {topProducts[i].ProductName} - {topProducts[i].TotalSold} عدد");
            }

            report.AppendLine("═══════════════════════════");
            _logger.LogInformation(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در گزارش ماهانه");
        }
    }

    public async Task CheckLowStockAsync()
    {
        _logger.LogInformation("بررسی موجودی کالاها");

        try
        {
            var allProducts = await _productRepository.GetAllAsync();
            var lowStockProducts = allProducts
                .Where(p => p.Inventories.Any() && p.Inventories.Sum(i => i.CurrentStock) < 10)
                .ToList();

            if (lowStockProducts.Any())
            {
                _logger.LogWarning("⚠️ {Count} محصول موجودی کم دارند:", lowStockProducts.Count);
                foreach (var product in lowStockProducts.Take(10))
                {
                    _logger.LogWarning("   • {Name}: {Stock} عدد", product.Name, product.Inventories.Sum(i => i.CurrentStock));
                }
            }
            else
            {
                _logger.LogInformation("✅ موجودی همه محصولات مناسب است");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در بررسی موجودی");
        }
    }

    public async Task CleanupOldOrdersAsync()
    {
        _logger.LogInformation("پاکسازی سفارش‌های قدیمی");

        try
        {
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var allOrders = await _orderRepository.GetAllAsync();
            var oldOrders = allOrders
                .Where(o => o.CreatedAt < sixMonthsAgo && o.Status == "Delivered")
                .ToList();

            _logger.LogInformation("{Count} سفارش قدیمی پیدا شد", oldOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در پاکسازی سفارش‌ها");
        }
    }

    public async Task CleanupAbandonedCartsAsync()
    {
        _logger.LogInformation("پاکسازی سبدهای خرید رها شده");

        try
        {
            var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
            var allOrders = await _orderRepository.GetAllAsync();
            var abandonedCarts = allOrders
                .Where(o => o.Status == "Pending" && o.CreatedAt < oneMonthAgo)
                .ToList();

            _logger.LogInformation("{Count} سبد رها شده پیدا شد", abandonedCarts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در پاکسازی سبدها");
        }
    }

    // ============ متدهای جدید ============

    // 1. گزارش فروش روزانه
    public async Task GenerateDailySalesReportAsync()
    {
        _logger.LogInformation("شروع گزارش فروش روزانه");

        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var allOrders = await _orderRepository.GetAllAsync();
            var todayOrders = allOrders
                .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow && o.Status == "Delivered")
                .ToList();

            var totalSales = todayOrders.Sum(o => o.TotalAmount);
            var orderCount = todayOrders.Count;

            var report = new StringBuilder();
            report.AppendLine("═══════════════════════════");
            report.AppendLine("   گزارش فروش روزانه");
            report.AppendLine($"   تاریخ: {today:yyyy/MM/dd}");
            report.AppendLine("═══════════════════════════");
            report.AppendLine($"تعداد سفارشات: {orderCount}");
            report.AppendLine($"کل فروش: {totalSales:N0} تومان");
            report.AppendLine("═══════════════════════════");

            _logger.LogInformation(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در گزارش فروش روزانه");
        }
    }

    // 2. گزارش فروش سالانه
    public async Task GenerateYearlySalesReportAsync()
    {
        _logger.LogInformation("شروع گزارش فروش سالانه");

        try
        {
            var now = DateTime.UtcNow;
            var startOfYear = new DateTime(now.Year, 1, 1);
            var startOfNextYear = startOfYear.AddYears(1);

            var allOrders = await _orderRepository.GetAllAsync();
            var yearOrders = allOrders
                .Where(o => o.CreatedAt >= startOfYear && o.CreatedAt < startOfNextYear && o.Status == "Delivered")
                .ToList();

            var totalSales = yearOrders.Sum(o => o.TotalAmount);
            var orderCount = yearOrders.Count;

            var monthlySales = yearOrders
                .GroupBy(o => o.CreatedAt.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(o => o.TotalAmount) })
                .OrderBy(g => g.Month)
                .ToList();

            var report = new StringBuilder();
            report.AppendLine("═══════════════════════════");
            report.AppendLine("   گزارش فروش سالانه");
            report.AppendLine($"   سال: {now.Year}");
            report.AppendLine("═══════════════════════════");
            report.AppendLine($"تعداد کل سفارشات: {orderCount}");
            report.AppendLine($"کل فروش سال: {totalSales:N0} تومان");
            report.AppendLine("───────────────────────────");
            report.AppendLine("فروش ماهانه:");

            foreach (var item in monthlySales)
            {
                report.AppendLine($"   {item.Month:00} - {item.Total:N0} تومان");
            }

            report.AppendLine("═══════════════════════════");
            _logger.LogInformation(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در گزارش فروش سالانه");
        }
    }

    // 3. پرفروش‌ترین محصولات
    public async Task GenerateTopSellingProductsReportAsync()
    {
        _logger.LogInformation("شروع گزارش پرفروش‌ترین محصولات");

        try
        {
            var allOrders = await _orderRepository.GetAllAsync();
            var completedOrders = allOrders.Where(o => o.Status == "Delivered").ToList();

            var productSales = new Dictionary<int, (string Name, int Quantity, decimal Revenue)>();

            foreach (var order in completedOrders)
            {
                var orderWithItems = await _orderRepository.GetOrderWithItemsAsync(order.Id);
                if (orderWithItems?.Items == null) continue;

                foreach (var item in orderWithItems.Items)
                {
                    if (productSales.ContainsKey(item.ProductId))
                    {
                        var existing = productSales[item.ProductId];
                        productSales[item.ProductId] = (existing.Name, existing.Quantity + item.Quantity, existing.Revenue + item.TotalPrice);
                    }
                    else
                    {
                        productSales[item.ProductId] = (item.ProductName, item.Quantity, item.TotalPrice);
                    }
                }
            }

            var topProducts = productSales.OrderByDescending(x => x.Value.Revenue).Take(10).ToList();

            var report = new StringBuilder();
            report.AppendLine("═══════════════════════════");
            report.AppendLine("   10 محصول پرفروش");
            report.AppendLine("═══════════════════════════");

            for (int i = 0; i < topProducts.Count; i++)
            {
                var p = topProducts[i];
                report.AppendLine($"{i + 1}. {p.Value.Name}");
                report.AppendLine($"   تعداد فروش: {p.Value.Quantity} عدد");
                report.AppendLine($"   درآمد: {p.Value.Revenue:N0} تومان");
                report.AppendLine("───────────────────────────");
            }

            _logger.LogInformation(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در گزارش پرفروش‌ترین محصولات");
        }
    }

    // 4. گزارش موجودی انبار
    public async Task GenerateInventoryStatusReportAsync()
    {
        _logger.LogInformation("شروع گزارش موجودی انبار");

        try
        {
            var inventories = await _inventoryRepository.GetAllWithProductAsync();
            var lowStock = inventories.Where(i => i.CurrentStock <= i.ReorderPoint).ToList();
            var outOfStock = inventories.Where(i => i.CurrentStock == 0).ToList();
            var healthyStock = inventories.Where(i => i.CurrentStock > i.ReorderPoint).ToList();

            var report = new StringBuilder();
            report.AppendLine("═══════════════════════════");
            report.AppendLine("   گزارش موجودی انبار");
            report.AppendLine($"   تاریخ: {DateTime.UtcNow:yyyy/MM/dd}");
            report.AppendLine("═══════════════════════════");
            report.AppendLine($"تعداد کل محصولات: {inventories.Count}");
            report.AppendLine($"موجودی مناسب: {healthyStock.Count}");
            report.AppendLine($"موجودی کم: {lowStock.Count}");
            report.AppendLine($"بی‌موجودی: {outOfStock.Count}");
            report.AppendLine("───────────────────────────");

            if (lowStock.Any())
            {
                report.AppendLine("⚠️ محصولات با موجودی کم:");
                foreach (var item in lowStock.Take(10))
                {
                    report.AppendLine($"   • {item.Product?.Name}: {item.CurrentStock} عدد (حد مجاز: {item.ReorderPoint})");
                }
            }

            report.AppendLine("═══════════════════════════");
            _logger.LogInformation(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در گزارش موجودی انبار");
        }
    }

    // 5. گزارش کاربران جدید
    public async Task GenerateNewUsersReportAsync()
    {
        _logger.LogInformation("شروع گزارش کاربران جدید");

        try
        {
            var now = DateTime.UtcNow;
            var fiveDaysAgo = now.AddDays(-5);

            var allUsers = await _userRepository.GetAllAsync();
            var newUsers = allUsers.Where(u => u.CreatedAt >= fiveDaysAgo).ToList();

            var report = new StringBuilder();
            report.AppendLine("═══════════════════════════");
            report.AppendLine("   گزارش کاربران جدید");
            report.AppendLine($"   بازه: {fiveDaysAgo:yyyy/MM/dd} تا {now:yyyy/MM/dd}");
            report.AppendLine("═══════════════════════════");
            report.AppendLine($"تعداد کاربران جدید: {newUsers.Count}");
            report.AppendLine("═══════════════════════════");

            _logger.LogInformation(report.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در گزارش کاربران جدید");
        }
    }
}
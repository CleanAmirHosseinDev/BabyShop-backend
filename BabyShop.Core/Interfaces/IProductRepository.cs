using BabyShop.Core.Entities;
using BabyShop.Core.ValueObjects;
using System.Linq.Expressions;

namespace BabyShop.Core.Interfaces;

public interface IProductRepository
{
    // ============ متدهای پایه ============
    Task<Product?> GetByIdAsync(int id);
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product> AddAsync(Product entity);
    Task UpdateAsync(Product entity);
    Task DeleteAsync(Product entity);
    Task DeleteProductAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync();
    Task<IReadOnlyList<Product>> GetPagedAsync(int page, int pageSize);
    Task<Product?> FirstOrDefaultAsync(Expression<Func<Product, bool>> predicate);
    Task<IReadOnlyList<Product>> FindAsync(Expression<Func<Product, bool>> predicate);

    // ============ متدهای صفحه‌بندی (برای سرویس) ============
    Task<IQueryable<Product>> GetQueryableAsync();
    Task<int> CountAsync(IQueryable<Product> query);
    Task<List<Product>> GetPagedAsync(IQueryable<Product> query, int pageNumber, int pageSize);

    // ============ متدهای صفحه‌بندی با فیلتر مستقیم ============
    Task<IReadOnlyList<Product>> GetProductsAsync(int pageNumber = 1, int pageSize = 10, int? categoryId = null, int? gender = null, string? ageRange = null, string? searchTerm = null);
    Task<int> GetProductsCountAsync(int? categoryId = null, int? gender = null, string? ageRange = null, string? searchTerm = null);

    // ============ متدهای اختصاصی ============
    Task<Product?> GetByIdWithCategoryAsync(int id);
    Task<IReadOnlyList<Product>> GetAllWithCategoryAsync();
    Task<IReadOnlyList<Product>> GetByCategoryAsync(int categoryId);
    Task<IReadOnlyList<Product>> SearchAsync(string term);
    Task<IReadOnlyList<Product>> GetByGenderAsync(Gender gender);
    Task<IReadOnlyList<Product>> GetByAgeRangeAsync(AgeRange ageRange);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(int threshold);
    Task<bool> CategoryExistsAsync(int categoryId);

    /// <summary>جمع موجودی همهٔ سایزهای هر محصول، برای لیست‌های صفحه‌بندی‌شده (یک کوئری، بدون N+1).</summary>
    Task<IReadOnlyDictionary<int, int>> GetStockTotalsAsync(IEnumerable<int> productIds);
}
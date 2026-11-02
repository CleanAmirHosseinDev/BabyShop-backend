using BabyShop.Core.Entities;
using BabyShop.Core.Interfaces;
using BabyShop.Core.ValueObjects;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Linq.Expressions;

namespace BabyShop.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;

    public ProductRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);

        return await connection.QueryFirstOrDefaultAsync<Product>(
            "SELECT * FROM Products WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id }
        );
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<Product>(
            "SELECT * FROM Products WHERE IsDeleted = 0"
        );

        return result.AsList();
    }

    public async Task<Product> AddAsync(Product entity)
    {
        using var connection = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@Name", entity.Name);
        parameters.Add("@Slug", entity.Slug);
        parameters.Add("@Description", entity.Description);
        parameters.Add("@Price", entity.Price);
        parameters.Add("@CategoryId", entity.CategoryId);
        // ذخیره یکدست: 1 / 2 / 3
        parameters.Add("@Gender", entity.Gender.Value.ToString());
        parameters.Add("@AgeRange", entity.AgeRange.Code);
        parameters.Add("@ImageUrl", entity.ImageUrl);
        parameters.Add("@Id", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(
            "INSERT INTO Products (Name, Slug, Description, Price, CategoryId, Gender, AgeRange, ImageUrl, IsDeleted, CreatedAt) " +
            "VALUES (@Name, @Slug, @Description, @Price, @CategoryId, @Gender, @AgeRange, @ImageUrl, 0, GETDATE()); " +
            "SET @Id = SCOPE_IDENTITY();",
            parameters);

        entity.Id = parameters.Get<int>("@Id");
        return entity;
    }

    public async Task UpdateAsync(Product entity)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "UPDATE Products SET " +
            "Name = @Name, " +
            "Slug = @Slug, " +
            "Description = @Description, " +
            "Price = @Price, " +
            "CategoryId = @CategoryId, " +
            "Gender = @Gender, " +
            "AgeRange = @AgeRange, " +
            "ImageUrl = @ImageUrl, " +
            "UpdatedAt = GETDATE() " +
            "WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                Id = entity.Id,
                Name = entity.Name,
                Slug = entity.Slug,
                Description = entity.Description,
                Price = entity.Price,
                CategoryId = entity.CategoryId,
                Gender = entity.Gender.Value.ToString(),
                AgeRange = entity.AgeRange.Code,
                ImageUrl = entity.ImageUrl
            }
        );
    }

    public async Task DeleteProductAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "UPDATE Products SET IsDeleted = 1 WHERE Id = @Id",
            new { Id = id }
        );
    }

    public async Task<bool> ExistsAsync(int id)
    {
        var product = await GetByIdAsync(id);
        return product != null;
    }

    public async Task<IQueryable<Product>> GetQueryableAsync()
    {
        var products = await GetAllAsync();
        return products.AsQueryable();
    }

    public async Task<int> CountAsync(IQueryable<Product> query)
    {
        return await Task.FromResult(query.Count());
    }

    public async Task<List<Product>> GetPagedAsync(IQueryable<Product> query, int pageNumber, int pageSize)
    {
        var result = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return await Task.FromResult(result);
    }

    public async Task<IReadOnlyList<Product>> GetProductsAsync(
        int pageNumber = 1,
        int pageSize = 10,
        int? categoryId = null,
        int? gender = null,
        string? ageRange = null,
        string? searchTerm = null)
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = "SELECT * FROM Products WHERE IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (categoryId.HasValue)
        {
            sql += " AND CategoryId = @CategoryId";
            parameters.Add("@CategoryId", categoryId);
        }

        if (gender.HasValue)
        {
            var g = Gender.FromValue(gender.Value);
            sql += " AND Gender IN @GenderValues";
            parameters.Add("@GenderValues", g.DbValues);
        }

        if (!string.IsNullOrEmpty(ageRange))
        {
            sql += " AND AgeRange = @AgeRange";
            parameters.Add("@AgeRange", ageRange);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            sql += " AND (Name LIKE @SearchTerm OR Description LIKE @SearchTerm)";
            parameters.Add("@SearchTerm", $"%{searchTerm}%");
        }

        sql += " ORDER BY Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add("@Offset", (pageNumber - 1) * pageSize);
        parameters.Add("@PageSize", pageSize);

        var result = await connection.QueryAsync<Product>(sql, parameters);
        return result.AsList();
    }

    public async Task<int> GetProductsCountAsync(
        int? categoryId = null,
        int? gender = null,
        string? ageRange = null,
        string? searchTerm = null)
    {
        using var connection = new SqlConnection(_connectionString);

        var sql = "SELECT COUNT(*) FROM Products WHERE IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (categoryId.HasValue)
        {
            sql += " AND CategoryId = @CategoryId";
            parameters.Add("@CategoryId", categoryId);
        }

        if (gender.HasValue)
        {
            var g = Gender.FromValue(gender.Value);
            sql += " AND Gender IN @GenderValues";
            parameters.Add("@GenderValues", g.DbValues);
        }

        if (!string.IsNullOrEmpty(ageRange))
        {
            sql += " AND AgeRange = @AgeRange";
            parameters.Add("@AgeRange", ageRange);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            sql += " AND (Name LIKE @SearchTerm OR Description LIKE @SearchTerm)";
            parameters.Add("@SearchTerm", $"%{searchTerm}%");
        }

        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public async Task<Product?> GetByIdWithCategoryAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);

        var productDict = new Dictionary<int, Product>();

        await connection.QueryAsync<Product, Category, Product>(
            @"SELECT p.*, c.*
              FROM Products p
              LEFT JOIN Categories c ON p.CategoryId = c.Id
              WHERE p.Id = @Id AND p.IsDeleted = 0",
            (product, category) =>
            {
                if (!productDict.TryGetValue(product.Id, out var currentProduct))
                {
                    currentProduct = product;
                    productDict.Add(product.Id, currentProduct);
                }
                currentProduct.Category = category;
                return currentProduct;
            },
            new { Id = id },
            splitOn: "Id"
        );

        return productDict.Values.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Product>> GetAllWithCategoryAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var productDict = new Dictionary<int, Product>();

        await connection.QueryAsync<Product, Category, Product>(
            @"SELECT p.*, c.*
              FROM Products p
              LEFT JOIN Categories c ON p.CategoryId = c.Id
              WHERE p.IsDeleted = 0",
            (product, category) =>
            {
                if (!productDict.TryGetValue(product.Id, out var currentProduct))
                {
                    currentProduct = product;
                    productDict.Add(product.Id, currentProduct);
                }
                currentProduct.Category = category;
                return currentProduct;
            },
            splitOn: "Id"
        );

        return productDict.Values.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(int categoryId)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<Product>(
            "SELECT * FROM Products WHERE CategoryId = @CategoryId AND IsDeleted = 0",
            new { CategoryId = categoryId }
        );

        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(string term)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<Product>(
            @"SELECT * FROM Products 
              WHERE (Name LIKE @Term OR Description LIKE @Term) AND IsDeleted = 0",
            new { Term = $"%{term}%" }
        );

        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetByGenderAsync(Gender gender)
    {
        using var connection = new SqlConnection(_connectionString);

        // Male / پسرانه / 1 همگی match شوند
        var result = await connection.QueryAsync<Product>(
            @"SELECT * FROM Products
              WHERE IsDeleted = 0
                AND (
                      Gender IN @Values
                      OR LTRIM(RTRIM(CAST(Gender AS NVARCHAR(50)))) = @Display
                    )",
            new
            {
                Values = gender.DbValues,
                Display = gender.DisplayName
            }
        );

        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetByAgeRangeAsync(AgeRange ageRange)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<Product>(
            @"SELECT * FROM Products
              WHERE IsDeleted = 0
                AND (
                      AgeRange = @Code
                      OR REPLACE(AgeRange, ' ', '') = @Code
                    )",
            new { Code = ageRange.Code }
        );

        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(int threshold)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<Product>(
            @"SELECT p.* FROM Products p 
              INNER JOIN Inventories i ON p.Id = i.ProductId 
              WHERE p.IsDeleted = 0 AND i.CurrentStock <= @Threshold",
            new { Threshold = threshold }
        );

        return result.AsList();
    }

    public async Task<bool> CategoryExistsAsync(int categoryId)
    {
        using var connection = new SqlConnection(_connectionString);

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Categories WHERE Id = @Id AND IsDeleted = 0",
            new { Id = categoryId }
        );

        return count > 0;
    }

    public async Task<IReadOnlyDictionary<int, int>> GetStockTotalsAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, int>();

        using var connection = new SqlConnection(_connectionString);

        var rows = await connection.QueryAsync<(int ProductId, int TotalStock)>(
            @"SELECT i.ProductId, ISNULL(SUM(i.CurrentStock), 0) AS TotalStock
              FROM Inventories i
              WHERE i.ProductId IN @Ids AND ISNULL(i.IsDeleted, 0) = 0
              GROUP BY i.ProductId",
            new { Ids = ids }
        );

        return rows.ToDictionary(r => r.ProductId, r => r.TotalStock);
    }

    public async Task DeleteAsync(Product entity)
    {
        await DeleteProductAsync(entity.Id);
    }

    public async Task<int> CountAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Products WHERE IsDeleted = 0"
        );
    }

    public async Task<IReadOnlyList<Product>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = new SqlConnection(_connectionString);

        var skip = (page - 1) * pageSize;

        var result = await connection.QueryAsync<Product>(
            "SELECT * FROM Products WHERE IsDeleted = 0 ORDER BY Id OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Skip = skip, PageSize = pageSize }
        );

        return result.AsList();
    }

    public async Task<Product?> FirstOrDefaultAsync(Expression<Func<Product, bool>> predicate)
    {
        var all = await GetAllAsync();
        return all.AsQueryable().FirstOrDefault(predicate.Compile());
    }

    public async Task<IReadOnlyList<Product>> FindAsync(Expression<Func<Product, bool>> predicate)
    {
        var all = await GetAllAsync();
        var result = all.AsQueryable().Where(predicate.Compile()).ToList();
        return result.AsReadOnly();
    }
}

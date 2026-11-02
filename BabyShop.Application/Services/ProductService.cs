using BabyShop.Application.Dtos;
using BabyShop.Application.Helpers;
using BabyShop.Application.Interfaces.Services;
using BabyShop.Core.Entities;
using BabyShop.Core.Exceptions;
using BabyShop.Core.Interfaces;
using BabyShop.Core.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BabyShop.Application.Services;

public class ProductService : IProductService
{
    private const int MaxPageSize = 60;
    private const int MaxImagesPerProduct = 8;

    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IInventoryService inventoryService,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto)
    {
        if (!await _categoryRepository.ExistsAsync(dto.CategoryId))
            throw new BusinessRuleException("دسته‌بندی انتخاب‌شده معتبر نیست");

        var gender = ParseGender(dto.Gender);
        var ageRange = ParseAgeRange(dto.AgeRange);

        var product = new Product(dto.Name, dto.Description, dto.Price, dto.CategoryId, gender, ageRange);
        product.SetSlug(BuildSlug(dto.Slug, dto.Name));

        var savedImages = await SaveImagesAsync(dto.Images);
        if (savedImages.Count > 0)
        {
            product.SetImageUrl(string.Join("|", savedImages));
            product.SetMainImage(savedImages[0]);
        }
        else if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
        {
            product.SetImageUrl(dto.ImageUrl);
            product.SetMainImage(dto.ImageUrl.Split('|')[0].Trim());
        }

        var created = await _productRepository.AddAsync(product);

        await ApplyInitialStockAsync(created.Id, dto);
        _logger.LogInformation("Product {ProductId} created", created.Id);

        return MapToDto(created, await GetCategoryNameAsync(created.CategoryId));
    }

    public async Task<ProductDto> UpdateProductAsync(UpdateProductDto dto)
    {
        var product = await _productRepository.GetByIdAsync(dto.Id)
                      ?? throw new NotFoundException(nameof(Product), dto.Id);

        if (!await _categoryRepository.ExistsAsync(dto.CategoryId))
            throw new BusinessRuleException("دسته‌بندی انتخاب‌شده معتبر نیست");

        var gender = Gender.FromValue(dto.Gender);
        var ageRange = string.IsNullOrWhiteSpace(dto.AgeRange)
            ? product.AgeRange
            : AgeRange.FromCode(dto.AgeRange.Split(',')[0].Trim());

        product.Update(dto.Name, dto.Description, dto.Price, gender, ageRange);
        product.SetCategory(dto.CategoryId);

        var newImages = await SaveImagesAsync(dto.NewImages);
        if (newImages.Count > 0)
        {
            var existing = (product.ImageUrl ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var merged = newImages.Concat(existing).Take(MaxImagesPerProduct).ToList();
            product.SetImageUrl(string.Join("|", merged));
            product.SetMainImage(merged[0]);
        }
        else if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
        {
            product.SetImageUrl(dto.ImageUrl);
            product.SetMainImage(dto.ImageUrl.Split('|')[0].Trim());
        }

        await _productRepository.UpdateAsync(product);
        _logger.LogInformation("Product {ProductId} updated", product.Id);

        return MapToDto(product, await GetCategoryNameAsync(product.CategoryId));
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        var product = await _productRepository.GetByIdWithCategoryAsync(id);
        return product == null ? null : MapToDto(product, product.Category?.Name ?? string.Empty);
    }

    public async Task<List<ProductDto>> GetAllProductsAsync()
    {
        var products = await _productRepository.GetAllWithCategoryAsync();
        return products.Select(p => MapToDto(p, p.Category?.Name ?? string.Empty)).ToList();
    }

    public async Task<PagedResultDto<ProductListDto>> GetProductsPagedAsync(
        int pageNumber, int pageSize, int? gender = null, string? ageRange = null)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var ageRangeFilter = string.IsNullOrWhiteSpace(ageRange) ? null : ageRange.Trim();

        var products = await _productRepository.GetProductsAsync(
            pageNumber, pageSize, categoryId: null, gender: gender, ageRange: ageRangeFilter);

        var totalCount = await _productRepository.GetProductsCountAsync(
            categoryId: null, gender: gender, ageRange: ageRangeFilter);

        var categories = await _categoryRepository.GetAllAsync();
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

        var stock = await _productRepository.GetStockTotalsAsync(products.Select(p => p.Id));

        var items = products.Select(p => new ProductListDto
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Price = p.Price,
            CategoryId = p.CategoryId,
            CategoryName = categoryNames.GetValueOrDefault(p.CategoryId, string.Empty),
            IsActive = p.IsActive,
            SoldCount = p.SoldCount,
            Gender = p.Gender.Value.ToString(),
            GenderName = p.Gender.DisplayName,
            AgeRangeDisplay = p.AgeRange.DisplayName,
            CurrentStock = stock.GetValueOrDefault(p.Id),
            ImageUrl = FirstImage(p.ImageUrl)
        }).ToList();

        return new PagedResultDto<ProductListDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<ProductDto>> GetProductsByCategoryAsync(int categoryId)
    {
        var products = await _productRepository.GetByCategoryAsync(categoryId);
        var categoryName = await GetCategoryNameAsync(categoryId);
        return products.Select(p => MapToDto(p, categoryName)).ToList();
    }

    public async Task<List<ProductDto>> GetProductsByGenderAsync(int gender)
    {
        var products = await _productRepository.GetByGenderAsync(Gender.FromValue(gender));
        return await MapManyAsync(products);
    }

    public async Task<List<ProductDto>> GetProductsByAgeRangeAsync(string ageRange)
    {
        var products = await _productRepository.GetByAgeRangeAsync(AgeRange.FromCode(ageRange));
        return await MapManyAsync(products);
    }

    public async Task<List<ProductDto>> SearchProductsAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return new List<ProductDto>();
        var products = await _productRepository.SearchAsync(term.Trim());
        return await MapManyAsync(products);
    }

    public async Task<ProductDto> UploadProductImageAsync(int productId, string imageUrl)
    {
        var product = await _productRepository.GetByIdAsync(productId)
                      ?? throw new NotFoundException(nameof(Product), productId);

        var gallery = (product.ImageUrl ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => u != imageUrl)
            .Prepend(imageUrl)
            .Take(MaxImagesPerProduct)
            .ToList();

        product.SetImageUrl(string.Join("|", gallery));
        product.SetMainImage(imageUrl);

        await _productRepository.UpdateAsync(product);
        return MapToDto(product, await GetCategoryNameAsync(product.CategoryId));
    }

    public async Task DeleteProductAsync(int id)
    {
        await _productRepository.DeleteProductAsync(id);
        _logger.LogInformation("Product {ProductId} soft-deleted", id);
    }

    // ---------- helpers ----------

    private async Task ApplyInitialStockAsync(int productId, CreateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.InventoryDetails))
        {
            if (dto.InitialStock > 0)
                await _inventoryService.IncreaseStockAsync(productId, dto.InitialStock);
            return;
        }

        try
        {
            var details = JsonSerializer.Deserialize<List<AgeRangeStockDto>>(dto.InventoryDetails);
            if (details is null || details.Count == 0)
            {
                if (dto.InitialStock > 0)
                    await _inventoryService.IncreaseStockAsync(productId, dto.InitialStock);
                return;
            }

            foreach (var item in details.Where(d => d.Stock > 0))
                await _inventoryService.IncreaseStockAsync(productId, item.AgeRange, item.Stock);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid InventoryDetails payload for product {ProductId}", productId);
            if (dto.InitialStock > 0)
                await _inventoryService.IncreaseStockAsync(productId, dto.InitialStock);
        }
    }

    private static async Task<List<string>> SaveImagesAsync(List<IFormFile>? images)
    {
        var saved = new List<string>();
        if (images is null || images.Count == 0) return saved;

        foreach (var file in images.Take(MaxImagesPerProduct))
        {
            var error = ImageFileValidator.Validate(file);
            if (error is not null)
                throw new BusinessRuleException(error);

            saved.Add(await ImageFileValidator.SaveAsync(file, UploadsFolder()));
        }

        return saved;
    }

    private static string UploadsFolder()
        => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");

    private static Gender ParseGender(string? raw)
        => int.TryParse(raw, out var value) ? Gender.FromValue(value) : Gender.FromAny(raw);

    private static AgeRange ParseAgeRange(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? AgeRange.Newborn : AgeRange.FromCode(raw.Split(',')[0].Trim());

    private static string BuildSlug(string? slugFromDto, string name)
    {
        var source = string.IsNullOrWhiteSpace(slugFromDto) || slugFromDto.Contains('?')
            ? name
            : slugFromDto;

        var slug = source.Trim().ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "");

        slug = string.Join("-", slug.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(slug) || slug.Contains('?'))
            slug = "product";

        return $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
    }

    private static string? FirstImage(string? imageUrls)
        => string.IsNullOrWhiteSpace(imageUrls) ? null : imageUrls.Split('|')[0].Trim();

    private async Task<string> GetCategoryNameAsync(int categoryId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        return category?.Name ?? string.Empty;
    }

    private async Task<List<ProductDto>> MapManyAsync(IReadOnlyList<Product> products)
    {
        var names = new Dictionary<int, string>();
        foreach (var product in products)
        {
            if (!names.ContainsKey(product.CategoryId))
                names[product.CategoryId] = await GetCategoryNameAsync(product.CategoryId);
        }

        return products.Select(p => MapToDto(p, names.GetValueOrDefault(p.CategoryId, string.Empty))).ToList();
    }

    private static ProductDto MapToDto(Product product, string categoryName) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Slug = product.Slug,
        Description = product.Description,
        Price = product.Price,
        CategoryId = product.CategoryId,
        CategoryName = categoryName,
        IsActive = product.IsActive,
        ViewCount = product.ViewCount,
        SoldCount = product.SoldCount,
        Gender = product.Gender.Value.ToString(),
        GenderValue = product.Gender.Value,
        GenderName = product.Gender.DisplayName,
        AgeRangeCode = product.AgeRange.Code,
        AgeRangeDisplay = product.AgeRange.DisplayName,
        CurrentStock = product.StockQuantity,
        CreatedAt = product.CreatedAt,
        ImageUrl = product.ImageUrl ?? product.MainImage
    };
}

public class AgeRangeStockDto
{
    public string AgeRange { get; set; } = string.Empty;
    public int Stock { get; set; }
}
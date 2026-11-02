using BabyShop.Core.Entities.Base;
using BabyShop.Core.Exceptions;
using BabyShop.Core.ValueObjects;
using System;
using System.Collections.Generic;

namespace BabyShop.Core.Entities;

public class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int CategoryId { get; private set; }
    public Gender Gender { get; private set; } = null!;
    public AgeRange AgeRange { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public int ViewCount { get; private set; }
    public int SoldCount { get; private set; }
    public string? MainImage { get; private set; }
    public string? ImageUrl { get; private set; }

    public virtual Category? Category { get; set; }
    public virtual Inventory? Inventory { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<BasketItem> BasketItems { get; set; } = new List<BasketItem>();

    private Product() { }

    public Product(string name, string description, decimal price, int categoryId, Gender gender, AgeRange ageRange)
    {
        SetName(name);
        SetSlug(name);
        SetDescription(description);
        SetPrice(price);
        SetCategory(categoryId);
        SetGender(gender);
        SetAgeRange(ageRange);
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string description, decimal price, Gender gender, AgeRange ageRange)
    {
        SetName(name);
        SetDescription(description);
        SetPrice(price);
        SetGender(gender);
        SetAgeRange(ageRange);
        UpdatedAt = DateTime.UtcNow;
    }

    // متدهای عمومی برای تغییر مقادیر توسط سرویس
    public void SetSlug(string slug) => Slug = slug.Trim().ToLower().Replace(" ", "-");
    public void SetImageUrl(string imageUrl) => ImageUrl = imageUrl;
    public void SetMainImage(string imageUrl) => MainImage = imageUrl;

    public void SetCategory(int categoryId)
    {
        if (categoryId <= 0) throw new BusinessRuleException("دسته‌بندی محصول الزامی است");
        CategoryId = categoryId;
    }

    public int StockQuantity => Inventory?.CurrentStock ?? 0;

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }

    public void IncrementViewCount() => ViewCount++;

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("نام محصول الزامی است");
        Name = name.Trim();
    }

    private void SetDescription(string desc) => Description = desc?.Trim() ?? string.Empty;

    private void SetPrice(decimal price)
    {
        if (price <= 0) throw new BusinessRuleException("قیمت باید بیشتر از صفر باشد");
        Price = price;
    }

    private void SetGender(Gender g) => Gender = g;
    private void SetAgeRange(AgeRange a) => AgeRange = a;
}
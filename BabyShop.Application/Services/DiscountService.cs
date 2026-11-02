using BabyShop.Application.Interfaces.Services;

namespace BabyShop.Application.Services;

public class DiscountService : IDiscountService
{
    public async Task<decimal> CalculateDiscountAsync(decimal amount, string? discountCode)
    {
        if (string.IsNullOrWhiteSpace(discountCode))
            return await Task.FromResult(0m);

        // کد تخفیف WELCOME10 = 10 درصد تخفیف
        if (discountCode == "WELCOME10")
        {
            var discount = amount * 0.1m;
            return await Task.FromResult(discount);
        }

        // کد تخفیف SALE20 = 20 درصد تخفیف
        if (discountCode == "SALE20")
        {
            var discount = amount * 0.2m;
            return await Task.FromResult(discount);
        }

        // کد تخفیف نامعتبر → بدون تخفیف
        return await Task.FromResult(0m);
    }
}
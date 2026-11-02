using Microsoft.AspNetCore.Http;

namespace BabyShop.Application.Helpers;

/// <summary>
/// اعتبارسنجی و ذخیرهٔ امن تصاویر محصول.
/// فقط پسوندهای تصویری مجاز، با سقف حجم، و نام فایل تصادفی (بدون نام کاربر).
/// </summary>
public static class ImageFileValidator
{
    /// <summary>حداکثر حجم هر تصویر: ۵ مگابایت.</summary>
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    /// <summary>
    /// اگر فایل معتبر باشد null وگرنه پیام خطا برمی‌گرداند.
    /// </summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "فایلی انتخاب نشده است";

        if (file.Length > MaxFileSizeBytes)
            return "حجم تصویر نباید بیشتر از ۵ مگابایت باشد";

        var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return "فرمت تصویر مجاز نیست. فقط jpg, jpeg, png, webp و gif پذیرفته می‌شود";

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            return "نوع فایل ارسالی مجاز نیست";

        return null;
    }

    /// <summary>
    /// فایل را با نام تصادفی در مسیر داده‌شده ذخیره و آدرس عمومی آن را برمی‌گرداند.
    /// </summary>
    public static async Task<string> SaveAsync(IFormFile file, string uploadsRoot, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/products/{fileName}";
    }
}
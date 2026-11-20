using BabyShop.Core.Entities;
using BabyShop.Core.Interfaces;
using BabyShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BabyShop.API;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // اعمال مایگریشن‌ها: ساخت دیتابیس و ساختار جداول در اولین اجرا / به‌روزرسانی در اجراهای بعدی
        await context.Database.MigrateAsync();


        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role("Admin", "مدیر سیستم"),
                new Role("Manager", "مدیر فروشگاه"),
                new Role("User", "کاربر عادی")
            };

            context.Roles.AddRange(roles);
            await context.SaveChangesAsync();
        }

        if (!await context.Users.AnyAsync(u => u.PhoneNumber == "09123456789"))
        {
            var adminUser = new User("مدیر سیستم", "09123456789", passwordHasher.HashPassword("Admin@123"));
            adminUser.VerifyPhone();

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole != null)
            {
                var userRole = new UserRole(adminUser.Id, adminRole.Id);
                context.UserRoles.Add(userRole);
                await context.SaveChangesAsync();
            }
        }

        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new Category("پوشاک", "انواع لباس کودک", null, null, 1),
                new Category("اسباب بازی", "انواع اسباب بازی", null, null, 2),
                new Category("لوازم بهداشتی", "لوازم بهداشتی کودک", null, null, 3),
                new Category("کتاب و لوازم تحریر", "کتاب و لوازم تحریر کودک", null, null, 4),
                new Category("تغذیه", "لوازم تغذیه کودک", null, null, 5),
                new Category("کالسکه و صندلی ماشین", "کالسکه و صندلی ماشین کودک", null, null, 6),
                new Category("تجهیزات اتاق کودک", "تجهیزات اتاق کودک", null, null, 7)
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }
    }
}
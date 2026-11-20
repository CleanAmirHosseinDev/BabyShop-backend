using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BabyShop.Infrastructure.Data
{
    // این کلاس برای EF Core در زمان ساخت Migrations و Update-Database استفاده می‌شود
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private const string DefaultConnectionString =
            "Server=AMIREDAALTI;Database=BabyShopDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true;Encrypt=False";

        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? DefaultConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}

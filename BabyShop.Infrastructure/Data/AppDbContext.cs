using BabyShop.Core.Entities;
using BabyShop.Core.Entities.Base;
using BabyShop.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BabyShop.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<Basket> Baskets { get; set; }
        public DbSet<BasketItem> BasketItems { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<TicketReply> TicketReplies { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Query Filters
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Role>().HasQueryFilter(r => !r.IsDeleted);
            modelBuilder.Entity<UserRole>().HasQueryFilter(ur => !ur.IsDeleted);
            modelBuilder.Entity<Permission>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<RolePermission>().HasQueryFilter(rp => !rp.IsDeleted);
            modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Inventory>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
            modelBuilder.Entity<OrderItem>().HasQueryFilter(oi => !oi.IsDeleted);
            modelBuilder.Entity<Payment>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Delivery>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Basket>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<BasketItem>().HasQueryFilter(bi => !bi.IsDeleted);
            modelBuilder.Entity<SupportTicket>().HasQueryFilter(st => !st.IsDeleted);
            modelBuilder.Entity<TicketReply>().HasQueryFilter(tr => !tr.IsDeleted);

            // User Configuration
            modelBuilder.Entity<User>(entity => {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(11);
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
                entity.Property(u => u.Email).HasMaxLength(100);
                entity.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
                entity.HasMany(u => u.Orders).WithOne(o => o.User).HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            // Product Configuration (حل مشکل EF Core با AgeRange و Gender)
            modelBuilder.Entity<Product>(entity => {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Slug).IsRequired().HasMaxLength(250);
                entity.Property(p => p.Description).HasMaxLength(4000);
                entity.Property(p => p.MainImage).HasMaxLength(500);
                entity.Property(p => p.ImageUrl).HasMaxLength(500);
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.HasIndex(p => p.Slug).IsUnique();

                // تبدیل AgeRange به استرینگ در دیتابیس
                entity.Property(p => p.AgeRange)
                    .HasConversion(
                        v => v.Code, // اینجا Code که خودش استرینگه ذخیره می‌شه
                        v => AgeRange.FromCode(v)
                    )
                    .HasMaxLength(50)
                    .IsRequired(false);

                // تبدیل Gender به استرینگ در دیتابیس (مشکل int to string اینجا حل شد)
                entity.Property(p => p.Gender)
                    .HasConversion(
                        v => v.Value.ToString(), // اینجا حتماً به استرینگ کست می‌شه
                        v => Gender.FromAny(v)
                    )
                    .HasMaxLength(50)
                    .IsRequired(false);
            });

            // Inventory Configuration
            modelBuilder.Entity<Inventory>(entity => {
                entity.HasKey(i => i.Id);
                entity.HasOne(i => i.Product)
                      .WithMany(p => p.Inventories)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Inventory: یک محصول در هر رده سنی فقط یک رکورد موجودی دارد
            modelBuilder.Entity<Inventory>()
                .Property(i => i.Location)
                .HasMaxLength(100);

            modelBuilder.Entity<Inventory>()
                .Property(i => i.AgeRange)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<Inventory>()
                .HasIndex(i => new { i.ProductId, i.AgeRange })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Inventories_ProductId_AgeRange");

            // Order Configuration
            modelBuilder.Entity<Order>(entity => {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(20);
                entity.HasIndex(o => o.OrderNumber).IsUnique();
                entity.Property(o => o.ShippingAddress).IsRequired().HasMaxLength(500);
                entity.Property(o => o.PhoneNumber).IsRequired().HasMaxLength(15);
                entity.Property(o => o.Status).IsRequired().HasMaxLength(20);
                entity.Property(o => o.SubTotal).IsRequired().HasPrecision(18, 2);
                entity.Property(o => o.ShippingCost).IsRequired().HasPrecision(18, 2);
                entity.Property(o => o.Tax).IsRequired().HasPrecision(18, 2);
                entity.Property(o => o.Discount).IsRequired().HasPrecision(18, 2);
                entity.Property(o => o.TotalAmount).IsRequired().HasPrecision(18, 2);
                entity.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            });

            // OrderItem Configuration
            modelBuilder.Entity<OrderItem>(entity => {
                entity.HasKey(i => i.Id);
                entity.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(i => i.UnitPrice).IsRequired().HasPrecision(18, 2);
                entity.HasOne(i => i.Product)
                      .WithMany(p => p.OrderItems)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Payment Configuration
            modelBuilder.Entity<Payment>(entity => {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Amount).IsRequired().HasPrecision(18, 2);
                entity.HasOne(p => p.Order).WithOne(o => o.Payment).HasForeignKey<Payment>(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
            });

            // Delivery Configuration
            modelBuilder.Entity<Delivery>(entity => {
                entity.HasKey(d => d.Id);
                entity.HasOne(d => d.Order).WithOne(o => o.Delivery).HasForeignKey<Delivery>(d => d.OrderId).OnDelete(DeleteBehavior.Cascade);
            });

            // Basket Configuration
            modelBuilder.Entity<Basket>(entity => {
                entity.HasKey(b => b.Id);
                entity.Property(b => b.Status).IsRequired().HasMaxLength(30);
                entity.Property(b => b.DiscountCode).HasMaxLength(50);
                entity.Property(b => b.SessionId).HasMaxLength(100);
                entity.Property(b => b.IpAddress).HasMaxLength(45);
                entity.Property(b => b.UserAgent).HasMaxLength(500);
                entity.Property(b => b.TotalPrice).HasPrecision(18, 2);
                entity.Property(b => b.DiscountAmount).HasPrecision(18, 2);
                entity.Property(b => b.DiscountPercentage).HasPrecision(5, 2);
                entity.HasOne(b => b.User)
                      .WithMany()
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(b => b.Items)
                      .WithOne(i => i.Basket)
                      .HasForeignKey(i => i.BasketId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // BasketItem Configuration
            modelBuilder.Entity<BasketItem>(entity => {
                entity.HasKey(i => i.Id);
                entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
                entity.Property(i => i.DiscountPerUnit).HasPrecision(18, 2);
                entity.HasOne(i => i.Product)
                      .WithMany(p => p.BasketItems)
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Wishlist Configuration
            modelBuilder.Entity<Wishlist>(entity => {
                entity.HasKey(w => w.Id);
                entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique().HasFilter("[IsDeleted] = 0");
                entity.HasOne(w => w.User)
                      .WithMany()
                      .HasForeignKey(w => w.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(w => w.Product)
                      .WithMany()
                      .HasForeignKey(w => w.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Wishlist>().HasQueryFilter(w => !w.IsDeleted);

            // Role / Permission
            modelBuilder.Entity<Role>(entity => {
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
                entity.Property(r => r.Description).HasMaxLength(200);
                entity.HasIndex(r => r.Name).IsUnique();
            });

            modelBuilder.Entity<Permission>(entity => {
                entity.Property(p => p.Name).IsRequired().HasMaxLength(50);
                entity.Property(p => p.Module).HasMaxLength(50);
                entity.Property(p => p.Group).HasMaxLength(50);
                entity.Property(p => p.Description).HasMaxLength(200);
                entity.HasIndex(p => p.Name).IsUnique();
            });

            modelBuilder.Entity<UserRole>(entity => {
                entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
            });

            modelBuilder.Entity<RolePermission>(entity => {
                entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
            });

            // SupportTicket / TicketReply
            modelBuilder.Entity<SupportTicket>(entity => {
                entity.Property(t => t.TicketNumber).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Subject).IsRequired().HasMaxLength(200);
                entity.Property(t => t.Status).IsRequired().HasMaxLength(20);
                entity.Property(t => t.Priority).IsRequired().HasMaxLength(20);
                entity.Property(t => t.Category).IsRequired().HasMaxLength(100);
                entity.HasIndex(t => t.TicketNumber).IsUnique();
                entity.HasIndex(t => t.Status);
                entity.HasIndex(t => t.CreatedAt);
                entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.Order).WithMany().HasForeignKey(t => t.OrderId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(t => t.Replies).WithOne(r => r.Ticket).HasForeignKey(r => r.TicketId).OnDelete(DeleteBehavior.Cascade);
            });

            // TicketReply: مسیرهای cascade تکراری روی Users نباشند
            modelBuilder.Entity<TicketReply>(entity => {
                entity.Property(r => r.Message).IsRequired().HasMaxLength(2000);
                entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            // Category
            modelBuilder.Entity<Category>(entity => {
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Slug).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Description).HasMaxLength(500);
                entity.Property(c => c.ImageUrl).HasMaxLength(500);
                entity.HasIndex(c => c.Slug).IsUnique();
                entity.HasOne(c => c.ParentCategory)
                      .WithMany(c => c.SubCategories)
                      .HasForeignKey(c => c.ParentCategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Payment
            modelBuilder.Entity<Payment>(entity => {
                entity.Property(p => p.Status).IsRequired().HasMaxLength(20);
                entity.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(50);
                entity.Property(p => p.Authority).HasMaxLength(50);
                entity.Property(p => p.ReferenceNumber).HasMaxLength(100);
                entity.Property(p => p.RefundReason).HasMaxLength(500);
                entity.HasIndex(p => p.Authority).IsUnique().HasFilter("[Authority] IS NOT NULL");
            });

            // Delivery
            modelBuilder.Entity<Delivery>(entity => {
                entity.Property(d => d.Address).IsRequired().HasMaxLength(500);
                entity.Property(d => d.PhoneNumber).IsRequired().HasMaxLength(15);
                entity.Property(d => d.PostalCode).IsRequired().HasMaxLength(20);
                entity.Property(d => d.Status).IsRequired().HasMaxLength(20);
                entity.Property(d => d.Carrier).HasMaxLength(50);
                entity.Property(d => d.TrackingNumber).HasMaxLength(50);
            });

            // Order
            modelBuilder.Entity<Order>(entity => {
                entity.Property(o => o.Email).HasMaxLength(100);
                entity.Property(o => o.CustomerNote).HasMaxLength(500);
                entity.Property(o => o.AdminNote).HasMaxLength(500);
                entity.Property(o => o.CancellationReason).HasMaxLength(500);
                entity.Property(o => o.UserName).HasMaxLength(100);
            });

            // Basket indexes
            modelBuilder.Entity<Basket>(entity => {
                entity.HasIndex(b => b.SessionId);
                entity.HasIndex(b => b.Status);
                entity.HasIndex(b => b.CreatedAt);
            });

            modelBuilder.Entity<BasketItem>(entity => {
                entity.HasIndex(i => new { i.BasketId, i.ProductId }).IsUnique().HasFilter("[IsDeleted] = 0");
            });

            base.OnModelCreating(modelBuilder);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateSoftDeleteStatuses();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateSoftDeleteStatuses()
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.IsDeleted = false;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        break;
                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAt = DateTime.UtcNow;
                        break;
                }
            }
        }
    }
}
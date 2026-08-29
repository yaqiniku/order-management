using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CUSTOMER
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customer");

            entity.HasKey(x => x.ID);

            entity.Property(x => x.ID)
                .HasColumnName("id");

            entity.Property(x => x.FullName)
                .HasColumnName("full_name")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasColumnName("email")
                .HasMaxLength(150);

            entity.Property(x => x.PhoneNo)
                .HasColumnName("phone_no")
                .HasMaxLength(50);

            entity.Property(x => x.CreDate)
                .HasColumnName("cre_date");
            
            entity.Property(x => x.ModDate)
                .HasColumnName("mod_date");
        });

        // PRODUCT
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("product");

            entity.HasKey(x => x.ID);

            entity.Property(x => x.ID)
                .HasColumnName("id");

            entity.Property(x => x.ProductName)
                .HasColumnName("product_name")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Quantity)
                .HasColumnName("quantity")
                .IsRequired();

            entity.Property(x => x.Price)
                .HasColumnName("price")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.CreDate)
                .HasColumnName("cre_date");
            
            entity.Property(x => x.ModDate)
                .HasColumnName("mod_date");

            entity.ToTable(t =>
                t.HasCheckConstraint(
                    "chk_product_stock",
                    "quantity >= 0"
                )
            );
        });

        // ORDER
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.HasKey(x => x.ID);

            entity.Property(x => x.ID)
                .HasColumnName("id");

            entity.Property(x => x.CustomerID)
                .HasColumnName("customer_id")
                .IsRequired();

            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ShippingAddress)
                .HasColumnName("shipping_address")
                .IsRequired();

            entity.Property(x => x.TotalAmount)
                .HasColumnName("total_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(x => x.CreDate)
                .HasColumnName("cre_date");

            entity.Property(x => x.ModDate)
                .HasColumnName("mod_date");

            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ORDER DETAIL
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.ToTable("order_detail");

            entity.HasKey(x => x.ID);

            entity.Property(x => x.ID)
                .HasColumnName("id");

            entity.Property(x => x.OrderID)
                .HasColumnName("order_id")
                .IsRequired();

            entity.Property(x => x.ProductID)
                .HasColumnName("product_id")
                .IsRequired();

            entity.Property(x => x.Quantity)
                .HasColumnName("quantity")
                .IsRequired();

            entity.Property(x => x.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.HasOne<Order>()
                .WithMany()
                .HasForeignKey(x => x.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(x => x.ProductID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
                t.HasCheckConstraint(
                    "chk_order_detail_quantity",
                    "quantity > 0"
                )
            );
        });
    }
}
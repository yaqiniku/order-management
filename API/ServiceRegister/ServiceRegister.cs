using DAL;
using DAL.Data;
using Domain.Abstract.Repository;
using Domain.Abstract.Service;
using Microsoft.EntityFrameworkCore;
using Service;

namespace API.ServiceRegister;

public static class ServiceRegister
{
    public static IServiceCollection AddService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = BuildConnectionString(configuration);
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepositry, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderDetailRepository, OrderDetailRepository>();
        services.AddScoped<IOrderDetailService, OrderDetailService>();

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var host = GetRequiredValue(configuration, "DB_HOST");
        var port = GetRequiredValue(configuration, "DB_PORT");
        var database = GetRequiredValue(configuration, "DB_NAME");
        var username = GetRequiredValue(configuration, "DB_USERNAME");
        var password = configuration["DB_PASSWORD"] ?? string.Empty;

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private static string GetRequiredValue(
        IConfiguration configuration,
        string key)
    {
        return configuration[key]
            ?? throw new InvalidOperationException(
                $"Konfigurasi database '{key}' belum diatur.");
    }
}

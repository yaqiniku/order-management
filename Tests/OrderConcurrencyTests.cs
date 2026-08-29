using DAL;
using DAL.Data;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Service;

namespace Tests;

public sealed class OrderConcurrencyTests : IAsyncLifetime
{
    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private readonly string _connectionString = TestDatabase.ConnectionString;
    private string CustomerId => $"test-customer-{_suffix}";
    private string ProductId => $"test-product-{_suffix}";

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into customer (id, full_name, cre_date, mod_date)
            values (@customerId, 'Concurrency Test', now(), now());
            insert into product (id, product_name, quantity, price, cre_date, mod_date)
            values (@productId, 'Product X', 15, 10000, now(), now());
            """;
        command.Parameters.AddWithValue("customerId", CustomerId);
        command.Parameters.AddWithValue("productId", ProductId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from orders where customer_id = @customerId;
            delete from product where id = @productId;
            delete from customer where id = @customerId;
            """;
        command.Parameters.AddWithValue("customerId", CustomerId);
        command.Parameters.AddWithValue("productId", ProductId);
        await command.ExecuteNonQueryAsync();
    }

    // ini untuk melakukan test terkait ada order atas product yang sama, jika ternyata nanti product qty nya kurang
    [Fact]
    public async Task Concurrent_orders_cannot_deduct_more_stock_than_available()
    {
        var order1 = NewOrder();
        var order2 = NewOrder();
        var tasks = new[]
        {
            InsertWithNewService(order1, $"key-a-{_suffix}"),
            InsertWithNewService(order2, $"key-b-{_suffix}")
        };

        var outcomes = await Task.WhenAll(tasks.Select(Capture));

        Assert.Single(outcomes.Where(result => result.Success));
        Assert.Single(outcomes.Where(result => result.Exception is InvalidOperationException));
        Assert.Equal(5, await ReadStock());
        Assert.Equal(1, await CountOrders());
    }

    // ini untuk testing submit double kurang lebih, tp menurut saya penjagaannya juga bisa dilakuakn dengan melakukan pembatasan pada UI untuk visible button submit saat ID atau Statusnya null
    // jadi standarisasi ini juga bisa diberlakukan untuk button proses yang lain
    [Fact]
    public async Task Same_idempotency_key_under_race_creates_only_one_order()
    {
        var key = $"same-key-{_suffix}";
        var order1 = NewOrder();
        var order2 = NewOrder();

        var results = await Task.WhenAll(
            InsertWithNewService(order1, key),
            InsertWithNewService(order2, key));

        Assert.Equal(new[] { 0, 1 }, results.OrderBy(value => value));
        Assert.Equal(order1.ID, order2.ID);
        Assert.Equal(5, await ReadStock());
        Assert.Equal(1, await CountOrders());
    }

    // Dua admin memproses order CONFIRMED yang sama secara bersamaan.
    // Row lock pada order memastikan hanya Ship atau Cancel yang menang.
    [Fact]
    public async Task Concurrent_ship_and_cancel_only_one_status_update_wins()
    {
        var order = NewOrder();
        Assert.Equal(1, await InsertWithNewService(order, $"status-key-{_suffix}"));
        Assert.Equal(1, await ConfirmWithNewService(order.ID!));

        var outcomes = await Task.WhenAll(
            Capture(ShipWithNewService(order.ID!)),
            Capture(CancelWithNewService(order.ID!)));

        Assert.Single(outcomes.Where(result => result.Success));
        Assert.Single(outcomes.Where(result => result.Exception is InvalidOperationException));

        var finalStatus = await ReadOrderStatus(order.ID!);
        Assert.Contains(finalStatus, new[] { "SHIPPED", "CANCELLED" });

        // Bila Cancel menang, 10 unit dikembalikan. Bila Ship menang, stock tetap 5.
        Assert.Equal(finalStatus == "CANCELLED" ? 15 : 5, await ReadStock());
        Assert.Equal(1, await CountOrders());
    }

    private Order NewOrder() => new()
    {
        CustomerID = CustomerId,
        ShippingAddress = "Jakarta",
        Items = [new OrderDetail { ProductID = ProductId, Quantity = 10 }]
    };

    private async Task<int> InsertWithNewService(Order order, string key)
    {
        await using var context = CreateContext();
        var service = new OrderService(
            new OrderRepository(context),
            new OrderDetailRepository(context),
            new ProductRepository(context));
        return await service.Insert(order, key);
    }

    private Task<int> ConfirmWithNewService(string orderId) =>
        ChangeStatusWithNewService(orderId, (service, model) => service.Confirm(model));

    private Task<int> ShipWithNewService(string orderId) =>
        ChangeStatusWithNewService(orderId, (service, model) => service.Ship(model));

    private Task<int> CancelWithNewService(string orderId) =>
        ChangeStatusWithNewService(orderId, (service, model) => service.Cancel(model));

    private async Task<int> ChangeStatusWithNewService(string orderId, Func<OrderService, Order, Task<int>> operation)
    {
        await using var context = CreateContext();
        var service = new OrderService(
            new OrderRepository(context),
            new OrderDetailRepository(context),
            new ProductRepository(context));
        return await operation(service, new Order { ID = orderId });
    }

    private static async Task<(bool Success, Exception? Exception)> Capture(Task<int> task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    private async Task<int> ReadStock() => await Scalar(
        "select quantity from product where id = @id", ProductId);

    private async Task<int> CountOrders() => await Scalar(
        "select count(*)::int from orders where customer_id = @id", CustomerId);

    private async Task<string> ReadOrderStatus(string orderId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select status from orders where id = @id";
        command.Parameters.AddWithValue("id", orderId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<int> Scalar(string sql, string id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);
}

internal static class TestDatabase
{
    public static string ConnectionString { get; } = LoadConnectionString();

    private static string LoadConnectionString()
    {
        var direct = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "API", ".env"));
        if (!File.Exists(path))
            throw new InvalidOperationException(
                "Atur TEST_DB_CONNECTION atau buat API/.env sebelum menjalankan integration test.");

        var values = File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

        return $"Host={values["DB_HOST"]};Port={values["DB_PORT"]};" +
               $"Database={values["DB_NAME"]};Username={values["DB_USERNAME"]};" +
               $"Password={values.GetValueOrDefault("DB_PASSWORD", string.Empty)}";
    }
}

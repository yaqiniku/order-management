using Domain.Abstract.Repository;
using Domain.Abstract.Service;
using Domain.Models;

namespace Service;

public class OrderService(IOrderRepositry repository, IOrderDetailRepository repoDetail, IProductRepository repoProduct) : IOrderService
{
    private readonly IOrderRepositry _repository = repository;
    private readonly IOrderDetailRepository _repoDetail = repoDetail;
    private readonly IProductRepository _repoProduct = repoProduct;

    public string GenerateIdempotencyKey()
    {
        return Guid.NewGuid().ToString("N");
    }

    public async Task<List<Order>> GetRows(string? keyword,int offset,int limit)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await _repository.GetRows(transaction, keyword, offset, limit);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<Order?> GetRow(string id)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await _repository.GetRow(transaction, id);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Insert(Order order, string idempotencyKey)
    {
        idempotencyKey = idempotencyKey.Trim();

        ValidateItems(order.Items);

        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // checking apakah order atas requestID itu udah ada
            var existingOrder = await _repository.GetByIdempotencyKey(
                transaction,
                idempotencyKey);

            // kalau ternyata ordernya udah pernah di submit, return aja
            if (existingOrder is not null)
            {
                order.ID = existingOrder.ID;
                transaction.Commit();
                return 0;
            }

            order.ID = Guid.NewGuid().ToString();
            order.Status = "PENDING";
            order.TotalAmount = 0;
            order.IdempotencyKey = idempotencyKey;
            order.CreDate = DateTime.UtcNow;
            order.ModDate = DateTime.UtcNow;

            var result = await _repository.Insert(transaction, order);

            // penjagaan in case lolos dari checking di atas
            if (result == 0)
            {
                existingOrder = await _repository.GetByIdempotencyKey(
                    transaction,
                    idempotencyKey);

                if (existingOrder is null)
                {
                    throw new InvalidOperationException(
                        "Order idempotent gagal ditemukan setelah terjadi conflict.");
                }

                order.ID = existingOrder.ID;

                transaction.Commit();
                return 0;
            }

            decimal totalAmount = 0;

            foreach (var item in order.Items.OrderBy(x => x.ProductID))
            {
                var product = await _repoProduct.GetProductStock(transaction, item.ProductID!);

                if (product is null)
                {
                    throw new KeyNotFoundException($"Product '{item.ProductID}' tidak ditemukan.");
                }

                int requestedQuantity = item.Quantity!.Value;
                int availableStock = product.Quantity ?? 0;

                if (availableStock < requestedQuantity)
                {
                    int shortageQuantity = requestedQuantity - availableStock;

                    throw new InvalidOperationException($"{product.ProductName} shortage - {shortageQuantity}.");
                }

                decimal amount = requestedQuantity * (product.Price ?? 0);
                totalAmount += amount;

                OrderDetail detail = new()
                {
                    ID = Guid.NewGuid().ToString(),
                    OrderID = order.ID,
                    ProductID = item.ProductID,
                    Quantity = requestedQuantity,
                    Amount = amount,
                    CreDate = DateTime.UtcNow,
                    ModDate = DateTime.UtcNow
                };

                await _repoDetail.Insert(transaction, detail);

                product.ID = item.ProductID;
                product.Quantity = availableStock - requestedQuantity;
                product.ModDate = DateTime.UtcNow;

                await _repoProduct.UpdateQty(transaction, product);
            }

            order.TotalAmount = totalAmount;
            order.ModDate = DateTime.UtcNow;

            await _repository.UpdateTotalAmount(transaction, order);

            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Update(Order order)
    {
        ValidateItems(order.Items);

        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Mengunci header agar dua update detail/cancel tidak berjalan bersamaan.
            var existingOrder = await _repository.GetRowForUpdate(transaction, order.ID!);

            if (existingOrder is null)
            {
                transaction.Commit();
                return 0;
            }

            if (!string.Equals(existingOrder.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Detail hanya dapat diubah ketika order masih PENDING.");
            }

            var existingItems = await _repoDetail.GetByOrderID(transaction, order.ID!);

            var oldItemsByProduct = existingItems.ToDictionary(x => x.ProductID!,StringComparer.OrdinalIgnoreCase);

            var newItemsByProduct = order.Items.ToDictionary(x => x.ProductID!,StringComparer.OrdinalIgnoreCase);

            var productIds = oldItemsByProduct.Keys
                .Union(newItemsByProduct.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            decimal totalAmount = 0;

            foreach (var productId in productIds)
            {
                oldItemsByProduct.TryGetValue(productId, out var oldItem);
                newItemsByProduct.TryGetValue(productId, out var newItem);

                int oldQuantity = oldItem?.Quantity ?? 0;
                int newQuantity = newItem?.Quantity ?? 0;

                // Row product dikunci. Stock baru = stock saat ini + qty lama - qty baru.
                var product = await _repoProduct.GetProductStock(transaction, productId);

                if (product is null)
                {
                    throw new KeyNotFoundException($"Product '{productId}' tidak ditemukan.");
                }

                int currentStock = product.Quantity ?? 0;
                int adjustedStock = currentStock + oldQuantity - newQuantity;

                if (adjustedStock < 0)
                {
                    throw new InvalidOperationException($"{product.ProductName} shortage - {Math.Abs(adjustedStock)}.");
                }

                if (adjustedStock != currentStock)
                {
                    product.Quantity = adjustedStock;
                    product.ModDate = DateTime.UtcNow;
                    await _repoProduct.UpdateQty(transaction, product);
                }

                if (newItem is null)
                {
                    // Product dihapus dari order: stock lama sudah dikembalikan di atas.
                    await _repoDetail.DeleteByID(transaction, oldItem!.ID!);
                    continue;
                }

                decimal amount = newQuantity * (product.Price ?? 0);
                totalAmount += amount;

                if (oldItem is null)
                {
                    var detail = new OrderDetail
                    {
                        ID = Guid.NewGuid().ToString(),
                        OrderID = order.ID,
                        ProductID = productId,
                        Quantity = newQuantity,
                        Amount = amount,
                        CreDate = DateTime.UtcNow,
                        ModDate = DateTime.UtcNow
                    };

                    await _repoDetail.Insert(transaction, detail);
                }
                else
                {
                    oldItem.Quantity = newQuantity;
                    oldItem.Amount = amount;
                    oldItem.ModDate = DateTime.UtcNow;

                    await _repoDetail.UpdateByID(transaction, oldItem);
                }
            }

            order.TotalAmount = totalAmount;
            order.ModDate = DateTime.UtcNow;
            var result = await _repository.UpdateTotalAmount(transaction, order);

            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void ValidateItems(List<OrderDetail>? items)
    {
        if (items is null || items.Count == 0)
        {
            throw new ArgumentException("Order harus memiliki minimal satu item.");
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductID))
            {
                throw new ArgumentException("Product ID wajib diisi.");
            }

            if (item.Quantity is null or <= 0)
            {
                throw new ArgumentException(
                    $"Quantity product '{item.ProductID}' harus lebih dari nol.");
            }
        }

        var duplicateProduct = items
            .GroupBy(x => x.ProductID!, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateProduct is not null)
        {
            throw new ArgumentException(
                $"Product '{duplicateProduct.Key}' tidak boleh duplikat dalam satu order.");
        }
    }

    public async Task<int> DeleteByID(string id)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await _repository.DeleteByID(transaction, id);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Delete(string[] ids)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = 0;
            foreach(var id in ids)
            {
                result += await _repository.DeleteByID(transaction, id);
            }

            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Confirm(Order order)
    {
        return await ChangeStatus(order, "PENDING", "CONFIRMED");
    }

    public async Task<int> Ship(Order order)
    {
        return await ChangeStatus(order, "CONFIRMED", "SHIPPED");
    }

    public async Task<int> Deliver(Order order)
    {
        return await ChangeStatus(order, "SHIPPED", "DELIVERED");
    }

    public async Task<int> Cancel(Order order)
    {
        EnsureOrderIdIsProvided(order);

        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var existingOrder = await _repository.GetRowForUpdate(transaction, order.ID!);

            if (existingOrder is null)
            {
                throw new KeyNotFoundException($"Order dengan ID '{order.ID}' tidak ditemukan.");
            }

            if (!string.Equals(existingOrder.Status, "PENDING", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existingOrder.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidTransition(existingOrder.Status, "CANCELLED");
            }

            var items = await _repoDetail.GetByOrderID(transaction, order.ID!);

            // Product dikunci secara terurut agar beberapa cancel tidak saling deadlock.
            foreach (var itemGroup in items
                         .GroupBy(item => item.ProductID!, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var product = await _repoProduct.GetProductStock(transaction, itemGroup.Key);

                if (product is null)
                {
                    throw new KeyNotFoundException($"Product '{itemGroup.Key}' tidak ditemukan.");
                }

                product.Quantity = (product.Quantity ?? 0) + itemGroup.Sum(item => item.Quantity ?? 0);
                product.ModDate = DateTime.UtcNow;

                await _repoProduct.UpdateQty(transaction, product);
            }

            order.Status = "CANCELLED";
            order.ModDate = DateTime.UtcNow;
            var result = await _repository.UpdateStatus(transaction, order);

            if (result == 0)
            {
                throw new InvalidOperationException("Status order gagal diperbarui.");
            }

            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<int> ChangeStatus(Order order, string expectedStatus, string newStatus)
    {
        EnsureOrderIdIsProvided(order);

        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var existingOrder = await _repository.GetRowForUpdate(transaction, order.ID!);

            if (existingOrder is null)
            {
                throw new KeyNotFoundException($"Order dengan ID '{order.ID}' tidak ditemukan.");
            }

            if (!string.Equals(existingOrder.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidTransition(existingOrder.Status, newStatus);
            }

            order.Status = newStatus;
            order.ModDate = DateTime.UtcNow;

            var result = await _repository.UpdateStatus(transaction, order);

            if (result == 0)
            {
                throw new InvalidOperationException("Status order gagal diperbarui.");
            }

            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void EnsureOrderIdIsProvided(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.ID))
        {
            throw new ArgumentException("ID order wajib diisi.");
        }
    }

    private static InvalidOperationException InvalidTransition(string? currentStatus, string newStatus)
    {
        return new InvalidOperationException(
            $"Status order tidak dapat diubah dari '{currentStatus ?? "UNKNOWN"}' menjadi '{newStatus}'.");
    }
}

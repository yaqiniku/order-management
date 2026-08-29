using Domain.Abstract.Repository;
using Domain.Abstract.Service;
using Domain.Models;

namespace Service;

public class OrderDetailService(
    IOrderDetailRepository repository,
    IOrderRepositry orderRepository,
    IProductRepository productRepository) : IOrderDetailService
{
    private readonly IOrderDetailRepository _repository = repository;
    private readonly IOrderRepositry _orderRepository = orderRepository;
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<List<OrderDetail>> GetRows(string? keyword, int offset, int limit)
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

    public async Task<OrderDetail?> GetRow(string id)
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

    public async Task<int> Insert(OrderDetail orderDetail)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await _repository.Insert(transaction, orderDetail);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Update(OrderDetail orderDetail)
    {
        if (string.IsNullOrWhiteSpace(orderDetail.ID))
        {
            throw new ArgumentException("ID order detail wajib diisi.");
        }

        if (orderDetail.Quantity is null or <= 0)
        {
            throw new ArgumentException("Quantity harus lebih dari nol.");
        }

        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Lookup awal diperlukan untuk mengetahui header yang harus dikunci.
            var detailBeforeLock = await _repository.GetRow(transaction, orderDetail.ID);

            if (detailBeforeLock is null)
            {
                transaction.Commit();
                return 0;
            }

            var order = await _orderRepository.GetRowForUpdate(transaction, detailBeforeLock.OrderID!);

            EnsureOrderCanBeEdited(order, detailBeforeLock.OrderID!);

            // ambil ulang setelah header terkunci untuk menghindari adanya perubahan data dari poses lain
            var existingDetail = await _repository.GetRowForUpdate(transaction, orderDetail.ID);

            if (existingDetail is null)
            {
                throw new InvalidOperationException("Order detail telah dihapus oleh request lain.");
            }

            var product = await _productRepository.GetProductStock(transaction, existingDetail.ProductID!);

            if (product is null)
            {
                throw new KeyNotFoundException($"Product '{existingDetail.ProductID}' tidak ditemukan.");
            }

            int oldQuantity = existingDetail.Quantity ?? 0;
            int newQuantity = orderDetail.Quantity.Value;
            int currentStock = product.Quantity ?? 0;
            int adjustedStock = currentStock + oldQuantity - newQuantity;

            if (adjustedStock < 0)
            {
                throw new InvalidOperationException($"{product.ProductName} shortage - {Math.Abs(adjustedStock)}.");
            }

            if (adjustedStock != currentStock)
            {
                product.ID = orderDetail.ProductID;
                product.Quantity = adjustedStock;
                product.ModDate = DateTime.UtcNow;
                await _productRepository.UpdateQty(transaction, product);
            }

            // Product dan OrderID tidak boleh dipindahkan lewat endpoint update detail.
            existingDetail.Quantity = newQuantity;
            existingDetail.Amount = newQuantity * (product.Price ?? 0);
            existingDetail.ModDate = DateTime.UtcNow;

            var result = await _repository.UpdateByID(transaction, existingDetail);

            // update total amount header
            await RefreshOrderTotal(transaction, existingDetail.OrderID!);

            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> DeleteByID(string id)
    {
        return await Delete([id]);
    }

    public async Task<int> Delete(string[] ids)
    {
        if (ids is null || ids.Length == 0)
        {
            throw new ArgumentException("Minimal satu ID order detail wajib diisi.");
        }

        var distinctIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctIds.Length != ids.Length)
        {
            throw new ArgumentException("ID order detail tidak boleh kosong atau duplikat.");
        }

        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var details = new List<OrderDetail>();

            // Lookup awal untuk mendapatkan seluruh OrderID yang harus dikunci.
            foreach (var id in distinctIds)
            {
                var detail = await _repository.GetRow(transaction, id)
                    ?? throw new KeyNotFoundException($"Order detail '{id}' tidak ditemukan.");

                details.Add(detail);
            }

            var orderIds = details
                .Select(detail => detail.OrderID!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var orderId in orderIds)
            {
                var order = await _orderRepository.GetRowForUpdate(transaction, orderId);

                EnsureOrderCanBeEdited(order, orderId);
            }

            // Setelah semua header terkunci, baca ulang detail yang akan dihapus.
            details.Clear();
            foreach (var id in distinctIds)
            {
                var detail = await _repository.GetRowForUpdate(transaction, id)
                    ?? throw new InvalidOperationException(
                        $"Order detail '{id}' telah dihapus oleh request lain.");

                details.Add(detail);
            }

            // Order wajib tetap memiliki minimal satu detail.
            foreach (var orderId in orderIds)
            {
                var allOrderDetails = await _repository.GetByOrderID(transaction, orderId);

                int deletedCount = details.Count(detail =>
                    string.Equals(
                        detail.OrderID,
                        orderId,
                        StringComparison.OrdinalIgnoreCase));

                if (allOrderDetails.Count - deletedCount <= 0)
                {
                    throw new InvalidOperationException($"Order '{orderId}' harus memiliki minimal satu item.");
                }
            }

            // Gabungkan pengembalian stock per product dan kunci secara terurut
            foreach (var productGroup in details
                         .GroupBy(
                             detail => detail.ProductID!,
                             StringComparer.OrdinalIgnoreCase)
                         .OrderBy(
                             group => group.Key,
                             StringComparer.OrdinalIgnoreCase))
            {
                var product = await _productRepository.GetProductStock(transaction, productGroup.Key);

                if (product is null)
                {
                    throw new KeyNotFoundException($"Product '{productGroup.Key}' tidak ditemukan.");
                }

                int restoredQuantity = productGroup.Sum(detail => detail.Quantity ?? 0);

                product.Quantity = (product.Quantity ?? 0) + restoredQuantity;
                product.ModDate = DateTime.UtcNow;

                await _productRepository.UpdateQty(transaction, product);
            }

            int result = 0;
            foreach (var detail in details.OrderBy(detail => detail.ID))
            {
                result += await _repository.DeleteByID(transaction,detail.ID!);
            }

            foreach (var orderId in orderIds)
            {
                await RefreshOrderTotal(transaction, orderId);
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

    private static void EnsureOrderCanBeEdited(
        Order? order,
        string orderId)
    {
        if (order is null)
        {
            throw new KeyNotFoundException($"Order '{orderId}' tidak ditemukan.");
        }

        if (!string.Equals( order.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Detail hanya dapat diubah ketika order masih PENDING.");
        }
    }

    private async Task RefreshOrderTotal(System.Data.IDbTransaction transaction, string orderId)
    {
        var items = await _repository.GetByOrderID(transaction, orderId);

        var order = new Order
        {
            ID = orderId,
            TotalAmount = items.Sum(item => item.Amount ?? 0),
            ModDate = DateTime.UtcNow
        };

        await _orderRepository.UpdateByID(transaction, order);
    }
}

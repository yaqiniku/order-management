using System.Data;
using Domain.Models;

namespace Domain.Abstract.Repository;

public interface IOrderDetailRepository
{
    IDbConnection GetDbConnection();
    Task<List<OrderDetail>> GetRows(IDbTransaction transaction, string? keyword, int offset, int limit);
    Task<OrderDetail?> GetRow(IDbTransaction transaction, string id);
    Task<List<OrderDetail>> GetByOrderID(
        IDbTransaction transaction,
        string orderId);
    Task<int> Insert(IDbTransaction transaction, OrderDetail orderDetail);
    Task<int> UpdateByID(IDbTransaction transaction, OrderDetail orderDetail);
    Task<int> DeleteByID(IDbTransaction transaction, string id);
}

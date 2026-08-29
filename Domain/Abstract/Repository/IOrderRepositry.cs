using System.Data;
using Domain.Models;

namespace Domain.Abstract.Repository;

public interface IOrderRepositry
{
    IDbConnection GetDbConnection();
    Task<List<Order>> GetRows(
        IDbTransaction transaction,
        string? keyword,
        int offset,
        int limit);
    Task<Order?> GetRow(IDbTransaction transaction, string id);
    Task<Order?> GetRowForUpdate(IDbTransaction transaction, string id);
    Task<Order?> GetByIdempotencyKey(
        IDbTransaction transaction,
        string idempotencyKey);
    Task<int> Insert(IDbTransaction transaction, Order order);
    Task<int> UpdateByID(IDbTransaction transaction, Order order);
    Task<int> UpdateTotalAmount(IDbTransaction transaction, Order order);
    Task<int> DeleteByID(IDbTransaction transaction, string id);
}

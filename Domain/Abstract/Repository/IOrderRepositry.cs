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
    Task<int> Insert(IDbTransaction transaction, Order order);
    Task<int> UpdateByID(IDbTransaction transaction, Order order);
    Task<int> DeleteByID(IDbTransaction transaction, string id);
}

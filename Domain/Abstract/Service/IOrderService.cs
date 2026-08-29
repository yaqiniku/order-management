using Domain.Models;

namespace Domain.Abstract.Service;

public interface IOrderService
{
    string GenerateIdempotencyKey();
    Task<List<Order>> GetRows(string? keyword, int offset, int limit);
    Task<Order?> GetRow(string id);
    Task<int> Insert(Order order, string idempotencyKey);
    Task<int> Update(Order order);
    Task<int> Delete(string[] ids);
    Task<int> DeleteByID(string id);
}

using Domain.Models;

namespace Domain.Abstract.Service;

public interface IOrderService
{
    Task<List<Order>> GetRows(string? keyword, int offset, int limit);
    Task<Order?> GetRow(string id);
    Task<int> Insert(Order order);
    Task<int> Update(Order order);
    Task<int> Delete(string[] ids);
    Task<int> DeleteByID(string id);
}

using Domain.Models;

namespace Domain.Abstract.Service;

public interface IOrderDetailService
{
    Task<List<OrderDetail>> GetRows(string? keyword, int offset, int limit);
    Task<OrderDetail?> GetRow(string id);
    Task<int> Insert(OrderDetail orderDetail);
    Task<int> Update(OrderDetail orderDetail);
    Task<int> Delete(string[] ids);
    Task<int> DeleteByID(string id);
}

using Domain.Models;

namespace Domain.Abstract.Service;

public interface ICustomerService
{
    Task<List<Customer>> GetRows(string? keyword, int offset, int limit);
    Task<Customer?> GetRow(string id);
    Task<int> Insert(Customer customer);
    Task<int> Update(Customer customer);
    Task<int> Delete(string[] ids);
    Task<int> DeleteByID(string id);
}

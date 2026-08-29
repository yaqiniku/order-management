using Domain.Models;

namespace Domain.Abstract.Service;

public interface IProductService
{
    Task<List<Product>> GetRows(string? keyword, int offset, int limit);
    Task<Product?> GetRow(string id);
    Task<int> Insert(Product product);
    Task<int> Update(Product product);
    Task<int> Delete(string[] ids);
    Task<int> DeleteByID(string id);
}

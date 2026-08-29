using System.Data;
using Domain.Models;

namespace Domain.Abstract.Repository;

public interface IProductRepository
{
    IDbConnection GetDbConnection();
    Task<List<Product>> GetRows(IDbTransaction transaction, string? keyword, int offset, int limit);
    Task<Product?> GetRow(IDbTransaction transaction, string id);
    Task<Product?> GetProductStock(IDbTransaction transaction, string id);
    Task<int> Insert(IDbTransaction transaction, Product product);
    Task<int> UpdateByID(IDbTransaction transaction, Product product);
    Task<int> UpdateQty(IDbTransaction transaction, Product product);
    Task<int> DeleteByID(IDbTransaction transaction, string id);
}

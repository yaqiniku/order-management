using System.Data;
using Domain.Models;

namespace Domain.Abstract.Repository;

public interface ICustomerRepository
{
    IDbConnection GetDbConnection();
    Task<List<Customer>> GetRows(IDbTransaction transaction, string? keyword, int offset, int limit);
    Task<Customer?> GetRow(IDbTransaction transaction, string id);
    Task<int> Insert(IDbTransaction transaction, Customer customer);
    Task<int> UpdateByID(IDbTransaction transaction, Customer customer);
    Task<int> DeleteByID(IDbTransaction transaction, string id);
}

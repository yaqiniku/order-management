using Domain.Abstract.Repository;
using Domain.Abstract.Service;
using Domain.Models;

namespace Service;

public class OrderDetailService(IOrderDetailRepository repository) : IOrderDetailService
{
    private readonly IOrderDetailRepository _repository = repository;

    public async Task<List<OrderDetail>> GetRows(string? keyword, int offset, int limit)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await _repository.GetRows(transaction, keyword, offset, limit);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<OrderDetail?> GetRow(string id)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await _repository.GetRow(transaction, id);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Insert(OrderDetail orderDetail)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await _repository.Insert(transaction, orderDetail);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Update(OrderDetail orderDetail)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await _repository.UpdateByID(transaction, orderDetail);
            transaction.Commit();
            return result;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> DeleteByID(string id)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await _repository.DeleteByID(transaction, id);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> Delete(string[] ids)
    {
        using var connection = _repository.GetDbConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = 0;
            foreach (var id in ids)
            {
                result += await _repository.DeleteByID(transaction, id);
            }
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

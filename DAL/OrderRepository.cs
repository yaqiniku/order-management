using System.Data;
using DAL.Data;
using DAL.Helper;
using Domain.Abstract.Repository;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DAL;

public class OrderRepository(AppDbContext context) : BaseRepository(context), IOrderRepositry
{
    private readonly AppDbContext _context = context;

    public async Task<List<Order>> GetRows(IDbTransaction transaction, string? keyword,int offset,int limit)
    {
        string p = db.Symbol();

        string query = $@"
                                
                        ";

        query = QueryLimitOffset(query);

        object parameters = new
        {
            Keyword = $"%{keyword}%",
            Offset = offset,
            Limit = limit
        };

        List<Order> result = await _command.GetRows<Order>(transaction, query, parameters);

        return result;
    }

    public async Task<Order?> GetRow(IDbTransaction transaction, string id)
    {
        string p = db.Symbol();

        string query = $@"
                                
                        ";

        object parameters = new
        {
            ID = id
        };

        var result = await _command.GetRow<Order>(transaction, query, parameters);

        return result;
    }

    public async Task<int> Insert(IDbTransaction transaction, Order order)
    {
        string p = db.Symbol();

        string query = $@"
                                
                        ";

        return await _command.Insert(transaction, query, order);
    }

    public async Task<int> UpdateByID(IDbTransaction transaction,Order order)
    {
        string p = db.Symbol();

      string query = $@"

					 ";

      return await _command.Update(transaction, query, order);
    }

    public async Task<int> DeleteByID(IDbTransaction transaction, string id)
    {
        string p = db.Symbol();

        string query = $@"

				 ";
                 
        return await _command.Delete(transaction, query, id);
    } 
}

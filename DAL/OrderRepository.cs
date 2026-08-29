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
                            select
                                o.id as ID
                                ,0.customer_id as CustomerID
                                ,0.status as Status 
                                ,0.shipping_address as ShippingAddress
                                ,0.total_amount as TotalAmount
                                ,0.cre_date as CreDate
                                ,0.mod_date as ModDate
                            from 
                                orders o
                            where
                            (
                                lower(0.customer_id) like lower({p}Keyword)
								or cast(o.status as varchar) like lower({p}Keyword)
								or lower(o.shipping_address) like lower({p}Keyword)
								or lower(o.total_amount) like lower({p}Keyword)
								or lower(o.cre_date) like lower({p}Keyword)
								or lower(o.mod_date) like lower({p}Keyword)
                            )
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
                            select
                                o.id as ID
                                ,0.customer_id as CustomerID
                                ,0.status as Status 
                                ,0.shipping_address as ShippingAddress
                                ,0.total_amount as TotalAmount
                                ,0.cre_date as CreDate
                                ,0.mod_date as ModDate
                            from 
                                orders o
                            where
                                o.id = {p}ID
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
                            insert into orders
							(
								id
								,customer_id
                                ,status
                                ,shipping_address
                                ,total_amount
                                ,cre_date
								,mod_date
							)
							values
							(
								{p}ID
								,{p}CustomerID
								,{p}Status
								,{p}ShippingAddress
								,{p}TotalAmount
								,{p}CreDate
								,{p}ModDate
							)
                        ";

        return await _command.Insert(transaction, query, order);
    }

    public async Task<int> UpdateByID(IDbTransaction transaction,Order order)
    {
        string p = db.Symbol();

      string query = $@"
                        update order
                        set
                            customer_id = {p}CustomerID
                            status = {p}Status
                            shipping_address = {p}ShippingAddress
                            total_amount = {p}TotalAmount
                            cre_date = {p}CreDate
                            mod_date = {p}ModDate
                        where
                            id = {p}ID
					 ";

      return await _command.Update(transaction, query, order);
    }

    public async Task<int> DeleteByID(IDbTransaction transaction, string id)
    {
        string p = db.Symbol();

        string query = $@"
                            delete from orders where id = {p}ID
				        ";
                 
        return await _command.Delete(transaction, query, id);
    } 
}

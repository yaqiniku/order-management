using System.Data;
using DAL.Data;
using DAL.Helper;
using Domain.Abstract.Repository;
using Domain.Models;

namespace DAL;

public class OrderDetailRepository(AppDbContext context) : BaseRepository(context), IOrderDetailRepository
{
    public async Task<List<OrderDetail>> GetRows(IDbTransaction transaction, string? keyword, int offset, int limit)
    {
        var p = db.Symbol();
        var query = $@"
            select od.id as ID, od.order_id as OrderID, od.product_id as ProductID,
                   od.quantity as Quantity, od.amount as Amount,
                   od.cre_date as CreDate, od.mod_date as ModDate
            from order_detail od
            where lower(coalesce(od.id, '')) like lower({p}Keyword)
               or lower(coalesce(od.order_id, '')) like lower({p}Keyword)
               or lower(coalesce(od.product_id, '')) like lower({p}Keyword)
               or cast(od.quantity as varchar) like {p}Keyword
               or cast(od.amount as varchar) like {p}Keyword
            order by od.id";

        query = QueryLimitOffset(query);
        return await _command.GetRows<OrderDetail>(transaction, query, new
        {
            Keyword = $"%{keyword}%",
            Offset = offset,
            Limit = limit
        });
    }

    public Task<OrderDetail?> GetRow(IDbTransaction transaction, string id)
    {
        var query = $@"
            select od.id as ID, od.order_id as OrderID, od.product_id as ProductID,
                   od.quantity as Quantity, od.amount as Amount,
                   od.cre_date as CreDate, od.mod_date as ModDate
            from order_detail od where od.id = {db.Symbol()}ID";
        return _command.GetRow<OrderDetail>(transaction, query, new { ID = id });
    }

    public Task<int> Insert(IDbTransaction transaction, OrderDetail orderDetail)
    {
        var p = db.Symbol();
        var query = $@"
            insert into order_detail (id, order_id, product_id, quantity, amount, cre_date, mod_date)
            values ({p}ID, {p}OrderID, {p}ProductID, {p}Quantity, {p}Amount, {p}CreDate, {p}ModDate)";
        return _command.Insert(transaction, query, orderDetail);
    }

    public Task<int> UpdateByID(IDbTransaction transaction, OrderDetail orderDetail)
    {
        var p = db.Symbol();
        var query = $@"
            update order_detail
            set order_id = {p}OrderID, product_id = {p}ProductID, quantity = {p}Quantity,
                amount = {p}Amount, cre_date = {p}CreDate, mod_date = {p}ModDate
            where id = {p}ID";
        return _command.Update(transaction, query, orderDetail);
    }

    public Task<int> DeleteByID(IDbTransaction transaction, string id)
    {
        var query = $"delete from order_detail where id = {db.Symbol()}ID";
        return _command.Delete(transaction, query, new { ID = id });
    }
}

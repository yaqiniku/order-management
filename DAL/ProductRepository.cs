using System.Data;
using DAL.Data;
using DAL.Helper;
using Domain.Abstract.Repository;
using Domain.Models;

namespace DAL;

public class ProductRepository(AppDbContext context) : BaseRepository(context), IProductRepository
{
    public async Task<List<Product>> GetRows(IDbTransaction transaction, string? keyword, int offset, int limit)
    {
        var p = db.Symbol();
        var query = $@"
            select p.id as ID, p.quantity as Quantity, p.price as Price,
                   p.product_name as ProductName, p.cre_date as CreDate, p.mod_date as ModDate
            from product p
            where lower(coalesce(p.id, '')) like lower({p}Keyword)
               or lower(coalesce(p.product_name, '')) like lower({p}Keyword)
               or cast(p.quantity as varchar) like {p}Keyword
               or cast(p.price as varchar) like {p}Keyword
            order by p.id";

        query = QueryLimitOffset(query);
        return await _command.GetRows<Product>(transaction, query, new
        {
            Keyword = $"%{keyword}%",
            Offset = offset,
            Limit = limit
        });
    }

    public Task<Product?> GetRow(IDbTransaction transaction, string id)
    {
        var query = $@"
            select p.id as ID, p.quantity as Quantity, p.price as Price,
                   p.product_name as ProductName, p.cre_date as CreDate, p.mod_date as ModDate
            from product p where p.id = {db.Symbol()}ID";
        return _command.GetRow<Product>(transaction, query, new { ID = id });
    }

    public Task<int> Insert(IDbTransaction transaction, Product product)
    {
        var p = db.Symbol();
        var query = $@"
            insert into product (id, quantity, price, product_name, cre_date, mod_date)
            values ({p}ID, {p}Quantity, {p}Price, {p}ProductName, {p}CreDate, {p}ModDate)";
        return _command.Insert(transaction, query, product);
    }

    public Task<int> UpdateByID(IDbTransaction transaction, Product product)
    {
        var p = db.Symbol();
        var query = $@"
            update product
            set quantity = {p}Quantity, price = {p}Price, product_name = {p}ProductName,
                cre_date = {p}CreDate, mod_date = {p}ModDate
            where id = {p}ID";
        return _command.Update(transaction, query, product);
    }

    public Task<int> DeleteByID(IDbTransaction transaction, string id)
    {
        var query = $"delete from product where id = {db.Symbol()}ID";
        return _command.Delete(transaction, query, new { ID = id });
    }
}

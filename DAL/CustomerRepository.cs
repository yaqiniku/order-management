using System.Data;
using DAL.Data;
using DAL.Helper;
using Domain.Abstract.Repository;
using Domain.Models;

namespace DAL;

public class CustomerRepository(AppDbContext context) : BaseRepository(context), ICustomerRepository
{
    public async Task<List<Customer>> GetRows(IDbTransaction transaction, string? keyword, int offset, int limit)
    {
        var p = db.Symbol();
        var query = $@"
            select c.id as ID, c.full_name as FullName, c.email as Email,
                   c.phone_no as PhoneNo, c.address as Address,
                   c.cre_date as CreDate, c.mod_date as ModDate
            from customer c
            where lower(coalesce(c.id, '')) like lower({p}Keyword)
               or lower(coalesce(c.full_name, '')) like lower({p}Keyword)
               or lower(coalesce(c.email, '')) like lower({p}Keyword)
               or lower(coalesce(c.phone_no, '')) like lower({p}Keyword)
               or lower(coalesce(c.address, '')) like lower({p}Keyword)
            order by c.id";

        query = QueryLimitOffset(query);
        return await _command.GetRows<Customer>(transaction, query, new
        {
            Keyword = $"%{keyword}%",
            Offset = offset,
            Limit = limit
        });
    }

    public Task<Customer?> GetRow(IDbTransaction transaction, string id)
    {
        var query = $@"
            select c.id as ID, c.full_name as FullName, c.email as Email,
                   c.phone_no as PhoneNo, c.address as Address,
                   c.cre_date as CreDate, c.mod_date as ModDate
            from customer c
            where c.id = {db.Symbol()}ID";
        return _command.GetRow<Customer>(transaction, query, new { ID = id });
    }

    public Task<int> Insert(IDbTransaction transaction, Customer customer)
    {
        var p = db.Symbol();
        var query = $@"
            insert into customer (id, full_name, email, phone_no, address, cre_date, mod_date)
            values ({p}ID, {p}FullName, {p}Email, {p}PhoneNo, {p}Address, {p}CreDate, {p}ModDate)";
        return _command.Insert(transaction, query, customer);
    }

    public Task<int> UpdateByID(IDbTransaction transaction, Customer customer)
    {
        var p = db.Symbol();
        var query = $@"
            update customer
            set full_name = {p}FullName, email = {p}Email, phone_no = {p}PhoneNo,
                address = {p}Address, cre_date = {p}CreDate, mod_date = {p}ModDate
            where id = {p}ID";
        return _command.Update(transaction, query, customer);
    }

    public Task<int> DeleteByID(IDbTransaction transaction, string id)
    {
        var query = $"delete from customer where id = {db.Symbol()}ID";
        return _command.Delete(transaction, query, new { ID = id });
    }
}

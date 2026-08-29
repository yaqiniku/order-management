using System.Data;
using DAL.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DAL.Helper;

public class Database(AppDbContext context)
{
    private readonly string _connectionString = context.Database.GetConnectionString()
        ?? throw new InvalidOperationException("Connection string database tidak tersedia.");

    public IDbConnection GetConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    public string Symbol()
    {
        return "@";
    }

    public string Type()
    {
        return "PostgreSQL";
    }
}

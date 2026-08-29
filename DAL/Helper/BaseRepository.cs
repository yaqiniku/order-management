using System.Data;
using DAL.Data;

namespace DAL.Helper;

public abstract class BaseRepository
{
    protected readonly Command _command = new();
    protected readonly Database db;

    protected BaseRepository(AppDbContext context)
    {
        db = new Database(context);
    }

    protected virtual string TableBase => string.Empty;

    public IDbConnection GetDbConnection()
    {
        var connection = db.GetConnection();
        connection.Open();
        return connection;
    }

    protected string QueryLimit(string query, string limitParameter = "Limit")
    {
        EnsureOrderBy(query);
        return $"{query} offset 0 rows fetch first {db.Symbol()}{limitParameter} rows only";
    }

    protected string QueryLimitOffset(
        string query,
        string limitParameter = "Limit",
        string offsetParameter = "Offset")
    {
        EnsureOrderBy(query);
        var symbol = db.Symbol();

        return $"{query} offset {symbol}{offsetParameter} rows " +
               $"fetch next {symbol}{limitParameter} rows only";
    }

    protected string FormatDateTime(string column, string format = "DD-Mon-YYYY")
    {
        return $"to_char({column}, '{format}')";
    }

    protected string FormatNumeric(string column, int comma = 2)
    {
        var decimalFormat = comma > 0 ? $".{new string('0', comma)}" : string.Empty;
        return $"to_char({column}, 'FM999999999999990{decimalFormat}')";
    }

    private static void EnsureOrderBy(string query)
    {
        if (!query.Contains("order by", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Query must contain an 'order by' clause.");
        }
    }
}

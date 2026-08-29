using System.Data;
using System.Data.Common;
using System.Reflection;
using Npgsql;

namespace DAL.Helper;

public class Command
{
    public async Task<List<T>> GetRows<T>(
        IDbTransaction transaction,
        string query,
        object? parameters = null)
        where T : new()
    {
        using var command = CreateCommand(transaction, query, parameters);
        var dbCommand = (DbCommand)command;
        await using var reader = await dbCommand.ExecuteReaderAsync();

        var result = new List<T>();
        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            var row = new T();

            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (!properties.TryGetValue(reader.GetName(index), out var property) ||
                    await reader.IsDBNullAsync(index))
                {
                    continue;
                }

                property.SetValue(row, ConvertValue(reader.GetValue(index), property.PropertyType));
            }

            result.Add(row);
        }

        return result;
    }

    public async Task<T?> GetRow<T>(
        IDbTransaction transaction,
        string query,
        object? parameters = null)
        where T : new()
    {
        var rows = await GetRows<T>(transaction, query, parameters);
        return rows.FirstOrDefault();
    }

    public async Task<int> Execute(
        IDbTransaction transaction,
        string query,
        object? parameters = null)
    {
        try
        {
            using var command = CreateCommand(transaction, query, parameters);
            return await ((DbCommand)command).ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            throw SQLException(exception);
        }
    }

    public Task<int> Insert(
        IDbTransaction transaction,
        string query,
        object parameter)
    {
        return Execute(transaction, query, parameter);
    }

    public Task<int> Update(
        IDbTransaction transaction,
        string query,
        object parameter)
    {
        return Execute(transaction, query, parameter);
    }

    public Task<int> Delete(
        IDbTransaction transaction,
        string query,
        object parameter)
    {
        return Execute(transaction, query, parameter);
    }

    private static IDbCommand CreateCommand(
        IDbTransaction transaction,
        string query,
        object? parameters)
    {
        var command = transaction.Connection?.CreateCommand()
            ?? throw new InvalidOperationException("Transaction tidak memiliki connection aktif.");

        command.CommandText = query;
        command.Transaction = transaction;
        AddParameters(command, parameters);

        return command;
    }

    private static void AddParameters(IDbCommand command, object? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var property in parameters.GetType().GetProperties())
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = property.Name;
            parameter.Value = property.GetValue(parameters) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static object ConvertValue(object value, Type destinationType)
    {
        var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        return Convert.ChangeType(value, targetType);
    }

    private static Exception SQLException(Exception exception)
    {
        if (exception is not PostgresException postgresException)
        {
            return exception;
        }

        return postgresException.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => new InvalidOperationException(
                $"Data pada constraint '{postgresException.ConstraintName}' sudah tersedia.",
                postgresException),
            PostgresErrorCodes.NotNullViolation => new InvalidOperationException(
                $"Field '{postgresException.ColumnName}' wajib diisi.",
                postgresException),
            PostgresErrorCodes.ForeignKeyViolation => new InvalidOperationException(
                "Data referensi tidak ditemukan atau masih digunakan oleh data lain.",
                postgresException),
            _ => postgresException
        };
    }
}

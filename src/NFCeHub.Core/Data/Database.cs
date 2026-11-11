using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace NFCeHub.Core;

public static class Database
{
    private const int DefaultPort = 3050;

    public static ReadOnlySession Open(AppSettings settings = null)
    {
        settings ??= AppSettings.LoadRequired();
        var builder = new FbConnectionStringBuilder
        {
            DataSource = settings.Host,
            Port = int.TryParse(settings.Port.Trim(), out var port) ? port : DefaultPort,
            Database = settings.DatabasePath,
            UserID = settings.User,
            Password = settings.Password,
            Charset = "ISO8859_1",
            Dialect = 3,
            Pooling = false,
            ConnectionTimeout = 15,
        };
        return new ReadOnlySession(builder.ToString());
    }
}

public sealed class PreparedQuery : IDisposable
{
    private readonly FbCommand command;

    internal PreparedQuery(FbCommand command)
    {
        this.command = command;
        command.Prepare();
    }

    public object Scalar(params object[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            command.Parameters[i].Value = args[i];
        }
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    public void Dispose() => command.Dispose();
}

public sealed class ReadOnlySession : IDisposable
{
    private readonly FbConnection connection;
    private readonly FbTransaction transaction;

    public ReadOnlySession(string connectionString)
    {
        connection = new FbConnection(connectionString);
        connection.Open();
        try
        {
            transaction = connection.BeginTransaction(new FbTransactionOptions
            {
                TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.ReadCommitted |
                                      FbTransactionBehavior.RecVersion | FbTransactionBehavior.Wait,
            });
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public List<object[]> Rows(string sql, params object[] args)
    {
        var rows = new List<object[]>();
        using var command = Command(sql, args);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] is DBNull)
                {
                    row[i] = null;
                }
            }
            rows.Add(row);
        }
        return rows;
    }

    public PreparedQuery Prepare(string sql, params object[] sampleArgs) => new(Command(sql, sampleArgs));

    public void Dispose()
    {
        try
        {
            if (connection.State == ConnectionState.Open)
            {
                transaction.Rollback();
            }
        }
        finally
        {
            transaction.Dispose();
            connection.Dispose();
        }
    }

    private FbCommand Command(string sql, object[] args)
    {
        var command = new FbCommand(sql, connection, transaction);
        for (var i = 0; i < args.Length; i++)
        {
            command.Parameters.AddWithValue("@p" + i, args[i]);
        }
        return command;
    }
}

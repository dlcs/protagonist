﻿using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DLCS.Repository;

/// <summary>
/// Helper methods for using DB Connections.
/// </summary>
public static class DatabaseConnectionManager
{
    /// <summary>
    /// Get PostgreSQLConnection string from config.
    /// </summary>
    /// <param name="configuration">Current <see cref="IConfiguration"/> object.</param>
    /// <returns>Connection string for PostgreSQL instance.</returns>
    public static string GetPostgresSqlConnection(this IConfiguration configuration)
        => configuration.GetConnectionString("PostgreSQLConnection");
    
    /// <summary>
    /// Get open connection to Postgres db.
    /// </summary>
    /// <param name="configuration">Current <see cref="IConfiguration"/> object.</param>
    /// <returns>Open <see cref="NpgsqlConnection"/> connection.</returns>
    public static async Task<NpgsqlConnection> GetOpenNpgSqlConnection(IConfiguration configuration)
    {
        var connection = new NpgsqlConnection(configuration.GetPostgresSqlConnection());
        await connection.OpenAsync();
        return connection;
    }
    
    /// <summary>
    /// Get open connection to Postgres db.
    /// </summary>
    /// <param name="dlcsContext">Current <see cref="DlcsContext"/> object.</param>
    /// <returns>Open <see cref="NpgsqlConnection"/> connection.</returns>
    public static async Task<NpgsqlConnection> GetOpenNpgSqlConnection(this DlcsContext dlcsContext)
    {
        var connection = dlcsContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        return (NpgsqlConnection)connection;
    }
}
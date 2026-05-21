using ChurrOS.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ChurrOS.Api.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class HypertableColumnAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)]
    public class HypertablePartitionColumnAttribute : Attribute { }

    public static class TimeScaleExtensions
    {
        public static async Task ApplyHypertablesAsync(this ChurrosDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;");
            var entityTypes = context.Model.GetEntityTypes();
            foreach (var entityType in entityTypes)
            {
                string? tableName = entityType.GetTableName();
                string? columnName = null;
                string? partitionColumnName = null;
                foreach (var property in entityType.GetProperties())
                {
                    if (property.PropertyInfo?.GetCustomAttribute(typeof(HypertableColumnAttribute)) != null)
                    {
                        columnName = property.GetColumnName();
                    }
                    if (property.PropertyInfo?.GetCustomAttribute(typeof(HypertablePartitionColumnAttribute)) != null)
                    {
                        partitionColumnName = property.GetColumnName();
                    }
                }
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(tableName, nameof(tableName));
                    ArgumentException.ThrowIfNullOrWhiteSpace(columnName, nameof(columnName));
                    ArgumentException.ThrowIfNullOrWhiteSpace(partitionColumnName, nameof(partitionColumnName));

                    string sqlQuery = $"""
                        SELECT EXISTS (
                            SELECT 1
                            FROM timescaledb_information.hypertables
                            WHERE hypertable_schema = 'cs'
                              AND hypertable_name = '{tableName}'
                        );
                        """;
                    var exists = await context.ExecuteScalarAsync<bool>(sqlQuery);

                    if (!exists)
                    {
                        sqlQuery = $"""
                            SELECT create_hypertable(
                                'cs.{tableName}',
                                time_column_name => '{columnName}',
                                partitioning_column => '{partitionColumnName}',
                                number_partitions => 16,
                                chunk_time_interval => INTERVAL '1 day'
                            );
                            """;
                        await context.Database.ExecuteSqlRawAsync(sqlQuery);
                        sqlQuery = $"""
                            ALTER TABLE cs.{tableName}
                            SET (
                              timescaledb.compress,
                              timescaledb.compress_segmentby = '{partitionColumnName}',
                              timescaledb.compress_orderby = '{columnName} DESC'
                            );
                            """;
                        await context.Database.ExecuteSqlRawAsync(sqlQuery);
                        sqlQuery = $"SELECT add_retention_policy('cs.{tableName}', INTERVAL '90 days');";
                        await context.Database.ExecuteSqlRawAsync(sqlQuery);
                    }
                }
            }
        }
    }
}

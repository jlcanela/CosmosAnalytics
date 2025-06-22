using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;

namespace ApiServices
{
    public class ReportingService
    {
        private readonly ILogger<ReportingService> _logger;

        public ReportingService(ILogger<ReportingService> logger)
        {
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object>>> RunReportAsync(string filePath, string sqlQuery)
        {
            var results = new List<Dictionary<string, object>>();

            using (var connection = new DuckDBConnection("Data Source=:memory:"))
            {
                await connection.OpenAsync();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $@"
                        CREATE TEMP TABLE projects AS 
                        SELECT * 
                        FROM read_json_auto('{filePath}',
                             format='newline_delimited',
                             compression='AUTO_DETECT'
                        );";
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sqlQuery;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader.GetValue(i);
                            }
                            results.Add(row);
                        }
                    }
                }
            }

            _logger.LogInformation($"Executed report on {filePath}");
            return results;
        }
    }
}

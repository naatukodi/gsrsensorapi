

// GetSensorTimeSeries.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

// Models.cs
public class TimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public int Humidity { get; set; }
}


namespace GSRSensorAPI
{
    public class GetSensorTimeSeries
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _tables;

        public GetSensorTimeSeries(ILoggerFactory loggerFactory,
                                   TableServiceClient tables)
        {
            _logger = loggerFactory.CreateLogger<GetSensorTimeSeries>();
            _tables = tables;
        }

        [Function("GetSensorTimeSeries")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "SensorTimeSeries/{sensorName}/{period}")]
            HttpRequestData req,
            string sensorName,
            string period)    // "hour" or "day"
        {
            _logger.LogInformation($"Fetching time‐series for '{sensorName}' over last {period}.");

            var span = period.Equals("day", StringComparison.OrdinalIgnoreCase)
                       ? TimeSpan.FromDays(1)
                       : TimeSpan.FromHours(1);

            var cutoff = DateTime.UtcNow.Subtract(span);

            var tableClient = _tables.GetTableClient("SensorData");
            await tableClient.CreateIfNotExistsAsync();

            // Filter by sensor and timestamp
            string filter =
              $"PartitionKey eq '{sensorName}' and Timestamp ge datetime'{cutoff:o}'";

            var points = new List<TimeSeriesPoint>();
            await foreach (var e in tableClient.QueryAsync<TableEntity>(filter))
            {
                points.Add(new TimeSeriesPoint
                {
                    Timestamp = e.Timestamp!.Value.UtcDateTime,
                    Temperature = e.GetDouble("Temperature") ?? 0.0,
                    Humidity = e.GetInt32("Humidity") ?? 0
                });
            }

            // Sort ascending by time
            points = points.OrderBy(p => p.Timestamp).ToList();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(points);
            return resp;
        }
    }
}

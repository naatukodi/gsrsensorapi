using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GSRSensorAPI
{
    public class GetSensorAverages
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _tables;

        public GetSensorAverages(ILoggerFactory loggerFactory,
                                 TableServiceClient tables)
        {
            _logger = loggerFactory.CreateLogger<GetSensorAverages>();
            _tables = tables;
        }

        [Function("GetSensorAverages")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "SensorAverages/{sensorName}")]
            HttpRequestData req,
            string sensorName)
        {
            _logger.LogInformation($"Calculating averages for sensor '{sensorName}'.");

            var tableClient = _tables.GetTableClient("SensorData");
            await tableClient.CreateIfNotExistsAsync();

            var now = DateTime.UtcNow;
            var oneHourAgo = now.AddHours(-1);
            var dayAgo = now.AddDays(-1);

            // helper to compute avg from a time window
            async Task<(double? temp, double? hum)> AvgBetween(DateTime since)
            {
                double sumT = 0, sumH = 0; int count = 0;
                string filter =
                  $"PartitionKey eq '{sensorName}' and Timestamp ge datetime'{since:o}'";
                await foreach (var e in tableClient.QueryAsync<TableEntity>(filter))
                {
                    sumT += e.GetDouble("Temperature") ?? 0;
                    sumH += (double)(e.GetInt32("Humidity") ?? 0);
                    count++;
                }
                if (count == 0) return (null, null);
                return (sumT / count, sumH / count);
            }

            var (tHour, hHour) = await AvgBetween(oneHourAgo);
            var (tDay, hDay) = await AvgBetween(dayAgo);

            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(new
            {
                sensorName,
                avgLastHour = new { temperature = tHour, humidity = hHour },
                avgLastDay = new { temperature = tDay, humidity = hDay }
            });
            return resp;
        }
    }
}

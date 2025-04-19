using System;
using System.IO;
using System.Text.Json;
using System.Net;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GSRSensorAPI
{
    // Define a simple POCO matching your payload
    public class SensorReading
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Temperature { get; set; }
        public int Humidity { get; set; }
    }

    public class GSRFunction
    {
        private readonly ILogger _logger;
        private readonly TableServiceClient _tables;

        public GSRFunction(ILoggerFactory loggerFactory,
                           TableServiceClient tables)
        {
            _logger = loggerFactory.CreateLogger<GSRFunction>();
            _tables = tables;
        }

        [Function("GSRFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]
            HttpRequestData req)
        {
            _logger.LogInformation("Received sensor payload, creating new Table entry.");

            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var reading = JsonSerializer.Deserialize<SensorReading>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (reading == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("⚠️ Invalid JSON payload");
                return bad;
            }

            var tableClient = _tables.GetTableClient("SensorData");
            await tableClient.CreateIfNotExistsAsync();

            // Use sensor Name for PK, and a timestamp+GUID for RK to ensure uniqueness
            string rowKey = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";

            var entity = new TableEntity(reading.Name, rowKey)
            {
                ["SensorId"]    = reading.Id,
                ["SensorName"]  = reading.Name,
                ["Temperature"] = reading.Temperature,
                ["Humidity"]    = reading.Humidity,
                ["Timestamp"]   = DateTime.UtcNow
            };

            // Insert new entity; will throw if a duplicate PartitionKey+RowKey exists (unlikely)
            await tableClient.AddEntityAsync(entity);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteStringAsync("✅ New sensor reading added to table ‘SensorData’");
            return ok;
        }

    }
}

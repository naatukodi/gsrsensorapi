using System;
using System.IO;
using System.Text.Json;
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
            _logger.LogInformation("Received sensor payload, writing to Table storage.");

            // 1. Read & deserialize
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var reading = JsonSerializer.Deserialize<SensorReading>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (reading == null)
            {
                var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("⚠️ Invalid JSON payload");
                return bad;
            }

            // 2. Get (or create) the table
            var tableClient = _tables.GetTableClient("SensorData");
            await tableClient.CreateIfNotExistsAsync();

            // 3. Prepare a TableEntity
            //    PartitionKey = sensor Name, RowKey = reading Id
            var entity = new TableEntity(reading.Name, reading.Id.ToString())
            {
                ["Id"] = reading.Id,          // ← new
                ["Name"] = reading.Name,        // ← new
                ["Temperature"] = reading.Temperature,
                ["Humidity"] = reading.Humidity,
                ["Timestamp"] = DateTime.UtcNow
            };


            // 4. Upsert (insert or replace)
            await tableClient.UpsertEntityAsync(entity);

            // 5. Return OK
            var ok = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await ok.WriteStringAsync("✅ Sensor data written to table ‘SensorData’");
            return ok;
        }
    }
}

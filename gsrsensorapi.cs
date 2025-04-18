using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GSRSensorAPI
{
    public class GSRFunction
    {
        private readonly ILogger _logger;
        private readonly BlobServiceClient _blobService;

        public GSRFunction(ILoggerFactory loggerFactory, BlobServiceClient blobService)
        {
            _logger = loggerFactory.CreateLogger<GSRFunction>();
            _blobService = blobService;
        }

        [Function("GSRFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]
            HttpRequestData req)
        {
            _logger.LogInformation("Received request, writing to Blob storage.");

            // Read request body
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            // Create container and blob client
            var container = _blobService.GetBlobContainerClient("incoming-data");
            await container.CreateIfNotExistsAsync();
            var blobClient = container.GetBlobClient($"{Guid.NewGuid():N}.json");

            // Upload the JSON payload
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
            await blobClient.UploadAsync(ms);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteStringAsync("✅ Data written to Blob storage.");
            return response;
        }
    }
}

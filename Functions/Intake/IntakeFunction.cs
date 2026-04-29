using AF.Domain.Documents;
using AF.Infrastructure.Requests;
using AF.Infrastructure.Storage;
using Azure;
using Azure.Data.Tables;
using Azure.AI.DocumentIntelligence;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AF.Functions.Intake;

public class IntakeFunction
{
    private readonly ILogger<IntakeFunction> _logger;
    private readonly IConfiguration _configuration;

    public IntakeFunction(
        ILogger<IntakeFunction> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("IntakeFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req
        )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Request body is empty");
                return badResponse;
            }

            var intakeRequest = JsonSerializer.Deserialize<IntakeRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (intakeRequest is null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid JSON payload");
                return badResponse;
            }


            if (string.IsNullOrWhiteSpace(intakeRequest.FileUrl) ||
                string.IsNullOrWhiteSpace(intakeRequest.FileName) ||
                string.IsNullOrWhiteSpace(intakeRequest.ClientType) ||
                string.IsNullOrWhiteSpace(intakeRequest.RegulationScope))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing required fields: fileUrl, fileName, clientType, regulationScope");
                return badResponse;
            }


            var storageConnectionString = _configuration["AzureWebJobsStorage"];
            var legajosStorageConnectionString = _configuration["LegajosStorageConnectionString"];

            var configTableName = _configuration["ConfigMappingTableName"] ?? "ConfigMapping";
            var stateTableName = _configuration["DocumentStateTableName"] ?? "DocumentState";
            var docIntelEndpoint = _configuration["DocumentIntelligence:Endpoint"];
            var docIntelApiKey = _configuration["DocumentIntelligence:ApiKey"];


            if (string.IsNullOrWhiteSpace(legajosStorageConnectionString))
            {
                _logger.LogInformation("Missing LegajosStorageConnectionString ");

                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteStringAsync("Missing LegajosStorageConnectionString.");
                return badResponse;
            }

            if (string.IsNullOrWhiteSpace(docIntelEndpoint) || string.IsNullOrWhiteSpace(docIntelApiKey))
            {
                _logger.LogInformation("Missing Document Intelligence configuration ");

                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteStringAsync("Missing Document Intelligence configuration");
                return badResponse;
            }

            var configTableClient = new TableClient(legajosStorageConnectionString, configTableName);
            var stateTableClient = new TableClient(legajosStorageConnectionString, stateTableName);

            // Create tables if not created
            await configTableClient.CreateIfNotExistsAsync();
            await stateTableClient.CreateIfNotExistsAsync();

            // Get config using clientType and regulationScope
            var configResponse = await configTableClient.GetEntityIfExistsAsync<ConfigMappingEntity>(
                intakeRequest.ClientType.Trim().ToLower(),
                intakeRequest.RegulationScope.Trim().ToLower());

            if (!configResponse.HasValue || configResponse.Value is null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await badResponse.WriteStringAsync("No config mapping found for clientType '{intakeRequest.ClientType}' and regulationScope '{intakeRequest.RegulationScope}'.");
                return badResponse;
            }

            var config = configResponse.Value;

            if (!config.IsActive)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await badResponse.WriteStringAsync("Config mapping for clientType '{intakeRequest.ClientType}' and regulationScope '{intakeRequest.RegulationScope}' is inactive.");
                return badResponse;
            }

            if (string.IsNullOrWhiteSpace(config.ClassifierId))
            {
                _logger.LogInformation("Config mapping does not contain a ClassifierId. ");
                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteStringAsync("Config mapping does not contain a ClassifierId.");
                return badResponse;
            }

            // Simple parse to get the allowed models
            var extractionModelMap = ParseModelMap(config.ExtractionModelMapJson);
            if (extractionModelMap.Count == 0)
            {
                _logger.LogInformation("Config mapping does not contain a valid extraction model map. ");
                var badResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await badResponse.WriteStringAsync("Config mapping does not contain a valid extraction model map.");
                return badResponse;
            }

            var tenantId = !string.IsNullOrWhiteSpace(intakeRequest.TenantId) ? intakeRequest.TenantId! : intakeRequest.ClientType!;
            var documentId = BuildDocumentId(intakeRequest.FileName!);

            // Upsert the initial state
            var stateEntity = new DocumentStateEntity
            {
                PartitionKey = tenantId,
                RowKey = documentId,
                FileName = intakeRequest.FileName,
                BlobPath = intakeRequest.BlobPath,
                FileUrl = intakeRequest.FileUrl,
                ClientType = intakeRequest.ClientType,
                RegulationScope = intakeRequest.RegulationScope,
                Status = "classifying",
                Message = "Invoking Document Intelligence Classifier.",
                ClassifierId = config.ClassifierId,
                AllowedExtractionModels = JsonSerializer.Serialize(extractionModelMap.Keys),
                ConfidenceThreshold = config.ConfidenceThreshold,
                RoutingProfile = config.RoutingProfile,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await stateTableClient.UpsertEntityAsync(stateEntity);

            var client = new DocumentIntelligenceClient(new Uri(docIntelEndpoint), new AzureKeyCredential(docIntelApiKey));

            var classifyOptions = new ClassifyDocumentOptions(
                config.ClassifierId,
                new Uri(GenerateBlobSasUrl(intakeRequest.FileUrl))
            );

            var classifyOperation = await client.ClassifyDocumentAsync(
                WaitUntil.Completed,
                classifyOptions
            );

            var classifyResult = classifyOperation.Value;

            if (classifyResult.Documents is null || classifyResult.Documents.Count == 0)
            {
                stateEntity.Status = "failed";
                stateEntity.Message = "Classifier returned no documents.";
                stateEntity.UpdatedAt = DateTimeOffset.UtcNow;
                await stateTableClient.UpsertEntityAsync(stateEntity);
                var badResponse = req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                await badResponse.WriteStringAsync("Classifier returned no detected document types.");
                return badResponse;
            }

            var firstDetected = classifyResult.Documents[0];
            var detectedType = firstDetected.DocumentType;
            var detectedConfidence = firstDetected.Confidence;

            if (string.IsNullOrWhiteSpace(detectedType))
            {
                stateEntity.Status = "failed";
                stateEntity.Message = "Classifier returned an empty document type.";
                stateEntity.UpdatedAt = DateTimeOffset.UtcNow;
                await stateTableClient.UpsertEntityAsync(stateEntity);
                var badResponse = req.CreateResponse(HttpStatusCode.UnprocessableEntity);
                await badResponse.WriteStringAsync("Classifier returned an empty document type.");
                return badResponse;
            }

            if (detectedConfidence < config.ConfidenceThreshold)
            {
                stateEntity.Status = "manual_review";
                stateEntity.Message = $"Classification confidence below threshold. Detected={detectedType} Confidence={detectedConfidence}.";
                stateEntity.UpdatedAt = DateTimeOffset.UtcNow;
                await stateTableClient.UpsertEntityAsync(stateEntity);
                var lowConfidenceResponse = req.CreateResponse(HttpStatusCode.OK);
                await lowConfidenceResponse.WriteAsJsonAsync(new
                {
                    message = "Classification completed, but confidence is below threshold",
                    documentId,
                    tenantId,
                    fileName = intakeRequest.FileName,
                    detectedType,
                    detectedConfidence,
                    threshold = config.ConfidenceThreshold,
                    nextAction = "manual_review"
                });
                return lowConfidenceResponse;
            }

            if (!extractionModelMap.TryGetValue(detectedType, out var extractionModelId))
            {
                stateEntity.Status = "manual_review";
                stateEntity.Message = $"Detected type '{detectedType}' is not mapped to an extraction model for this client/regulation route.";
                stateEntity.UpdatedAt = DateTimeOffset.UtcNow;
                await stateTableClient.UpsertEntityAsync(stateEntity);
                var unmappedResponse = req.CreateResponse(HttpStatusCode.OK);
                await unmappedResponse.WriteAsJsonAsync(new
                {
                    message = "Classification completed, but detected type is not mapped to an extraction model.",
                    documentId,
                    tenantId,
                    fileName = intakeRequest.FileName,
                    detectedType,
                    detectedConfidence,
                    nextAction = "manual_review"
                });
                return unmappedResponse;
            }

            stateEntity.Status = "classified";
            stateEntity.Message = $"Detected type '{detectedType}' routed to extraction model '{extractionModelId}'.";
            stateEntity.UpdatedAt = DateTimeOffset.UtcNow;
            await stateTableClient.UpsertEntityAsync(stateEntity);

            var extractOptions = new AnalyzeDocumentOptions(
                extractionModelId,
                new Uri(GenerateBlobSasUrl(intakeRequest.FileUrl))
            );

            var extractOperation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                extractOptions
                );

            var extractResult = extractOperation.Value;

            var adapter = GetAdapterForDetectedType(detectedType);
            var canonicalResult = adapter.Transform(extractResult, extractionModelId);

            canonicalResult.ClientType = intakeRequest.ClientType;
            canonicalResult.RegulationScope = intakeRequest.RegulationScope;
            canonicalResult.Confidence = detectedConfidence;
            canonicalResult.DocumentType = detectedType;

            stateEntity.Status = "extracted";
            stateEntity.Message = $"Extraction completed with model '{extractionModelId}'";
            stateEntity.UpdatedAt = DateTimeOffset.UtcNow;
            await stateTableClient.UpsertEntityAsync(stateEntity);


            var okResponse = req.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteAsJsonAsync(new
            {
                message = "Classification completed successfully",
                documentId,
                tenantId,
                fileName = intakeRequest.FileName,
                fileUrl = intakeRequest.FileUrl,
                clientType = intakeRequest.ClientType,
                regulationScope = intakeRequest.RegulationScope,
                classifierId = config.ClassifierId,
                detectedType,
                detectedConfidence,
                extractionModelId,
                canonicalResult
            });

            return okResponse;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while processing IntakeFunction request.");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = exception.Message,
                stackTrace = exception.StackTrace
            });
            return errorResponse;
        }
    }

    private static Dictionary<string, string> ParseModelMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return result is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IExtractionResultAdapter GetAdapterForDetectedType(string detectedType)
    {
        return detectedType.ToLowerInvariant() switch
        {
            "identidad" => new IdentityAdapter(),
            _ => new DefaultExtractionAdapter()
        };
    }

    private string GenerateBlobSasUrl(string blobUrl)
    {
        var storageConnectionString = _configuration["LegajosStorageConnectionString"];
        var serviceClient = new BlobServiceClient(storageConnectionString);

        var blobUriBuilder = new BlobUriBuilder(new Uri(blobUrl));

        var containerClient = serviceClient.GetBlobContainerClient(blobUriBuilder.BlobContainerName);
        var blobClient = containerClient.GetBlobClient(blobUriBuilder.BlobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = blobUriBuilder.BlobContainerName,
            BlobName = blobUriBuilder.BlobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri.ToString();
    }

    private static string BuildDocumentId(string fileName)
    {
        var safeName = fileName.Replace(" ", "_");
        return $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeName}";
    }
}

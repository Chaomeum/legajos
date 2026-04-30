using Azure.Data.Tables;
using AF.Domain.Documents;
using AF.Domain.Validation;
using AF.Infrastructure.Requests;
using AF.Infrastructure.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AF.Functions.Validation;

public class ValidationFunction
{
    private readonly ILogger<ValidationFunction> _logger;
    private readonly IConfiguration _configuration;

    public ValidationFunction(
        ILogger<ValidationFunction> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("ValidationFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<ValidationRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
                return Error(req, HttpStatusCode.BadRequest, "Invalid request.");

            var partitionKey = BuildPartitionKey(request);
            var resultsTable = new TableClient(
                _configuration["LegajosStorageConnectionString"],
                "ValidationResults");
            var tableClient = new TableClient(
                _configuration["LegajosStorageConnectionString"],
                "ValidationCriteria");

            await resultsTable.CreateIfNotExistsAsync();

            var criteria = tableClient
                .Query<ValidationCriterionEntity>(c =>
                    c.PartitionKey == partitionKey &&
                    c.IsActive &&
                    c.Phase == "Fase1")
                .ToList();

            var results = new List<ValidationResult>();

            foreach (var criterion in criteria)
            {
                var result = EvaluateCriterion(criterion, request.ExtractedDocuments);
                results.Add(result);
            }

            var now = DateTimeOffset.UtcNow;

            foreach (var result in results)
            {
                var entity = new ValidationResultEntity
                {
                    PartitionKey = request.CaseId,
                    RowKey = $"{request.SampleId}|{result.CriterionId}",
                    CaseId = request.CaseId,
                    SampleId = request.SampleId,
                    CriterionId = result.CriterionId,
                    Status = result.Status,
                    Evidence = result.Evidence,
                    Observation = result.Observation,

                    ValidatedAt = now
                };

                await resultsTable.UpsertEntityAsync(entity);
            }


            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                request.CaseId,
                request.SampleId,
                ValidationResults = results
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation error");
            return Error(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    // ---------------- Validation Logic ----------------

    private static ValidationResult EvaluateCriterion(
        ValidationCriterionEntity criterion,
        List<CanonicalExtractionResult> documents)
    {
        return criterion.ValidationType switch
        {
            "DocumentExists" => ValidateDocumentExists(criterion, documents),
            "RequiredFields" => ValidateRequiredFields(criterion, documents),
            _ => NA(criterion)
        };
    }

    private static ValidationResult ValidateDocumentExists(
        ValidationCriterionEntity criterion,
        List<CanonicalExtractionResult> documents)
    {
        var docTypes = (criterion.RequiredDocumentTypes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim());

        var found = documents
            .FirstOrDefault(d => docTypes.Contains(d.DocumentType, StringComparer.OrdinalIgnoreCase));

        return found != null
            ? OK(criterion, found.DocumentType)
            : NOK(criterion, $"Missing document: {criterion.RequiredDocumentTypes}");
    }

    private static ValidationResult ValidateRequiredFields(
        ValidationCriterionEntity criterion,
        List<CanonicalExtractionResult> documents)
    {
        var doc = documents.FirstOrDefault(d =>
            d.DocumentType.Equals(criterion.DocumentType, StringComparison.OrdinalIgnoreCase));

        if (doc == null)
            return NOK(criterion, $"Document {criterion.DocumentType} not found");

        var fields = (criterion.RequiredFields ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim());

        var missing = fields
            .Where(f => !doc.Fields.ContainsKey(f) || string.IsNullOrWhiteSpace(doc.Fields[f]?.ToString()))
            .ToList();

        return !missing.Any()
            ? OK(criterion, criterion.DocumentType)
            : NOK(criterion, $"Missing fields: {string.Join(", ", missing)}");
    }

    // ---------------- Helpers ----------------

    private static string BuildPartitionKey(ValidationRequest req)
        => $"{req.Regulation}|{req.PersonType}|{req.PersonSubType}|{req.Regime}";

    private static ValidationResult OK(ValidationCriterionEntity c, string evidence)
        => new()
        {
            CriterionId = c.RowKey,
            Description = c.Description,
            Status = "OK",
            Evidence = evidence
        };

    private static ValidationResult NOK(ValidationCriterionEntity c, string obs)
        => new()
        {
            CriterionId = c.RowKey,
            Description = c.Description,
            Status = "NOK",
            Observation = obs
        };

    private static ValidationResult NA(ValidationCriterionEntity c)
        => new()
        {
            CriterionId = c.RowKey,
            Description = c.Description,
            Status = "NA",
            Observation = "Not applicable in current phase"
        };

    private static HttpResponseData Error(HttpRequestData req, HttpStatusCode code, string msg)
    {
        var res = req.CreateResponse(code);
        res.WriteAsJsonAsync(new { error = msg });
        return res;
    }
}
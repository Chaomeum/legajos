using System.Net;
using System.Text.Json;
using AF.Infrastructure.Requests;
using ClosedXML.Excel;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AF.Functions.Export;

public class ExportExcelFunction
{
    private readonly ILogger<ExportExcelFunction> _logger;
    private readonly IConfiguration _configuration;

    public ExportExcelFunction(
        ILogger<ExportExcelFunction> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("ExportExcelFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<ExportExcelRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null || request.Samples == null || !request.Samples.Any())
            {
                return CreateError(req, HttpStatusCode.BadRequest, "Invalid export request.");
            }


            var fileName = BuildFileName(
                        request.Regulation,
                        request.TenantId,
                        request.PersonType,
                        request.PersonSubType);

            var excelStream = BuildExcel(request);

            var blobUrl = await SaveToBlobAsync(
                excelStream,
                request,
                fileName);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                status = "success",
                fileName,
                downloadUrl = blobUrl
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Excel");
            return CreateError(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    // ---------------- Helpers ----------------
    private const string EyYellow = "FFE600";
    private const string EyFont = "EyInterstate Regular";
    private static MemoryStream BuildExcel(ExportExcelRequest req)
    {
        using var wb = new XLWorkbook();

        BuildResumenSheet(wb, req);
        BuildDocumentSheets(wb, req);

        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void BuildResumenSheet(XLWorkbook wb, ExportExcelRequest req)
    {
        var ws = wb.Worksheets.Add("Resumen");

        var samplesCount = req.Samples.Count;

        // Header padre
        ws.Cell(1, 1).Value = "Información verificada";
        ws.Range(1, 1, 2, 1).Merge();

        ws.Cell(1, 2).Value = "Número de muestra";
        ws.Range(1, 2, 1, 1 + samplesCount).Merge();

        // Header hijos (Muestra 1..N)
        for (int i = 0; i < samplesCount; i++)
        {
            ws.Cell(2, 2 + i).Value = $"{req.Samples[i].SampleId}";
        }

        var criteria = req.Samples.First().ValidationResults;

        var row = 3;
        foreach (var criterion in criteria)
        {
            ws.Cell(row, 1).Value = criterion.Description;

            for (int s = 0; s < samplesCount; s++)
            {
                var sampleResult = req.Samples[s]
                    .ValidationResults
                    .FirstOrDefault(r => r.CriterionId == criterion.CriterionId);

                ws.Cell(row, 2 + s).Value = sampleResult?.Status switch
                {
                    "OK" => "✅",
                    "NOK" => "❌",
                    _ => "NA"
                };
            }

            row++;
        }

        var used = ws.RangeUsed();
        if (used is null) return;

        ApplyBodyStyle(used);
        ApplyHeaderStyle(ws.Range(1, 1, 2, 1 + samplesCount));

        ws.Range(3, 1, row - 1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ws.Range(3, 2, row - 1, 1 + samplesCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.SheetView.FreezeRows(2);
        ws.SheetView.FreezeColumns(1);

        ws.Columns().AdjustToContents();
    }


    private static void BuildDocumentSheets(XLWorkbook wb, ExportExcelRequest req)
    {
        var samples = req.Samples;

        var allDocs = samples
            .SelectMany(s => s.ExtractedDocuments)
            .Select(d => d.DocumentType)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var docType in allDocs)
        {
            var ws = wb.Worksheets.Add(docType.ToUpper());

            ws.Cell(1, 1).Value = "Campo";
            ws.Cell(1, 1).Style.Font.Bold = true;

            for (int s = 0; s < samples.Count; s++)
            {
                ws.Cell(1, 2 + s).Value = $"Muestra {samples[s].SampleId}";
                ws.Cell(1, 2 + s).Style.Font.Bold = true;
            }

            var fields = samples
                .SelectMany(s => s.ExtractedDocuments)
                .Where(d => d.DocumentType.Equals(docType, StringComparison.OrdinalIgnoreCase))
                .SelectMany(d => d.Fields.Keys)
                .Distinct();

            var row = 2;
            foreach (var field in fields)
            {
                ws.Cell(row, 1).Value = field;

                for (int s = 0; s < samples.Count; s++)
                {
                    var doc = samples[s].ExtractedDocuments
                        .FirstOrDefault(d => d.DocumentType.Equals(docType, StringComparison.OrdinalIgnoreCase));

                    ws.Cell(row, 2 + s).Value =
                        doc?.Fields.TryGetValue(field, out var value) == true
                            ? value?.ToString() ?? string.Empty
                            : string.Empty;
                }

                row++;
            }

            var used = ws.RangeUsed();
            if (used is null) continue;
            ApplyBodyStyle(used);
            ApplyHeaderStyle(ws.Range(1, 1, 1, samples.Count + 1));

            used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.SheetView.FreezeRows(1);
            ws.SheetView.FreezeColumns(1);

            ws.Columns().AdjustToContents();
        }
    }

    private static void ApplyHeaderStyle(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml($"#{EyYellow}");
        range.Style.Font.FontName = EyFont;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyBodyStyle(IXLRange range)
    {
        range.Style.Font.FontName = EyFont;
        range.Style.Font.Bold = false;
        range.Style.Fill.BackgroundColor = XLColor.NoColor;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private async Task<string> SaveToBlobAsync(
        MemoryStream stream,
        ExportExcelRequest req,
        string fileName)
    {
        var connStr = _configuration["LegajosStorageConnectionString"];
        var containerName = "output";

        var blobService = new BlobServiceClient(connStr);
        var container = blobService.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        var now = DateTime.UtcNow;
        var blobPath =
            $"{req.TenantId}/{req.Regulation}/{now:yyyy/MM}/excel/{fileName}";

        var blobClient = container.GetBlobClient(blobPath);
        await blobClient.UploadAsync(stream, overwrite: true);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri.ToString();

    }

    private static string BuildFileName(
            string regulation,
            string tenant,
            string personType,
            string personSubType)
    {
        return $"{regulation.ToUpper()}_{tenant}_{personType}_{personSubType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
    }

    private static HttpResponseData CreateError(
        HttpRequestData req,
        HttpStatusCode code,
        string message)
    {
        var response = req.CreateResponse(code);
        response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}
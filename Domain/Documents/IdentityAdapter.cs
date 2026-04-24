using Azure.AI.DocumentIntelligence;

namespace AF.Domain.Documents;

public class IdentityAdapter : IExtractionResultAdapter
{
    public CanonicalExtractionResult Transform(AnalyzeResult analyzeResult, string extractionModelId)
    {
        var result = new CanonicalExtractionResult
        {
            DocumentType = "identidad",
            ExtractionModelId = extractionModelId
        };

        var doc = analyzeResult.Documents.FirstOrDefault();
        if (doc is null)
            return result;

        foreach (var field in doc.Fields)
        {
            result.Fields[field.Key] = ExtractFieldValue(field.Value);
        }
        return result;
    }

    private static object? ExtractFieldValue(DocumentField field)
    {
        if (!string.IsNullOrWhiteSpace(field.Content))
            return field.Content;

        if (field.FieldType == DocumentFieldType.String)
            return field.ValueString;

        if (field.FieldType == DocumentFieldType.Date)
            return field.ValueDate?.ToString("yyyy-MM-dd");

        if (field.FieldType == DocumentFieldType.Time)
            return field.ValueTime?.ToString();

        if (field.FieldType == DocumentFieldType.Double)
            return field.ValueDouble;

        if (field.FieldType == DocumentFieldType.Int64)
            return field.ValueInt64;

        if (field.FieldType == DocumentFieldType.PhoneNumber)
            return field.ValuePhoneNumber;

        if (field.FieldType == DocumentFieldType.CountryRegion)
            return field.ValueCountryRegion;

        if (field.FieldType == DocumentFieldType.Currency)
            return field.ValueCurrency;

        if (field.FieldType == DocumentFieldType.Address)
            return field.ValueAddress;

        if (field.FieldType == DocumentFieldType.Dictionary)
        {
            var dict = new Dictionary<string, object?>();
            if (field.ValueDictionary is not null)
            {
                foreach (var kvp in field.ValueDictionary)
                {
                    dict[kvp.Key] = ExtractFieldValue(kvp.Value);
                }
            }
            return dict;
        }

        if (field.FieldType == DocumentFieldType.List)
        {
            var list = new List<object?>();
            if (field.ValueList is not null)
            {
                foreach (var item in field.ValueList)
                {
                    list.Add(ExtractFieldValue(item));
                }
            }
            return list;
        }
        return field.Content;
    }
}
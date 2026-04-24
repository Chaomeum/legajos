using Azure.AI.DocumentIntelligence;

namespace AF.Domain.Documents;

public interface IExtractionResultAdapter
{
    CanonicalExtractionResult Transform(AnalyzeResult analyzeResult, string extractionModelId);
}
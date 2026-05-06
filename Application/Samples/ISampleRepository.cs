using AF.Domain.Samples;

namespace AF.Application.Samples;

public interface ISampleRepository
{
    Task<SampleStateRecord?> GetAsync(string caseId, string sampleId);
    Task<bool> TryCreateAsync(SampleStateRecord sample);
    Task<bool> TryUpdateAsync(SampleStateRecord sample);
}
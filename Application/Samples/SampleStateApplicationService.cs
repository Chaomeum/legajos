using AF.Domain.Samples;

namespace AF.Application.Samples;

public sealed class SampleStateApplicationService
{
    private const int MaxAttempts = 3;
    private readonly ISampleRepository _sampleRepository;
    private readonly SampleStateService _sampleStateService;

    public SampleStateApplicationService(
        ISampleRepository sampleRepository,
        SampleStateService sampleStateService
    )
    {
        _sampleRepository = sampleRepository;
        _sampleStateService = sampleStateService;
    }

    public async Task<SampleStateUpdateResult> RegisterReceivedDocumentAsync(SampleStateUpdateCommand command)
    {
        var expectedDocumentTypes = _sampleStateService.ParseRequiredDocumentFamilies(command.RequiredDocumentFamilies);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var existingSample = await _sampleRepository.GetAsync(command.CaseId, command.SampleId);
            var updatedSample = _sampleStateService.ApplyReceivedDocumentType(
                existingSample,
                command.CaseId,
                command.SampleId,
                expectedDocumentTypes,
                command.DetectedType,
                command.PersonType,
                command.PersonSubType,
                command.Regime,
                command.Regulation
            );

            var persisted = existingSample is null
              ? await _sampleRepository.TryCreateAsync(updatedSample)
              : await _sampleRepository.TryUpdateAsync(updatedSample);

            if (persisted)
            {
                return new SampleStateUpdateResult(
                    updatedSample.Status,
                    updatedSample.ExpectedDocumentTypes,
                    updatedSample.ReceivedDocumentTypes
                );
            }
        }

        throw new InvalidOperationException("Could not update sample state due to concurrent modifications");
    }
}
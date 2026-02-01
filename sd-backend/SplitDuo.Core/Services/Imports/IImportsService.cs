using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Common;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports;

public interface IImportsService
{
    Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file);

    Task<Result<ImportStatusDto>> CreateImportJobAsync(IFormFile file, int groupId, int userId,
        ImportAnalysisDto analysisDto);

    Task<Result<ImportStatusDto>> UpdateImportMappingsAsync(Guid importGuid, ImportMappingDto mappingDto);
    Task<Result<ImportStatusDto>> TriggerImportJobAsync(Guid importGuid);
    Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId);
}
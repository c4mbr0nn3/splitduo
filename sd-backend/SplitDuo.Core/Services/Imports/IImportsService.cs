using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Common;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports;

public interface IImportsService
{
    Task<Result> IsValidImportAsync(IFormFile file, int groupId);
    Task<Result> IsDuplicateFileAsync(string fileHash, int groupId);
    Task<Result<CospendImportAnalysisDto>> AnalyzeFileAsync(IFormFile file);
    Task<Result<ImportStatusDto>> CreateImportJobAsync(IFormFile file, int groupId, int userId, CospendImportAnalysisDto analysisDto);
    Task<Result<ImportStatusDto>> UpdateImportMappingsAsync(Guid importGuid, CospendImportMappingDto mappingDto);
    Task<Result<ImportStatusDto>> TriggerImportJobAsync(Guid importGuid);
    Task<Result<int>> ProcessImportAsync(string filePath, int groupId, int importId);
}
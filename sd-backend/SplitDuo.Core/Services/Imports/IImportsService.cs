using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Common;
using SplitDuo.Core.Dto;

namespace SplitDuo.Core.Services.Imports;

public interface IImportsService
{
    Task<Result<ImportStatusDto>> InsertImportJobAsync(IFormFile file, int groupId, int userId);
    Task<Result<ImportStatusDto>> TriggerImportJobAsync(Guid importGuid);
    Task<Result<int>> ProcessImportAsync(string filePath, int groupId, int importId);
}
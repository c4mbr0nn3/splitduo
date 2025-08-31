using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Common;

namespace SplitDuo.Core.Services;

public interface IImportService
{
    Task<Result<ImportStatusDto>> StartImportAsync(IFormFile file, int groupId, int userId);
    Task<Result<int>> ProcessImportAsync(string filePath, int groupId, int userId);
}
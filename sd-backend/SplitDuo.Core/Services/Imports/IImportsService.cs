using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Common;
using SplitDuo.Core.Dto;

namespace SplitDuo.Core.Services.Imports;

public interface IImportsService
{
    Task<Result<ImportStatusDto>> StartImportAsync(IFormFile file, int groupId, int userId);
    Task<Result<int>> ProcessImportAsync(string filePath, int groupId, int userId);
}
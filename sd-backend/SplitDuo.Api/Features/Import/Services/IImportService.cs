using SplitDuo.Api.Features.Import.Dto;
using SplitDuo.Core.Common;

namespace SplitDuo.Api.Features.Import.Services;

public interface IImportService
{
    Task<Result<ImportStatusDto>> ImportFileAsync(IFormFile file, int groupId, int userId);
}
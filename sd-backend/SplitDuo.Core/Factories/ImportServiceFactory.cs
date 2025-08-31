using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Services;
using SplitDuo.Core.Services.Imports;

namespace SplitDuo.Core.Factories;

public interface IImportServiceFactory
{
    IImportService GetImportService(ImportType importType);
}

public class ImportServiceFactory(IServiceProvider serviceProvider) : IImportServiceFactory
{
    public IImportService GetImportService(ImportType importType)
    {
        var service = serviceProvider.GetKeyedService<IImportService>(importType);

        return service ?? throw new NotSupportedException($"Import type '{importType}' is not supported");
    }
}
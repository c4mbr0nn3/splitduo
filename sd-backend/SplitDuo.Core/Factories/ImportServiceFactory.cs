using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Services.Imports;

namespace SplitDuo.Core.Factories;

public interface IImportServiceFactory
{
    IImportsService GetImportService(ImportType importType);
}

public class ImportServiceFactory(IServiceProvider serviceProvider) : IImportServiceFactory
{
    public IImportsService GetImportService(ImportType importType)
    {
        var service = serviceProvider.GetKeyedService<IImportsService>(importType);

        return service ?? throw new NotSupportedException($"Import type '{importType}' is not supported");
    }
}
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Import;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Library.Core;

public static class ImportServiceCollectionExtensions
{
    public static IServiceCollection AddLibraryImports(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ImportOptions>().Bind(configuration.GetSection(ImportOptions.Section));
        services.TryAddScoped<CatalogueCsvValidator>();
        services.TryAddScoped<ImportLookupAdapter>();
        services.TryAddScoped<ICatalogueCodeLookup, CatalogueCodeLookup>();
        services.TryAddScoped<IImportItemWriter, StubImportItemWriter>();
        services.AddMemoryCache();
        services.TryAddSingleton<ImportPreviewStore>();
        services.TryAddSingleton<IImportBatchStore, InMemoryImportBatchStore>();
        services.TryAddScoped<ImportWorkflowService>();
        services.TryAddScoped<IExternalCatalogueProvider, MockImportProvider>();
        return services;
    }
}

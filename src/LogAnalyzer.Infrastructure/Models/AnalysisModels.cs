using LogAnalyzer.Core.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>Creates the model that <c>Model:Provider</c> selects, once its settings have passed validation.</summary>
public static class AnalysisModels
{
    public static IAnalysisModel Create(IServiceProvider services) =>
        services.GetRequiredService<IOptions<ModelOptions>>().Value.IsClaude
            ? ActivatorUtilities.CreateInstance<ClaudeAnalysisModel>(services)
            : new StandInAnalysisModel();
}

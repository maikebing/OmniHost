using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using OmniHost.Sample.WebShowcase.Models;

namespace OmniHost.Sample.WebShowcase.Services;

public sealed class ScriptedApiService(IHostEnvironment environment)
{
    private readonly ScriptOptions _scriptOptions = ScriptOptions.Default
        .AddReferences(typeof(object).Assembly, typeof(Enumerable).Assembly, typeof(ScriptApiResult).Assembly)
        .AddImports("System", "System.Linq", "OmniHost.Sample.WebShowcase.Models");

    public async Task<ScriptApiResult> ExecuteSummaryAsync(CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(environment.ContentRootPath, "Scripts", "dashboard-api.csx");
        var source = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        var globals = new ScriptGlobals(
            DateTimeOffset.Now,
            Environment.MachineName,
            cancellationToken);

        return await CSharpScript.EvaluateAsync<ScriptApiResult>(
            source,
            _scriptOptions,
            globals,
            cancellationToken: cancellationToken);
    }

    public sealed record ScriptGlobals(
        DateTimeOffset Now,
        string MachineName,
        CancellationToken CancellationToken);
}

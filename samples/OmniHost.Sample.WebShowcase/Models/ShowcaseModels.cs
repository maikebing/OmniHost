namespace OmniHost.Sample.WebShowcase.Models;

public sealed record MvcDashboardViewModel(
    string Title,
    string Summary,
    string Timestamp,
    IReadOnlyList<string> Highlights);

public sealed record BlazorSummary(
    string MachineName,
    string Timestamp,
    string Summary,
    IReadOnlyList<string> Capabilities);

public sealed record VueTask(
    string Title,
    string Detail,
    bool Complete);

public sealed record ScriptApiResult(
    string Language,
    string Runtime,
    string Message,
    string Timestamp,
    IReadOnlyList<string> Checkpoints);

public sealed record ShowcaseServerInfo(
    string BaseUrl,
    string MachineName,
    string DotNetVersion,
    IReadOnlyList<string> Scenarios);

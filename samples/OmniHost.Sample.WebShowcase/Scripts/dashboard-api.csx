var checkpoints = new[]
{
    "Script file loaded from Scripts/dashboard-api.csx",
    $"Machine: {MachineName}",
    $"Generated at: {Now:yyyy-MM-dd HH:mm:ss zzz}",
    "Payload produced by Roslyn C# scripting"
};

return new ScriptApiResult(
    "C# Script (.csx)",
    ".NET Roslyn scripting",
    "This JSON payload was created by evaluating a script file on the server side.",
    Now.ToString("yyyy-MM-dd HH:mm:ss"),
    checkpoints);

using HandoffExporter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP server local (stdio) que expõe o snapshot do split do MacGyver ao Claude Code / VS Code (Copilot).
// NÃO toca o TFS — só lê os arquivos gerados por `HandoffExporter --split`.
//
// Diretório do snapshot (em ordem de precedência):
//   1) --export <dir>
//   2) env HANDOFF_EXPORT_DIR
//   3) ./export/macgyver

var builder = Host.CreateApplicationBuilder(args);

// IMPORTANTE: logs vão para STDERR — STDOUT é o canal do protocolo MCP (JSON-RPC).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

string exportDir =
    GetArg(args, "--export")
    ?? Environment.GetEnvironmentVariable("HANDOFF_EXPORT_DIR")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "export", "macgyver");

builder.Services.AddSingleton(new HandoffStore(exportDir));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

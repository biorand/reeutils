using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IntelOrca.Biohazard.REEUtils.Tests
{
    /// <summary>Launches the real reeutils MCP server (dotnet reeutils.dll mcp) over stdio for tests to call tools against.</summary>
    internal static class McpServerTestHost
    {
        public static async Task<McpClient> CreateClientAsync(CancellationToken cancellationToken)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var reeutilsDll = Path.Combine(repoRoot, "src", "reeutils", "bin", configuration, "net10.0", "reeutils.dll");

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "reeutils",
                Command = "dotnet",
                Arguments = [reeutilsDll, "mcp"],
                WorkingDirectory = repoRoot,
            });

            return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        }

        public static async Task<string> CallToolTextAsync(
            McpClient client,
            string toolName,
            IReadOnlyDictionary<string, object?>? arguments = null,
            CancellationToken cancellationToken = default)
        {
            var result = await client.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>(), cancellationToken: cancellationToken);
            Assert.False(result.IsError is true, GetText(result));
            return GetText(result);
        }

        public static string GetText(CallToolResult result)
        {
            return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        }
    }
}

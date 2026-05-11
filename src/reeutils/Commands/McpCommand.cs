using System.Threading.Tasks;
using IntelOrca.Biohazard.REEUtils.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class McpCommand : AsyncCommand<McpCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services.AddSingleton<McpSession>();
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<SessionTools>()
                .WithTools<PakTools>()
                .WithTools<RszTools>();

            using var host = builder.Build();
            await host.RunAsync();
            return ExitCodes.Ok;
        }
    }
}

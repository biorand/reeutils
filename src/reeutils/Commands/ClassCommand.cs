using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using IntelOrca.Biohazard.REE.Rsz;
using Spectre.Console;
using Spectre.Console.Cli;

namespace IntelOrca.Biohazard.REEUtils.Commands
{
    internal sealed class ClassCommand : AsyncCommand<ClassCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-g|--game")]
            public string? Game { get; init; }

            [CommandOption("--enums")]
            public bool GenerateEnums { get; init; }

            [Description("Fully-qualified RSZ type name")]
            [CommandArgument(0, "[args...]")]
            public string[] TypeNames { get; init; } = [];
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Game))
            {
                return ValidationResult.Error("Game not specified. Use -g <game>.");
            }

            return base.Validate(context, settings);
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var repo = GetRszTypeRepository(settings.Game!)
                ?? throw new Exception($"{settings.Game} not recognized.");

            var types = settings.TypeNames.Select(x => GetType(repo, x)).ToArray();
            if (types.Any(x => x == null))
            {
                return Task.FromResult(ExitCodes.Help);
            }

            var writer = new RszTypeCsharpWriter
            {
                GenerateEnums = settings.GenerateEnums
            };
            Console.WriteLine(writer.Generate(types!));
            return Task.FromResult(ExitCodes.Ok);
        }

        private static RszType? GetType(RszTypeRepository repo, string name)
        {
            var type = repo.FromName(name);
            if (type == null)
            {
                var suggestions = repo.Types
                    .Where(x =>
                        !x.Name.ContainsAny(['[', ']', '<', '>', '`']) &&
                        x.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToArray();

                if (suggestions.Length == 1)
                {
                    type = repo.FromName(suggestions[0]);
                }

                if (type == null)
                {
                    if (suggestions.Length == 0)
                    {
                        AnsiConsole.Error.MarkupLine($"[red]Type {Markup.Escape(name)} not found.[/]");
                    }
                    else
                    {
                        AnsiConsole.Error.MarkupLine($"[red]Type {Markup.Escape(name)} not found. Did you mean:[/]");
                        foreach (var suggestion in suggestions)
                        {
                            AnsiConsole.Error.MarkupLine($"[red]- {Markup.Escape(suggestion)}[/]");
                        }
                    }
                }
            }
            return type;
        }

        private static RszTypeRepository? GetRszTypeRepository(string game)
        {
            var rszJsonGz = EmbeddedData.GetFile($"rsz{game}.json.gz");
            if (rszJsonGz == null)
                return null;

            return RszRepositorySerializer.Default.FromJsonGz(rszJsonGz);
        }
    }
}

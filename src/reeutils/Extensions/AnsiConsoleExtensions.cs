using System;
using Spectre.Console;

namespace IntelOrca.Biohazard.REEUtils
{
    public static class AnsiConsoleExtensions
    {
        private static readonly IAnsiConsole ErrorConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Error)
        });

        extension(AnsiConsole)
        {
            /// <summary>
            /// Gets an AnsiConsole that outputs to stderr.
            /// </summary>
            public static IAnsiConsole Error => ErrorConsole;
        }
    }
}

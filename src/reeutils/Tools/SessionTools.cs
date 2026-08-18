using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntelOrca.Biohazard.REEUtils.Tools
{
    [McpServerToolType]
    internal sealed class SessionTools
    {
        [McpServerTool(Name = "get_engine_details", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Gets embedded RE Engine documentation in markdown format.")]
        public static string GetEngineDetails()
        {
            return McpEmbeddedData.GetEngineDetailsMarkdown();
        }

        [McpServerTool(Name = "open_pak", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true), Description("REQUIRED FIRST STEP. Opens a .pak file or game install directory so other tools can access its contents. Call this first, then set_game.")]
        public static string OpenPak(
            [Description("Path to a .pak file or a game install directory.")] string path,
            McpSession session)
        {
            session.OpenPak(path);
            return session.GetStatus().ToJson(camelCase: true);
        }

        [McpServerTool(Name = "open_rsz", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true), Description("Loads an RSZ type repository from a .json or .json.gz file.")]
        public static string OpenRsz(
            [Description("Path to an RSZ repository .json or .json.gz file.")] string path,
            McpSession session)
        {
            session.OpenRsz(path);
            return session.GetStatus().ToJson(camelCase: true);
        }

        [McpServerTool(Name = "open_pak_list", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true), Description("Loads a pak list from a .txt or .gz file.")]
        public static string OpenPakList(
            [Description("Path to a pak list .txt or .gz file.")] string path,
            McpSession session)
        {
            session.OpenPakList(path);
            return session.GetStatus().ToJson(camelCase: true);
        }

        [McpServerTool(Name = "list_games", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false), Description("Lists the supported embedded RE Engine games.")]
        public static string ListGames()
        {
            return new
            {
                games = McpEmbeddedData.GetSupportedGames()
            }.ToJson(camelCase: true);
        }

        [McpServerTool(Name = "set_game", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false), Description("REQUIRED AFTER open_pak. Loads the embedded pak file listing (path map) and RSZ type data for the given game. Must complete before find, read, search, or list_files can work. Call this after open_pak, then wait for the result before calling any other tool.")]
        public static string SetGame(
            [Description("Game identifier, such as re2, re4, re7, re8, or re9.")] string game,
            McpSession session)
        {
            session.SetGame(game);
            return session.GetStatus().ToJson(camelCase: true);
        }
    }
}

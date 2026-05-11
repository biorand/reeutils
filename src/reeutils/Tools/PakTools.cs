using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntelOrca.Biohazard.REEUtils.Tools
{
    [McpServerToolType]
    internal sealed class PakTools
    {
        [McpServerTool(Name = "search"), Description("Searches files in the current pak similarly to the grep command.")]
        public static string Search(
            [Description("Regex pattern to search for in paths or values.")] string regex,
            [Description("Pak paths, prefixes, or wildcard patterns to search within.")] string[] paths,
            [Description("Maximum number of matches to return.")] int maxResults,
            McpSession session)
        {
            return McpServerSupport.Search(session, regex, paths, maxResults);
        }

        [McpServerTool(Name = "list_files"), Description("Lists files or directories in the current pak similarly to the ls command.")]
        public static string ListFiles(
            [Description("Optional pak directory or file path. Leave empty to list the pak root.")] string? path,
            McpSession session)
        {
            return McpServerSupport.ListFiles(session, path);
        }

        [McpServerTool(Name = "read"), Description("Reads a supported REE file and returns JSON. Supports .msg, .scn, .user, and .pfb files.")]
        public static string Read(
            [Description("A disk path or pak-internal path to read.")] string path,
            McpSession session)
        {
            return McpServerSupport.Read(session, path);
        }
    }
}

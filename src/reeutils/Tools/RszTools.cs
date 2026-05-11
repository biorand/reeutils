using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntelOrca.Biohazard.REEUtils.Tools
{
    [McpServerToolType]
    internal sealed class RszTools
    {
        [McpServerTool(Name = "generate_class"), Description("Generates C# classes for RSZ types similarly to the class command.")]
        public static string GenerateClass(
            [Description("One or more fully-qualified RSZ type names.")] string[] typeNames,
            [Description("Whether enum dependencies should also be generated.")] bool includeEnums,
            McpSession session)
        {
            return McpServerSupport.GenerateClass(session, typeNames, includeEnums);
        }

        [McpServerTool(Name = "get_type"), Description("Gets structured RSZ type information for a single type.")]
        public static string GetType(
            [Description("Fully-qualified RSZ type name.")] string typeName,
            McpSession session)
        {
            return McpServerSupport.GetTypeDetails(session, typeName);
        }
    }
}

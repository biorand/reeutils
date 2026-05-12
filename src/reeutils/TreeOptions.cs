namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class TreeOptions
    {
        public static TreeOptions Root { get; } = new TreeOptions();

        public string Xpath { get; init; } = "";
        public int Depth { get; init; }
    }
}

namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class TreeOptions
    {
        public static TreeOptions Root { get; } = new TreeOptions
        {
            Depth = 0
        };

        public string Xpath { get; init; } = "";
        public string[] Xpaths { get; init; } = [];
        public int Depth { get; init; } = 1;
        public bool CompactComponents { get; init; }
    }
}

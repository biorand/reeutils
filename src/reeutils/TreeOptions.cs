namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class TreeOptions
    {
        public static TreeOptions Root { get; } = new TreeOptions
        {
            Full = true
        };

        public string[] ExpandNodes { get; init; } = [];
        public bool Full { get; init; }
        public int? MaxDepth { get; init; }
    }
}

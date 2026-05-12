namespace IntelOrca.Biohazard.REEUtils
{
    internal sealed class TreeOptions
    {
        public static TreeOptions Root { get; } = new TreeOptions
        {
            Full = true
        };

        public string[] Xpaths { get; init; } = [];
        public bool Full { get; init; }
    }
}

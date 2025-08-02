namespace IntelOrca.Biohazard.REE.Messages
{
    public readonly struct MsgAttributeDefinition(MsgAttributeType type, string name)
    {
        public MsgAttributeType Type => type;
        public string Name => name;

        public override string ToString() => string.IsNullOrEmpty(Name) ? $"Unnamed {Type}" : Name;
    }
}

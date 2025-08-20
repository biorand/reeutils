namespace IntelOrca.Biohazard.REE.Rsz
{
    public readonly struct RszInstance(RszInstanceId id, IRszNode value)
    {
        public RszInstanceId Id => id;
        public IRszNode Value => value;

        public override string ToString() => $"{Value}[{Id.Index}]";
    }
}

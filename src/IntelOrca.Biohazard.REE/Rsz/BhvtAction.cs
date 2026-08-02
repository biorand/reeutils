namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// An action a node runs. The action's own id (the field the game uses to look it up) always lives
    /// on <see cref="Instance"/> itself -- see <see cref="BhvtFile.ActionIdFieldIndex"/> -- rather than
    /// being duplicated here.
    /// </summary>
    public sealed class BhvtAction(RszObjectNode instance, uint actionEx)
    {
        public RszObjectNode Instance { get; } = instance;
        public uint ActionEx { get; } = actionEx;

        public BhvtAction WithInstance(RszObjectNode instance) => new(instance, ActionEx);
        public BhvtAction WithActionEx(uint actionEx) => new(Instance, actionEx);

        public override string ToString() => Instance.Type.Name;
    }
}

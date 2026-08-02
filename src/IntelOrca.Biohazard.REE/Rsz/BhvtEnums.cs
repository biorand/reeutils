using System;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [Flags]
    public enum BhvtNodeAttributes : ushort
    {
        None = 0,
        IsEnabled = 0x1,
        IsRestartable = 0x2,
        HasReferenceTree = 0x4,
        BubblesChildEnd = 0x8,
        SelectOnce = 0x10,
        IsFsmNode = 0x20,
        TraverseToLeaf = 0x40,
    }

    [Flags]
    public enum BhvtWorkFlags : ushort
    {
        None = 0,
        IsNotifiedEnd = 0x1,
        HasEvaluated = 0x2,
        HasSelected = 0x4,
        IsCalledActionPrestart = 0x8,
        IsCalledActionStart = 0x10,
        IsNotifiedUnderLayerEnd = 0x20,
        IsBranchState = 0x40,
        IsEndState = 0x80,
        IsStartedSelector = 0x100,
        OverridedSelector = 0x200,
        DuplicatedAction = 0x400,
        IsAsRestartable = 0x800,
    }
}

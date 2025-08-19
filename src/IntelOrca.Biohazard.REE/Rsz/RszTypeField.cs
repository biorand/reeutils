﻿using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}: {Type}")]
    public readonly struct RszTypeField(RszType type, string name)
    {
        public RszType Type => type;
        public string Name => name;
    }
}

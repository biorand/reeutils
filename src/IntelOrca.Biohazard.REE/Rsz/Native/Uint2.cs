using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Uint2
    {
        public uint x;
        public uint y;

        public readonly override string ToString()
        {
            return $"<{x}, {y}>";
        }

        public uint this[int index]
        {
            readonly get
            {
                ValueTypeUtils.CheckIndex(index, 2);
                return index == 0 ? x : y;
            }
            set
            {
                ValueTypeUtils.CheckIndex(index, 2);
                if (index == 0) x = value;
                else y = value;
            }
        }
    }
}

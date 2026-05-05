using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct RangeI
    {
        public int r;
        public int s;

        public readonly override string ToString()
        {
            return $"Range({r}, {s})";
        }

        public int this[int index]
        {
            readonly get
            {
                ValueTypeUtils.CheckIndex(index, 2);
                return index == 0 ? r : s;
            }
            set
            {
                ValueTypeUtils.CheckIndex(index, 2);
                if (index == 0) r = value;
                else s = value;
            }
        }
    }
}

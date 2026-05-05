using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Size
    {
        public float w;
        public float h;

        public float this[int index]
        {
            readonly get
            {
                ValueTypeUtils.CheckIndex(index, 2);
                return index == 0 ? w : h;
            }
            set
            {
                ValueTypeUtils.CheckIndex(index, 2);
                if (index == 0) w = value;
                else h = value;
            }
        }

        public readonly override string ToString() => $"<{w}, {h}>";
    }
}

using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Int2
    {
        public int x;
        public int y;

        public Int2(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public readonly override string ToString()
        {
            return $"<{x}, {y}>";
        }

        public int this[int index]
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

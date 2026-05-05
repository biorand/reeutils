using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Uint3
    {
        public uint x;
        public uint y;
        public uint z;

        public readonly override string ToString()
        {
            return $"<{x}, {y}, {z}>";
        }

        public uint this[int index]
        {
            readonly get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                _ => throw ValueTypeUtils.IndexError(index, 3)
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw ValueTypeUtils.IndexError(index, 3);
                }
            }
        }
    }
}

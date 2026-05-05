using System;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Uint4
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;

        public readonly override string ToString()
        {
            return $"<{x}, {y}, {z}, {w}>";
        }

        public uint this[int index]
        {
            readonly get => index switch
            {
                0 => x,
                1 => y,
                2 => z,
                3 => w,
                _ => throw ValueTypeUtils.IndexError(index, 4)
            };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default: throw ValueTypeUtils.IndexError(index, 4);
                }
            }
        }
    }
}

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using IntelOrca.Biohazard.REE.Rsz;

namespace via
{
    public struct Rect
    {
        public float left;
        public float top;
        public float right;
        public float bottom;

        public Rect()
        {
        }

        public Rect(float left, float top, float right, float bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        public float this[int index]
        {
            get
            {
                ValueTypeUtils.CheckIndex(index, 4);
                ref float ptr = ref left;
                return Unsafe.Add(ref ptr, index);
            }
            set
            {
                ValueTypeUtils.CheckIndex(index, 4);
                ref float ptr = ref left;
                Unsafe.Add(ref ptr, index) = value;
            }
        }

        public readonly Vector4 AsVector => new Vector4(left, top, right, bottom);

        public readonly override string ToString() => AsVector.ToString();
    }
}

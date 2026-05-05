using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AABB
    {
        [FieldOffset(0), JsonIgnore]
        public Vector3 minpos;
        [FieldOffset(16), JsonIgnore]
        public Vector3 maxpos;

        public AABB()
        {
        }

        public AABB(Vector3 minpos, Vector3 maxpos)
        {
            this.minpos = minpos;
            this.maxpos = maxpos;
        }

        public Vector3 Minpos { readonly get => minpos; set => minpos = value; }
        public Vector3 Maxpos { readonly get => maxpos; set => maxpos = value; }

        public readonly Vector3 Size => maxpos - minpos;
        public readonly Vector3 Center => (minpos + maxpos) / 2;

        public readonly bool IsEmpty => minpos == maxpos;
        public readonly bool IsInvalid => minpos.X == float.MaxValue;

        public static readonly AABB MaxMin = new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue));
        public static readonly AABB MinMax = new AABB(new Vector3(float.MinValue), new Vector3(float.MaxValue));
        public static readonly AABB Invalid = new AABB(new Vector3(float.MaxValue), new Vector3(float.MaxValue));

        public readonly AABB Extend(Vector3 point)
        {
            return new AABB(Vector3.Min(minpos, point), Vector3.Max(maxpos, point));
        }

        public readonly AABB Extend(AABB other)
        {
            return new AABB(Vector3.Min(minpos, other.minpos), Vector3.Max(maxpos, other.maxpos));
        }

        public readonly AABB Margin(float margin)
        {
            var mv = new Vector3(margin);
            return new AABB(minpos - mv, maxpos + mv);
        }

        public static AABB Combine(IEnumerable<AABB> bounds) => bounds.Aggregate(MaxMin, (bound, item) => bound.Extend(item));

        public static AABB operator +(AABB aabb, Vector3 vec) => new AABB(aabb.minpos + vec, aabb.maxpos + vec);
        public static AABB operator -(AABB aabb, Vector3 vec) => new AABB(aabb.minpos - vec, aabb.maxpos - vec);

        public readonly override string ToString() => $"AABB({minpos} {maxpos})";
    }
}

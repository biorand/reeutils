using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AABB
    {
        [FieldOffset(0)]
        public Vector3 Min;
        [FieldOffset(16)]
        public Vector3 Max;

        public AABB()
        {
        }

        public AABB(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public readonly Vector3 Size => Max - Min;
        public readonly Vector3 Center => (Min + Max) / 2;

        public readonly bool IsEmpty => Min == Max;
        public readonly bool IsInvalid => Min.X == float.MaxValue;

        public static readonly AABB MaxMin = new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue));
        public static readonly AABB MinMax = new AABB(new Vector3(float.MinValue), new Vector3(float.MaxValue));
        public static readonly AABB Invalid = new AABB(new Vector3(float.MaxValue), new Vector3(float.MaxValue));

        public readonly AABB Extend(Vector3 point)
        {
            return new AABB(Vector3.Min(Min, point), Vector3.Max(Max, point));
        }

        public readonly AABB Extend(AABB other)
        {
            return new AABB(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
        }

        public readonly AABB Margin(float margin)
        {
            var mv = new Vector3(margin);
            return new AABB(Min - mv, Max + mv);
        }

        public static AABB Combine(IEnumerable<AABB> bounds) => bounds.Aggregate(MaxMin, (bound, item) => bound.Extend(item));

        public static AABB operator +(AABB aabb, Vector3 vec) => new AABB(aabb.Min + vec, aabb.Max + vec);
        public static AABB operator -(AABB aabb, Vector3 vec) => new AABB(aabb.Min - vec, aabb.Max - vec);

        public readonly override string ToString() => $"AABB({Min} {Max})";
    }
}

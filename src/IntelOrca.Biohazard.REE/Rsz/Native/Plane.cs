using System.Numerics;
using System.Runtime.CompilerServices;

namespace via
{
    public struct Plane
    {
        public Vector3 normal;
        public float dist;

        public Plane(Vector3 normal, float dist)
        {
            this.normal = normal;
            this.dist = dist;
        }

        public Plane(float x, float y, float z, float dist)
        {
            normal = new Vector3(x, y, z);
            this.dist = dist;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInFront(Vector3 point) => Vector3.Dot(point, normal) + dist > 0;

        public Plane Normalize()
        {
            float invLen = 1.0f / normal.Length();
            return new Plane(normal * invLen, dist * invLen);
        }

        public readonly override string ToString() => $"Plane({normal}, Dist = {dist})>";
    }
}

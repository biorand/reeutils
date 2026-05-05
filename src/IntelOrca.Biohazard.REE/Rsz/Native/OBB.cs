using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace via
{
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct OBB
    {
        [FieldOffset(0), JsonIgnore]
        private Matrix4x4 coord;
        [FieldOffset(64), JsonIgnore]
        private Vector3 extent;

        public OBB()
        {
            coord = Matrix4x4.Identity;
            extent = Vector3.One;
        }

        public OBB(Matrix4x4 coord, Vector3 extent)
        {
            this.coord = coord;
            this.extent = extent;
        }

        public Matrix4x4 Coord { readonly get => coord; set => coord = value; }
        public Vector3 Extent { readonly get => extent; set => extent = value; }

        public readonly AABB GetBounds(float margin = 0)
        {
            static Vector3 Transform(Matrix4x4 matrix, Vector3 vector)
            {
                var transformed = Vector4.Transform(new Vector4(vector, 1), matrix);
                return new Vector3(transformed.X, transformed.Y, transformed.Z) / transformed.W;
            }

            var aabb = AABB.MaxMin;
            var size = extent + new Vector3(margin);
            aabb = aabb.Extend(Transform(coord, new Vector3(size.X, size.Y, size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(size.X, size.Y, -size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(size.X, -size.Y, size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(size.X, -size.Y, -size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(-size.X, size.Y, size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(-size.X, size.Y, -size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(-size.X, -size.Y, size.Z)));
            aabb = aabb.Extend(Transform(coord, new Vector3(-size.X, -size.Y, -size.Z)));
            return aabb;
        }

        public override string ToString() => $"[Pos: {coord.M41} {coord.M42} {coord.M43}, Extent: {extent}]";
    }
}

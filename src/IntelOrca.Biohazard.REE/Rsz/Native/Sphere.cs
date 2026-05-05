using System.Numerics;
using System.Text.Json.Serialization;

namespace via
{
    public struct Sphere
    {
        [JsonIgnore]
        public Vector3 pos;
        [JsonIgnore]
        public float r;

        public Vector3 Pos { readonly get => pos; set => pos = value; }
        public float R { readonly get => r; set => r = value; }

        public Sphere(Vector3 pos, float r)
        {
            this.pos = pos;
            this.r = r;
        }

        public Sphere()
        {
            r = 1;
        }

        public readonly override string ToString()
        {
            return $"Sphere({pos}, {r})";
        }

        public readonly AABB GetBounds(float margin = 0)
        {
            var vec = new Vector3(r + margin);
            return new AABB(pos - vec, pos + vec);
        }
    }
}

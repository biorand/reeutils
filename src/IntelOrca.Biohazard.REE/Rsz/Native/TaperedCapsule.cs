using System.Numerics;

namespace via
{
    public struct TaperedCapsule
    {
        private Vector4 vertexRadiusA;
        private Vector4 vertexRadiusB;

        public Vector4 VertexRadiusA { readonly get => vertexRadiusA; set => vertexRadiusA = value; }
        public Vector4 VertexRadiusB { readonly get => vertexRadiusB; set => vertexRadiusB = value; }

        public readonly override string ToString() => $"TaperedCapsule({vertexRadiusA}, {vertexRadiusB})";
    }
}

using System.Runtime.CompilerServices;

namespace via
{
    public struct Frustum
    {
        public Plane plane0;
        public Plane plane1;
        public Plane plane2;
        public Plane plane3;
        public Plane plane4;
        public Plane plane5;

        public Plane this[int index]
        {
            get
            {
                ref Plane ptr = ref plane0;
                return Unsafe.Add(ref ptr, index);
            }
            set
            {
                ref Plane ptr = ref plane0;
                Unsafe.Add(ref ptr, index) = value;
            }
        }
    }
}

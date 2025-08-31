using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}: {Type}")]
    public sealed class RszTypeField
    {
        public string Name { get; set; } = "";
        public int Align { get; set; }
        public int Size { get; set; }
        public bool IsArray { get; set; }
        public RszFieldType Type { get; set; }
        public RszType? ObjectType { get; set; }

        /// <summary>
        /// This is used for PFB GameObjectRef linking.
        /// Since the RSZ dumps don't contain the ID, we find it when loading a file
        /// and keep note of it.
        /// </summary>
        public int? Id { get; set; }
    }
}

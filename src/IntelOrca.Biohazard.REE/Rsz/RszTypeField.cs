using System.Diagnostics;

namespace IntelOrca.Biohazard.REE.Rsz
{
    [DebuggerDisplay("{Name,nq}: {Type}")]
    public sealed class RszTypeField
    {
        public string Name { get; init; } = "";
        public int Align { get; init; }
        public int Size { get; init; }
        public bool IsArray { get; init; }
        public RszFieldType Type { get; init; }
        public RszType? ObjectType { get; init; }

        /// <summary>
        /// This is used for PFB GameObjectRef linking.
        /// Since the RSZ dumps don't contain the ID, we find it when loading a file
        /// and keep note of it.
        /// </summary>
        public int? Id { get; set; }
    }
}

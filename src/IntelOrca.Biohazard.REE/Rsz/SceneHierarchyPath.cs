using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Represents a game object path within a scene.
    /// </summary>
    /// <param name="path">The path of the game object where / is used as a child separator.</param>
    public readonly struct SceneHierarchyPath(string path)
    {
        public ImmutableArray<string> Hierarchy { get; } = path.Split('/').ToImmutableArray();
        public IReadOnlyList<string> Folders => Hierarchy.SkipLast(1).ToImmutableArray();
        public string Name => Hierarchy.Last();

        public override string ToString() => string.Join('/', Hierarchy);

        public static implicit operator SceneHierarchyPath(string path) => new(path);
    }
}

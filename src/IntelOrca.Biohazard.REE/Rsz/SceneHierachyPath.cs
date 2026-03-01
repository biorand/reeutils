using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Represents a game object path within a scene.
    /// </summary>
    /// <param name="path">The path of the game object where / is used as a child separator.</param>
    public readonly struct SceneHierachyPath(string path)
    {
        public ImmutableArray<string> Hierachy { get; } = path.Split('/').ToImmutableArray();
        public IReadOnlyList<string> Folders => Hierachy.SkipLast(1).ToImmutableArray();
        public string Name => Hierachy.Last();

        public override string ToString() => string.Join('/', Hierachy);

        public static implicit operator SceneHierachyPath(string path) => new(path);
    }
}

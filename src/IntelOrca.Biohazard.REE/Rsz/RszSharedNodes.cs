using System.Runtime.CompilerServices;

namespace IntelOrca.Biohazard.REE.Rsz
{
    /// <summary>
    /// Tracks the nodes that a source document (a binary RSZ file or JSON with "@id"/"@ref")
    /// genuinely shares between several owners.
    /// <para>
    /// RE2 non-RT (RSZ v&lt;16) files do contain instances referenced from more than one place, and
    /// the builder keeps those as a single instance. Nodes are immutable, though, so cloning or
    /// editing a game object leaves every untouched sub-node as the very same object in both the
    /// original and the copy. Treating all repeated nodes as shared would silently turn a clone
    /// into a game object that points at its source's component instances, which crashes the game.
    /// Only nodes marked here are de-duplicated; every other repeat gets its own instance.
    /// </para>
    /// </summary>
    internal static class RszSharedNodes
    {
        private static readonly ConditionalWeakTable<object, object> _sharedNodes = new();
        private static readonly object _marker = new();

        public static void Mark(IRszNode node)
        {
            if (node.GetType().IsValueType)
                return;
            // GetValue adds the marker when missing; TryAdd is not available on netstandard2.1.
            _sharedNodes.GetValue(node, static _ => _marker);
        }

        public static bool IsShared(IRszNode node)
        {
            return !node.GetType().IsValueType && _sharedNodes.TryGetValue(node, out _);
        }
    }
}

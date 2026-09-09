using System;
using System.Collections.Immutable;
using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.REE.Tests
{
    /// <summary>
    /// Read/write tests for Onimusha behaviour trees (.fsmv2.42). The write path re-emits the whole
    /// container, so an export->import roundtrip must reproduce the source bytes exactly, and a
    /// targeted edit (flipping an <c>app.fsm_condition</c> / node-work constant) must survive a
    /// rebuild + re-read.
    /// </summary>
    public sealed class TestOniwsFsmv2
    {
        private const int Version = 42;

        private static readonly string[] Corpus =
        [
            "natives/STM/GameDesign/Story/MainMission/Ms000100/Ob11/_Fsm/Ms000100_Ob11_Fsm01.fsmv2.42",
            "natives/STM/GameDesign/Story/MainMission/Ms032010/Ob00/_Fsm/Ms032010_Ob00_RestoreFsm.fsmv2.42",
            "natives/STM/GameDesign/Story/SideMissionStory/Ms100401/Ob05/_Fsm/Ms100401_Ob05_Fsm00.fsmv2.42",
            "natives/STM/GameDesign/Story/SideMissionStory/Ms105003/Ob00/_Fsm/Ms105003_Ob00_RestoreFsm.fsmv2.42",
        ];

        private readonly OriginalPakHelper _pakHelper = OriginalPakHelper.Default;

        [Fact]
        public void Read_Build_All_Oniws_Fsmv2_Is_ByteIdentical()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            foreach (var path in Corpus)
            {
                var data = _pakHelper.GetFileData(GameNames.ONIWS, path);
                var file = new BhvtFile(Version, data);
                Assert.Equal("root", file.ReadTree(repo).Name);
                var rebuilt = file.ToBuilder(repo).Build().Data.ToArray();
                Assert.True(rebuilt.AsSpan().SequenceEqual(data), $"FSMV2 rebuild changed bytes: {path}");
            }
        }

        [Fact]
        public void Edit_Condition_Constant_Survives_Rebuild()
        {
            var repo = _pakHelper.GetTypeRepository(GameNames.ONIWS);
            var path = "natives/STM/GameDesign/Story/SideMissionStory/Ms100401/Ob05/_Fsm/Ms100401_Ob05_Fsm00.fsmv2.42";
            var file = new BhvtFile(Version, _pakHelper.GetFileData(GameNames.ONIWS, path));
            var tree = file.ReadTree(repo);

            // Find a state condition carrying a "Condition" bool and flip it.
            var (flippedRoot, flippedValue) = FlipFirstCondition(tree, repo);
            Assert.NotNull(flippedRoot);
            Assert.NotNull(flippedValue);

            var builder = file.ToBuilder(repo);
            builder.Root = flippedRoot;
            var rebuilt = builder.Build();

            // The edit must survive a rebuild + re-read: the first condition's value is now the
            // flipped value (the original was the opposite).
            var reread = new BhvtFile(Version, rebuilt.Data).ReadTree(repo);
            var firstValue = ReadFirstConditionValue(reread, repo);
            Assert.NotNull(firstValue);
            Assert.Equal(flippedValue, firstValue);
        }

        /// <summary>
        /// Returns a copy of the tree with the first state condition's "Condition" bool flipped, plus
        /// the new value. Returns (null, null) if no such condition exists.
        /// </summary>
        private static (BhvtNode? Root, bool? Value) FlipFirstCondition(BhvtNode root, RszTypeRepository repo)
        {
            return Flip(root);

            (BhvtNode? Node, bool? Value) Flip(BhvtNode node)
            {
                for (var i = 0; i < node.States.Length; i++)
                {
                    var state = node.States[i];
                    var cond = state.Condition;
                    if (TryReadCondition(cond, out var current))
                    {
                        var flipped = cond!.SetField("Condition", !current);
                        var states = node.States.SetItem(i, state.WithCondition(flipped));
                        return (node.WithStates(states), !current);
                    }
                }

                for (var i = 0; i < node.Transitions.Length; i++)
                {
                    var transition = node.Transitions[i];
                    if (TryReadCondition(transition.Condition, out var current))
                    {
                        var flipped = transition.Condition!.SetField("Condition", !current);
                        var transitions = node.Transitions.SetItem(i, transition.WithCondition(flipped));
                        return (node.WithTransitions(transitions), !current);
                    }
                }

                foreach (var child in node.Children)
                {
                    var result = Flip(child.Node);
                    if (result.Node != null)
                    {
                        var children = ImmutableArray.CreateBuilder<BhvtChild>(node.Children.Length);
                        foreach (var c in node.Children)
                        {
                            children.Add(ReferenceEquals(c.Node, child.Node)
                                ? new BhvtChild(result.Node, c.Condition)
                                : c);
                        }
                        return (node.WithChildren(children.MoveToImmutable()), result.Value);
                    }
                }

                return (null, null);
            }
        }

        private static bool? ReadFirstConditionValue(BhvtNode root, RszTypeRepository repo)
        {
            bool? found = null;
            void Walk(BhvtNode node)
            {
                if (found.HasValue) return;
                foreach (var state in node.States)
                {
                    if (TryReadCondition(state.Condition, out var value))
                    {
                        found = value;
                        return;
                    }
                }
                foreach (var transition in node.Transitions)
                {
                    if (TryReadCondition(transition.Condition, out var value))
                    {
                        found = value;
                        return;
                    }
                }
                foreach (var child in node.Children)
                {
                    Walk(child.Node);
                }
            }
            Walk(root);
            return found;
        }

        private static bool TryReadCondition(RszObjectNode? cond, out bool value)
        {
            if (cond != null && cond.Type.FindFieldIndex("Condition") >= 0 &&
                cond["Condition"] is RszValueNode valueNode)
            {
                value = RszSerializer.Deserialize<bool>(valueNode);
                return true;
            }
            value = default;
            return false;
        }
    }
}
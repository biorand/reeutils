using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using IntelOrca.Biohazard.REE.Rsz;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace IntelOrca.Biohazard.REEUtils.Tools
{
    /// <summary>
    /// Named, single-purpose gameplay patches, built on the same BhvtFile.Builder/RszObjectNode API the
    /// fsmv2 tests exercise (see IntelOrca.Biohazard.REEUtils.Tests.TestBhvt) -- not text/JSON surgery.
    /// Each fix loads the real file into a Builder (preserving everything the JSON export/import round
    /// trip doesn't carry, such as UVars), applies a targeted, minimal edit, and rebuilds.
    /// </summary>
    [McpServerToolType]
    internal sealed class FixModifier
    {
        [McpServerTool(Name = "fix_re8_chapter1_inventory_unlock", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true),
         Description("Patches RE8's chapter 1 main-flow FSM (mainflowfsm/chapter1/c01_main.fsmv2.40) so the pause-menu inventory unlocks at the start of chapter 1 instead of at chapter end. Attaches ReleaseMenuAction and ChangeInventoryAction -- the same action pair chapter1_end.fsmv2.40 uses to unseal the menu -- to the chapter's first flow node, alongside its existing GameFlowNode action.")]
        public static string UnlockChapter1Inventory(
            [Description("Disk path or pak-internal path to c01_main.fsmv2.40.")] string path,
            [Description("Disk path to write the patched .fsmv2.40 file to.")] string outputPath,
            McpSession session)
        {
            var repo = session.RszTypeRepository ?? throw new McpException("No RSZ repository is loaded. Call open_rsz or set_game first.");
            var data = session.ReadFileData(path, out var resolvedPath);
            var version = GetFsmv2Version(resolvedPath);

            var builder = new BhvtFile(version, data).ToBuilder(repo);

            var firstChildIndex = -1;
            for (var i = 0; i < builder.Root.Children.Length; i++)
            {
                if (builder.Root.Children[i].Node.Name == "0")
                {
                    firstChildIndex = i;
                    break;
                }
            }
            if (firstChildIndex == -1)
            {
                throw new McpException(
                    "No child named \"0\" found under root -- this doesn't look like c01_main.fsmv2.40's expected layout.");
            }

            var target = builder.Root.Children[firstChildIndex];
            var usedIds = CollectActionIds(builder.Root);

            var releaseMenu = repo.Create("app.ReleaseMenuAction")
                .SetField("v0_Enabled", true)
                .SetField("v1_ID", NextUnusedId(usedIds));
            var changeInventory = repo.Create("app.ChangeInventoryAction")
                .SetField("v0_Enabled", true)
                .SetField("v1_ID", NextUnusedId(usedIds));

            var patchedNode = target.Node.WithActions(target.Node.Actions
                .Add(new BhvtAction(releaseMenu, 0))
                .Add(new BhvtAction(changeInventory, 0)));

            builder.Root = builder.Root.WithChildren(builder.Root.Children.SetItem(firstChildIndex, target.WithNode(patchedNode)));

            var patched = builder.Build();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            File.WriteAllBytes(outputPath, patched.Data.ToArray());

            return ToJson(new
            {
                sourcePath = resolvedPath,
                outputPath,
                patchedNode = patchedNode.Name,
                addedActions = new[] { "app.ReleaseMenuAction", "app.ChangeInventoryAction" },
            });
        }

        /// <summary>Collects every v1_ID already used by an action anywhere in the tree, so new actions get IDs that can't collide within the action table.</summary>
        private static HashSet<uint> CollectActionIds(BhvtNode root)
        {
            var ids = new HashSet<uint>();

            void Walk(BhvtNode node)
            {
                foreach (var action in node.Actions)
                {
                    if (action.Instance.Type.FindFieldIndex("v1_ID") != -1)
                        ids.Add(RszSerializer.Deserialize<uint>(action.Instance["v1_ID"]));
                }
                foreach (var child in node.Children)
                    Walk(child.Node);
            }

            Walk(root);
            return ids;
        }

        private static uint NextUnusedId(HashSet<uint> used)
        {
            uint id;
            do
            {
                id = (uint)Random.Shared.NextInt64(1, uint.MaxValue);
            } while (!used.Add(id));
            return id;
        }

        private static int GetFsmv2Version(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Length > 1 && int.TryParse(ext.AsSpan(1), out var version) ? version : 40;
        }

        private static string ToJson(object value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value, JsonSupport.CreateOptions(camelCase: true));
        }
    }
}

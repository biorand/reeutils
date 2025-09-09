using IntelOrca.Biohazard.REE.Cryptography;
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

namespace reeexamples
{
    internal class HashFinder
    {
        public static void Main(string[] args)
        {
            var jsonPath = @"M:\git\reasy\resources\data\dumps\rszre8.json";
            var repo = RszRepositorySerializer.Default.FromJsonFile(jsonPath);
            var pakFile = new RePakCollection(@"G:\biorand\re8\vanilla");
            var pakList = PakList.FromFile(@"M:\git\biorand-re8\src\BioRand.RE8\data\re8.pak.list.gz");

            var stringList = new List<string>();
            var dict = new Dictionary<string, uint>();

            foreach (var path in pakList.Entries)
            {
                try
                {
                    Console.WriteLine(path + ":");
                    if (path.EndsWith(".user.2"))
                    {
                        var root = new UserFile(pakFile.GetEntryData(path)).GetObjects(repo)[0];
                        Visit(root);
                    }
                    else if (path.EndsWith(".scn.20"))
                    {
                        var scn = new ScnFile(20, pakFile.GetEntryData(path)).ReadScene(repo);
                        Visit(scn);
                    }
                    else if (path.EndsWith(".pfb.17"))
                    {
                        var pfb = new PfbFile(17, pakFile.GetEntryData(path)).ReadScene(repo);
                        Visit(pfb);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(path + " : " + e.Message);
                }
            }

            void AddString(string s)
            {
                if (!dict.ContainsKey(s))
                {
                    stringList.Add(s);
                    var hash = (uint)MurMur3.HashData(s);
                    dict[s] = hash;
                    Console.WriteLine("    " + hash + "    " + s);
                }
            }

            void AddStringCases(string s)
            {
                AddString(s);
                AddString(s.ToLowerInvariant());
                AddString(s.ToUpperInvariant());
            }

            void Visit(IRszNode node)
            {
                if (node is RszStringNode stringNode)
                {
                    AddStringCases(stringNode.Value);
                }
                else if (node is RszValueNode valueNode)
                {
                    if (valueNode.Type == RszFieldType.Guid)
                    {
                        AddStringCases(RszSerializer.Deserialize<Guid>(valueNode).ToString());
                    }
                }

                if (node is RszGameObject gameObject)
                {
                    foreach (var component in gameObject.Components)
                    {
                        Visit(component);
                    }
                }
                if (node is IRszNodeContainer container)
                {
                    foreach (var child in container.Children)
                    {
                        Visit(child);
                    }
                }
            }
        }
    }
}

# 🧩 reeutils, IntelOrca.Biohazard.REE

[![NuGet](https://img.shields.io/nuget/v/IntelOrca.Biohazard.REE.svg)](https://www.nuget.org/packages/IntelOrca.Biohazard.REE)

A .NET library for working with RE Engine resource formats and PAK files. Primarily developed to support the `biorand` project, but useful for modding most RE engine games.

## 🚀 Usage

Add the package to your project by referencing the NuGet package in your `.csproj`:

```xml
<PackageReference Include="IntelOrca.Biohazard.REE" Version="1.6.0" />
```

Basic example showing how to open a file from the game's patched pak files, modify it and write a new patch pak:

```csharp
using IntelOrca.Biohazard.REE.Package;
using IntelOrca.Biohazard.REE.Rsz;

var repo = RszRepositorySerializer.Default.FromJsonFile(@"C:\reasy\rszre4.json");
using var pak = new RePakCollection(@"C:\Program Files (x86)\Steam\steamapps\common\RESIDENT EVIL 4  BIOHAZARD RE4");

// Get user data and create new modified version
const string guiParamPath = "natives/stm/_chainsaw/appsystem/ui/userdata/guiparamholdersettinguserdata.user.2";
var userFileBuilder = new UserFile(pak.GetEntryData(guiParamPath)).ToBuilder(repo);
var root = userFileBuilder.Objects[0];
root = root.Set("_InGameShopGuiParamHolder._HoldTime_Purchase", 0.1f);
userFileBuilder.Objects = [root];
var newUserFile = userFileBuilder.Build();

// Create new patch pak file with modified user file
var pakBuilder = new PakFileBuilder();
pakBuilder.AddEntry(guiParamPath, newUserFile.Data);
pakBuilder.Save("re_chunk_000.pak.patch_006.pak");
```

## 🤖 MCP server

`reeutils` also runs as a [Model Context Protocol](https://modelcontextprotocol.io) server, so AI coding assistants can browse, search and inspect game files for you. It exposes the CLI functionality as tools: `open_pak`, `set_game`, `find`, `list_files`, `read`, `search`, `get_type`, `generate_class` and `get_engine_details`, backed by embedded pak listings and RSZ type data for `oniws`, `re2`, `re3`, `re4`, `re7`, `re8` and `re9`.

The server speaks stdio and requires the [.NET 10 SDK](https://dotnet.microsoft.com), which provides `dnx` — the .NET equivalent of `npx`. No install step is needed, the client runs the published tool directly:

```json
{
  "mcpServers": {
    "reeutils": {
      "command": "dnx",
      "args": ["reeutils", "mcp"]
    }
  }
}
```

Put this in your client's MCP configuration file, for example:

- Claude Code / Cursor: `.mcp.json` or `.cursor/mcp.json`, using the `mcpServers` key shown above.
- Claude Desktop: `claude_desktop_config.json`, also using `mcpServers`.
- VS Code: `.vscode/mcp.json`, which uses a top level `servers` key with `"type": "stdio"` alongside the same `command` and `args`.

By default the latest release is used. A version can be pinned the same way as with `npx`, either exactly (`"args": ["reeutils@1.6.0", "mcp"]`) or as a range (`"args": ["reeutils@1.*", "mcp"]`).

If you prefer a permanent install, run `dotnet tool install --global reeutils` and then use `"command": "reeutils"` with `"args": ["mcp"]` instead.

When developing `reeutils` itself, point the client at the local build:

```json
{
  "mcpServers": {
    "reeutils": {
      "command": "dotnet",
      "args": ["run", "--project", "src/reeutils", "--", "mcp"]
    }
  }
}
```

A typical session starts with the assistant calling `open_pak` on a game install directory or `.pak` file, followed by `set_game` (e.g. `re4`), after which it can locate and read files without further setup.

## 📦 Build

- Requirements: [.NET SDK 10.0](https://dotnet.microsoft.com).
- Build the solution:

```bash
dotnet build reeutils.sln
```

## 🧪 Tests

- Run tests with the `dotnet` test runner:

```bash
dotnet test
```

- Note: many tests require access to original game PAK files and RSZ type JSON dumps. You must configure local paths to where Resident Evil games are installed before running those tests.
  - By default the test helper `test/IntelOrca.Biohazard.REE.Tests/OriginalPakHelper.cs` contains example paths used on the author machine. Edit that file to point to your game installation directories or ensure equivalent paths exist on your machine.
  - The RSZ type JSON files used by tests are referenced from a local `reasy` clone in the tests (see `GetTypeRepository` in `OriginalPakHelper.cs`). Place or point those JSON dumps to the expected paths or update the test helper accordingly.

## ⚙️ Design

- The library is designed around immutability and builder patterns for thread-safety and efficient cloning. Builders provide explicit, safe mutation paths while core types remain immutable to allow safe sharing across threads and cheap copy-on-write style operations.

### Supported formats (RE Engine)

| Game | Formats |
| ---- | ------- |
| RE2/RE3 (non-RT) | `.scn.19`, `.pfb.16`, `.fsmv2.30` — full read+write, byte-identical JSON roundtrip (corpus-verified) |
| RE8 | `.fsmv2.40` (BHVT), `.user`, `.scn`, `.pfb` (RSZ-based), `.tex`, `.msg`, `.fsm` |
| Onimusha WotS | `.scn.21`, `.user.3`, `.pfb.18`, `.msg.23`, `.pog.12`, `.poglst.0`, `.cset.6`, `.fsmv2.42` — full read+write, corpus-verified (byte-identical for the point-graph/collider-set/fsmv2 containers) |

> Note: Onimusha `.pog.12` point graphs (placed enemy/item/gimmick nodes, `.poglst.0` lists, and `.cset.6` collider sets) are fully read+write. `.fsmv2.42` behaviour trees now support import for **versions 30, 40, and 42** (RE2/RE3, RE8, and RE9/Onimusha-era).

> Note: when invoking the CLI on versioned files, keep the version suffix (`.19`/`.16`/`.30`) — bare extensions default to the RT/newer versions.


## 🙏 Credits

- Thanks to the REasy project for RSZ JSON dumps and type information used in tests and tooling.
- Historical tooling and ideas inspired by [RSZTool](https://github.com/czastack/RszTool).
- Additional contributions and community tooling from [Namsku](https://github.com/namsku).

## 🧾 License

This repository is MIT licensed.

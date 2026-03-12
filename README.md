# 🧩 reeutils, IntelOrca.Biohazard.REE

[![NuGet](https://img.shields.io/nuget/v/IntelOrca.Biohazard.REE.svg)](https://www.nuget.org/packages/IntelOrca.Biohazard.REE)

A .NET library for working with RE Engine resource formats and PAK files. Primarily developed to support the `biorand` project, but useful for modding most RE engine games.

## 🚀 Usage

Add the package to your project by referencing the NuGet package in your `.csproj`:

```xml
<PackageReference Include="IntelOrca.Biohazard.REE" Version="1.4.2" />
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

## 🙏 Credits

- Thanks to the REasy project for RSZ JSON dumps and type information used in tests and tooling.
- Historical tooling and ideas inspired by [RSZTool](https://github.com/czastack/RszTool).
- Additional contributions and community tooling from [Namsku](https://github.com/namsku).

## 🧾 License

This repository is MIT licensed.

# RE Engine notes for reeutils

`reeutils` focuses on RE Engine resource inspection and transformation, especially RSZ-backed scene/object data and hashed PAK archives.

## Supported embedded games

- `re2`
- `re4`
- `re7`
- `re8`
- `re9`

## Core concepts

- **PAK files** store hashed file entries. A pak list is needed to map hashes back to human-readable paths.
- **RSZ repositories** describe RE Engine type metadata. They are required to decode `.user`, `.scn`, and `.pfb` object graphs into named fields and typed values.
- **REE resource files** commonly inspected by this toolset include:
  - `.msg` message bundles
  - `.user` serialized object data
  - `.scn` scene graphs
  - `.pfb` prefab scene graphs

## Typical MCP workflow

1. Call `open_pak` with either a `.pak` path or a game install directory.
2. Call `set_game` to load embedded RSZ and pak-list data, or use `open_rsz` and `open_pak_list` for custom inputs.
3. Use `list_files`, `search`, `read`, `generate_class`, and `get_type` against the active session state.

## Important behavior

- `set_game` only loads embedded RSZ and pak-list data. It does not infer or open a pak automatically.
- `read` returns JSON for `.msg`, `.user`, `.scn`, and `.pfb`.
- `generate_class` emits C# code from RSZ type metadata.
- `get_type` returns structured field and inheritance information for an RSZ type.

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
  - `.tex` texture headers and mip tables

## Typical MCP workflow

1. Call `open_pak` with either a `.pak` path or a game install directory.
2. Call `set_game` to load embedded RSZ and pak-list data, or use `open_rsz` and `open_pak_list` for custom inputs.
3. Use `list_files`, `search`, `read`, `generate_class`, and `get_type` against the active session state.

## Reading files effectively

For large scene and prefab files, use an iterative approach:

1. **Skeleton view**: `read(path, max_depth=1)` → see top-level objects with names, GUIDs, and component summaries (collapsed).
2. **Target expansion**: `read(path, expand_nodes=["TargetName", "OtherName"])` → get full component data (Position, Rotation, fields) only for matched objects.
3. **Prefix matching**: `expand_nodes=["Root"]` also matches `Root/Child`, `Root/Child/Grandchild` and all descendants.
4. **MAX_depth**: Limits JSON tree depth. `max_depth=0` shows root only. `max_depth=1` shows top-level children. `max_depth=2` adds grandchildren.

Parameter notes:
- `full=true` expands ALL components in the entire file — can produce very large output. Prefer `expand_nodes`.
- `.user` files: `expand_nodes` collapses un-matched sub-objects to `{"@type": "..."}` for a focused view.
- `.msg` files: `expand_nodes` filters entries by name.
- `.fsm` files: `expand_nodes` is not supported.
- `max_depth` works for `.scn` and `.pfb` files (limits scene graph recursion).

## Important behavior

- `set_game` only loads embedded RSZ and pak-list data. It does not infer or open a pak automatically.
- `read` returns JSON for `.msg`, `.user`, `.scn`, and `.pfb`.
- `inspect` prints handler-provided summaries without building: file type, version, size, and type-specific attributes such as texture format and image dimensions.
- `generate_class` emits C# code from RSZ type metadata.
- `get_type` returns structured field and inheritance information for an RSZ type.

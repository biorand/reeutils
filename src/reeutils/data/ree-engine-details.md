# RE Engine notes for reeutils

`reeutils` focuses on RE Engine resource inspection and transformation, especially RSZ-backed scene/object data and hashed PAK archives.

## Supported embedded games

- `re2`
- `re4`
- `re7`
- `re8`
- `re9`
- `oniws` (Onimusha: Way of the Sword) — uses `.scn.21`, `.user.3`, `.pfb.18`, `.fsmv2.42`, `.msg.23`, `.pog.12`, `.poglst.0`, and `.cset.6` containers. `rszoniws.json.gz` derives from the retail game RSZ dump; `paklist.oniws.txt.gz` lists the retail pak contents. Both are pinned upstream data (SHA-256 in the vendoring commit).

## Onimusha point-graph / collider-set containers

Onimusha ships placement and zone data in three RSZ-backed containers that reeutils reads **and** writes (byte-identical roundtrip, corpus-verified):

- `.pog.12` — **point graphs**. A `POG` container (magic `POG\0`, version 12) with a node table and two embedded RSZ streams. The main stream holds the placed-context objects (`app.ContextPointGraphEnemy` / `app.ContextPointGraphGimmick` / `app.ContextPointGraphItem`, etc.) — the per-node type id (e.g. `_Data._EmID`) and world transform (`v2` position, `_Rotation`) — and the secondary stream holds the graph-level container (`app.ContextPointGraph*.cContextLayoutGraph*`). The node table maps each graph node to a position in the main object list, so adding/removing a node edits both `objects` and `nodeTable` in the exported JSON. Two node-section layouts exist: the standard table (header field `0x10` = 0) and the spawner/point-pool name table (`SpnSet_*`, `RandomSetPoint_*`, `0x10` != 0, preserved verbatim). Empty graphs (no nodes) omit the main RSZ stream.
- `.poglst.0` — **point-graph lists**. A `PGL` container (magic `PGL\0`, version 0) that indexes the `.pog` paths a `ContextLayouter` / `RandomSetPointFinder` loads. Editing the `files` array changes a `RandomSet_*_Set{NNN}` variant list's membership. Note the stored paths omit the `.12` version suffix.
- `.cset.6` — **collider sets**. A `CSET` container (magic `CSET`, version 0–8) with a native header + collider geometry (preserved verbatim) and one parameter RSZ stream holding the per-zone objects (`app.col_user_data.*ZoneCollider`). The header/geometry bytes are carried base64 in `@meta-cset`; the `objects` array is the editable zone parameter list.

## fsmv2 import versions

`.fsmv2` (BHVT) **import is now supported for versions 30, 40, and 42**. Version 42 (RE9 / Onimusha-era) differs from 40 in the header (a 4-byte pad after the hash) and in the trailing pools: v42 files place the empty userdata-path pool directly after the resource pool with no 16-byte alignment, and write it as just the count (no char-length field). Onimusha `.fsmv2.42` export→import is byte-identical.

## .user wrapper resources

`.user` files wrap their RSZ stream in a `USR` header plus an optional resource table (8-byte offsets into a UTF-16 string pool) and a userdata table. Onimusha ships many `.user.3` files with a resource table before the RSZ (bank lists, montage parts, trigger-info lists, effect params). The wrapper prefix is preserved verbatim on rebuild (and carried as `@meta-user.prefix` in exports that need it), so editing those files doesn't drop the resource table — dropping it corrupts the file and the game refuses to load it.

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

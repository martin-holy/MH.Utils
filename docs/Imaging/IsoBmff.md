# ISO BMFF

This namespace reads and edits MP4/ISO BMFF box metadata. Editing is staged: metadata edits an `IsoBmffNode` tree; the planner assigns offsets; the differ creates in-place moves and writes; and the executor applies those operations or performs a full rewrite. `IsoBmffFile.Save` orchestrates the appropriate save path.

## Classes

- `IsoBmffFile` — Entry point. Exposes file metadata and XMP, and saves edits using in-place execution or a full rewrite.
- `IsoBmffReader` — Parses boxes, finds boxes and children, and builds the original node tree.
- `IsoBmffBox` — Box type, original offset, header size, and total size.
- `IsoBmffNode` — A box in the original or edited tree. Keeps its original-node link and optional replacement `Data` bytes.
- `IsoBmffEditor` — Manages the edited node tree and applies metadata-entry edits.
- `IsoBmffMetadata` — Reads video properties and coordinates access to item-list and `mdta` metadata stores.
- `IsoBmffItemList` — Reads and edits item-list metadata stored in `ilst`.
- `IsoBmffKeys` — Reads and edits `mdta` metadata.
- `IsoBmffXmp` — Finds XMP in a `uuid` box and builds or updates its payload.
- `IsoBmffMetadataEntry` — Parsed metadata value associated with its source box.
- `IsoBmffTypes` — FourCC constants and type-name helpers.
- `IsoBmffLayoutPlanner` — Assigns offsets, reuses `free` space where possible, and reports when a full rewrite is required.
- `IsoBmffLayout` / `IsoBmffLayoutBox` — Planned box offsets and sizes.
- `IsoBmffLayoutDiffer` — Compares layouts and produces move and write operations.
- `IsoBmffLayoutExecutor` — Executes in-place moves and writes or writes a full rewrite.

## In-place layout algorithm

An in-place plan must fit the edited boxes into the original file’s existing byte ranges and preserves the original file length. If the required space cannot be made available, the planner sets `RequiresFullRewrite`.

### 1. Build the edited tree

The original tree is retained as the baseline. Edited nodes link to their original nodes through `OriginalNode`; new nodes have no original node and initially have offset `-1`. Removed nodes are absent from the edited tree.

`Data` holds bytes to write immediately after that box’s header. For a leaf, it is the replacement payload. For a container, it can hold a prefix before the child boxes, such as the four-byte version/flags prefix of a `meta` box.

Size changes propagate to ancestors in the edited tree. Payload-only changes with unchanged sizes still need writes, but do not require layout changes.

### 2. Create the original layout

The original layout maps each original box identity to its original offset and size. This is the source coordinate system for moves and comparisons. It is not modified while planning.

The planner works with top-level boxes because moving a container moves its contents. Child offsets are calculated later from the edited tree.

### 3. Identify changed roots

Edits to nested boxes are grouped by their top-level ancestor. The planner processes roots whose total size changed and newly added top-level boxes. Unchanged roots are not resized merely because a neighboring `free` box was used.

For each changed root, the planner compares its edited size with its original size:

- **Growth** requires space from adjacent `free` boxes.
- **Shrinkage** creates or enlarges a `free` box.
- **A new top-level box** is placed according to its position in the edited tree and must fit into available `free` space.

### 4. Grow into free space

For a growing root, the planner tries free space after the root first, then before it.

- Taking space from a following `free` box grows the root into the free region. Intervening top-level boxes move right by the amount consumed.
- Taking space from a preceding `free` box reduces that free box and shifts the root left. Boxes between the free box and root shift left by the consumed amount.
- A new root starts with zero allocated bytes at its insertion position and uses the same process.

The planner does not consume a `free` box across an expensive box, such as a large `mdat` at or above the configured threshold. If enough usable free space cannot be found, the in-place plan fails and `RequiresFullRewrite` is set.

### 5. Shrink and normalize free space

When a root shrinks, the released bytes are added to a following `free` box when possible; otherwise, a new `free` region is created after the root.

A `free` region must be large enough for its header. Zero-size regions are removed. Fragments smaller than the header are merged into another reachable `free` region when possible. If a valid layout cannot be formed, the planner requests a full rewrite.

### 6. Produce the edited layout

For an edited root, the planner walks its edited descendants in tree order. Each child begins after the parent header, any container prefix, and the preceding siblings. New boxes therefore receive planned offsets even though their source-tree offsets were `-1`.

Unedited roots retain their original child layout, shifted with their top-level parent when necessary. The resulting layout contains the planned offset and size for every box that remains in the edited tree.

### 7. Generate operations

The differ compares original and edited layouts:

- A moved existing box produces a move unless that move is already covered by a moved ancestor.
- A new box or changed box size produces a header write at the planned offset.
- A node with `Data` produces a payload write at `planned offset + header size`.
- A moved or resized `free` box gets an updated header; its contents are not treated as a move.

Header and payload writes are separate `(Offset, Data)` operations. Same-size payload edits must be passed to the differ even when the layout itself did not change.

### 8. Save the edited file

For an in-place plan, execute all moves before writes. The executor copies overlapping ranges in the safe direction, then writes the supplied bytes at their planned offsets. It does not make planning decisions.

When `RequiresFullRewrite` is true, `IsoBmffFile.Save` uses the full-rewrite path. The executor writes the edited layout to a separate destination stream, regenerating headers and relocating `stco` and `co64` chunk offsets as needed. The file save path then replaces the original file or copies the result back to the caller-provided stream.

## TODO

- Validate `data` box type and locale fields, and define which text encodings are supported. `ReadDataString` currently assumes UTF-8; ensure reads and writes handle or explicitly reject other supported formats.
- Harden `keys` table parsing: validate the key count and each entry size against the containing `keys` box before allocating or reading, and add tests for truncated and malformed tables.
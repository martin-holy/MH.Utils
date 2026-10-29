# MH.Utils

**MH.Utils** is a .NET utility library built around reusable infrastructure for application development: MVVM, data binding, in-memory data management, tree and selection models, asynchronous work, string parsing, binary I/O, and media metadata.

The library targets **.NET 8** and is designed to be used by application code without depending on a particular UI framework. Some UI-related helpers provide framework-neutral building blocks rather than UI controls.

## Highlights

- **Lossless JPEG metadata editing.** Read and update EXIF and XMP metadata without decoding or re-encoding the image. The writer uses in-place binary edits where possible and can rearrange or rewrite the file while preserving the compressed image data. It supports metadata such as keywords, ratings, comments, GPS coordinates, orientation, and people regions.
- **In-place MP4/MOV metadata editing.** Read video dimensions, orientation, duration, and frame rate, and edit supported metadata in ISO Base Media File Format containers without re-encoding video or audio. A write plan describes the required binary changes and handles layouts that need a full rewrite.
- **SimpleDB: a fast in-memory graph database with ORM-style repositories.** Manage objects and their relationships in memory, track changes, and persist records and relations to compact pipe-delimited CSV files. Includes repository and tree-repository patterns, reference linking, schema migration, and backups.
- **BindingU: observable property and collection bindings.** Subscribe to property changes, collection changes, or nested property paths. Nested bindings are rebuilt when an intermediate object changes, and subscriptions can be disposed when no longer needed.
- **StringRange: range-based text parsing.** Search for delimited regions within larger strings, constrain searches to existing ranges, enumerate repeated matches, and convert matched ranges to values. Useful for extracting structured information from semi-structured text without first building a full document model.
- **MVVM command infrastructure.** Synchronous and asynchronous relay commands support command availability, parameters, cancellation, execution status, and bindable command text and icons.
- **Tree, selection, and list models.** Reusable observable items support hierarchical data, flat visible-tree projections, multi-selection, ordering, drag/drop operations, and change notifications.
- **Low-level binary I/O.** Span-based readers and writers, stream writers, and a general-purpose in-place writer support efficient binary parsing and file editing.

## Main components

### MVVM, binding, and observable models

- **`BindingU`** — Binding helpers for `INotifyPropertyChanged` objects and `INotifyCollectionChanged` collections, including nested property paths and collection-valued properties. Bindings return disposable subscriptions.
- **`BindingScope`** — Owns a group of disposable bindings or other resources and releases them together.
- **`ObservableObject`** — Base implementation of `INotifyPropertyChanged` with property-change notification and a helper for updating fields only when values differ.
- **`RelayCommand` / `RelayCommand<T>`** — Synchronous `ICommand` implementations with optional execution predicates and typed parameters.
- **`AsyncRelayCommand` / `AsyncRelayCommand<T>`** — Asynchronous commands with cancellation support, single-execution gating, and observable execution state.
- **`CancelAsyncCommand`** — Cancellation command exposed by asynchronous relay commands.
- **`NotifyTaskCompletion`** — Exposes task completion, result, cancellation, and error state through property notifications for data binding.
- **`ListItem` / `ListItem<T>`** — Observable list presentation models with names, icons, associated data or typed content, selection and visibility flags. Names and icons can fall back to an underlying `IListItem`.
- **`ExtObservableCollection<T>`** — An observable collection with owner context and batch operations that raise consolidated change notifications.
- **`CompositeObservableCollection<T>`** — Combines observable collections into a single collection view.

### SimpleDB and data relationships

- **`MH.Utils.DB.SimpleDB`** — Coordinates data sources, repository and relation loading, reference linking, change tracking, persistence, schema migration, and database backups.
- **`Repository<T>` / `IRepository<T>`** — In-memory item storage and repository operations, with item lifecycle events and change tracking.
- **`TreeRepository<T>` / `ITreeRepository<T>`** — Repository operations for hierarchical items, including create, rename, copy, move, and delete operations.
- **`Relation<TA, TB>` / `IRelation<TA, TB>`** — Represents a relationship between two repositories.
- **`DbTrackable` / `IDbTrackable`** — Observable modification state used by database objects, repositories, and relations.
- **CSV data sources** — `CsvRepositoryDataSource`, `CsvOneToOneDataSource`, `CsvOneToManyDataSource`, and `CsvTreeDataSource` load and save records or relationships, resolve references, and reconstruct object graphs.
- **`CsvParser`** — Parses the library's pipe-delimited record format using span-based operations.

### Trees and selection

- **`ITreeItem` / `TreeItem`** — Hierarchical observable items with parent links, child collections, expansion state, and the presentation features inherited from `ListItem`.
- **`ILeafyTreeItem<T>` / `LeafyTreeItem<T>`** — Tree-item abstractions for typed leaf data.
- **`FlatTree` / `FlatTreeItem`** — Projects a hierarchical tree into a flat list representing its visible, expanded rows and reports range-level structural changes.
- **`TreeU`** — Tree traversal, item lookup, visible indexes, path lookup, and ordered insertion helpers.
- **`TreeItemExtensions`** — Additional tree operations for working with item hierarchies.
- **`SelectionManager<T>`** — Maintains selected items, selection order, and single- or multi-selection state independently of a UI control.
- **`Selecting<T>`** — Observable selection model that coordinates item selection, range selection, and Ctrl/Shift-style selection behavior.
- **`ISelectable` / `IListItem` / `ITreeGroup`** — Small contracts that allow models to participate in shared selection, list, tree, and grouping behavior.

### Text, collections, and extensions

- **`StringRange`** — Finds a substring bounded by configurable start, optional intermediate, and end markers. Supports searching within another range, repeated enumeration, custom conversion, integer conversion, and HTML-decoded text.
- **`Extensions`** — Extension methods grouped by type and purpose, including arrays, characters, dates, numeric values, enumerables, events, hash sets, lists, observable collections, JSON elements, orientation values, progress reporting, property-change events, strings, and commands.
- **Collection helpers** — Sorting and ordered insertion, moving and toggling items, shuffling, selecting adjacent items, null-if-empty conversions, and batch collection updates.
- **`MathU`, `ByteU`, `BitsMasks`, and `Rational`** — Numeric helpers, byte/bounds utilities, bit masks, and rational-number representation.

### Asynchronous work, tasks, and application helpers

- **`Tasks`** — Helpers for dispatching work to a configured UI thread and running work with success/error callbacks.
- **`TaskQueue<T>`** — Processes queued items with a configurable concurrency limit and a completion callback.
- **`WorkTask`** — Runs cancellable asynchronous work and exposes cancellation and lifecycle management.
- **`Log`** — Shared logging entry points for errors and diagnostic information.
- **`Plugin`** — Lightweight plugin-related infrastructure.
- **`DragDropHelper`** — Framework-neutral drag/drop decision helpers for determining whether data can be dragged or dropped and how the operation should behave.
- **`IOExtensions`, `Drives`, `Net`, `Clipboard`, and `Keyboard`** — File/path and drive helpers, network utilities, and application-level clipboard and keyboard abstractions.
- **`PointD`, `SizeD`, and `ThicknessD`** — Simple double-precision geometry value types.

### Binary I/O

- **`BinarySpanReader`** — Reads endian-aware integer, rational, and text values from an in-memory byte buffer with bounds checking.
- **`BinarySpanWriter`** — Writes endian-aware values to a span without requiring a stream abstraction.
- **`BinaryStreamWriter`** — Writes primitive binary values to a stream with configurable byte order.
- **`BinaryInPlaceWriter`** — Applies a write plan containing byte writes and moves to a file or stream. Supports efficient binary edits without rebuilding the entire file when the layout permits, with a full-rewrite path for plans that require it.

### Imaging and metadata

The imaging code is split by format and metadata standard. It focuses on parsing and editing metadata, not image or video encoding.

- **`ImageMetadata`** — Unified access to JPEG dimensions and common EXIF/XMP properties, including orientation, original date, comment, GPS coordinates, rating, keywords, and people regions.
- **`JpegFile`** — Reads JPEG structure and lazily loads EXIF and XMP metadata. Provides metadata writing and metadata-removal operations.
- **`JpegMetadataWriter`** — Writes or removes JPEG metadata segments while preserving the JPEG image data. Extended XMP packets are supported when metadata exceeds a single APP1 segment.
- **`ExifMetadata`** — Reads and updates EXIF values, including user comments, orientation, timestamps, and GPS coordinates.
- **`XmpMetadata` / `XmpDocument`** — Manage XMP packets and their XML/RDF representation, including comments, ratings, keywords, and people regions.
- **`MpRegion` / `MpRegionCollection`** — Read and modify people/face regions represented in XMP metadata.
- **`TiffFile`, `TiffParser`, `TiffSerializer`, `TiffLayoutPlanner`, and related TIFF types** — Parse, represent, plan, and serialize TIFF/EXIF structures, including entry values, offsets, padding, and layout changes.
- **`VideoMetadata`** — Unified metadata access for supported ISO BMFF video files, including dimensions, orientation, duration, frame rate, comments, keywords, ratings, and people regions.
- **`IsoBmffFile`** — Reads ISO Base Media File Format box structures and exposes video metadata and XMP. Supports planned binary edits to metadata without re-encoding media streams.
- **ISO BMFF layout components** — `IsoBmffReader`, `IsoBmffEditor`, and the layout planner/differ/executor types handle box parsing, edited layouts, write planning, and execution.
- **`ImagingU` / `XmpU` / `GpsU`** — Image sizing and hash helpers, XMP utilities, and GPS coordinate conversions.

## Extensions overview

The extension-method namespaces provide focused helpers for common application code:

- **Strings:** parsing, universal decimal parsing, index search, newline normalization, pluralization, and snake-case conversion.
- **Collections:** sorting, ordered insertion, moving, toggling, shuffling, adjacent-item lookup, and empty/null handling.
- **Dates and numbers:** date/time operations, numeric rounding, formatting, and orientation conversion.
- **Events and binding:** event-handler helpers, command execution checks, and property-change event-name matching.
- **JSON:** safe property access and conversion to nullable values, arrays, and objects.
- **I/O and progress:** path and file-name helpers, file-size formatting, directory cleanup, and timestamped progress reporting.

## Installation

MH.Utils is available as the **`MH.Utils`** NuGet package.

- NuGet: [MH.Utils](https://www.nuget.org/packages/MH.Utils/)
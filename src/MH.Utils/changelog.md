5.0.0 (not released):
	- [U] ListItem: Icon and Name from Data if Data is IListItem
	- [N] CompositeObservableCollection
	- [N] MenuItem: AddSource methods for composite MenuItems
	- [U] Tree: ToFlatTreeItems: hidden items not included to result
	- [N] Tree: ITreeItem.IsVisible extension method
	- [U] FlatTree: Hidden items are not included to FlatTree.Items
	- [N] FlatTreeTests
	- [N] DB DataSource
	- [N] DB CsvDataSource
	- [N] DB CsvRepositoryDataSource
	- [N] DB CsvTreeDataSource
	- [N] DB CsvOneToOneDataSource
	- [N] DB CsvOneToManyDataSource
	- [N] SimpleDB for CsvDataSources reducing ~50% memory allocation
	- [N] DB Repository
	- [N] DB TreeRepository
	- [N] DB Relation
	- [N] DB OneToOneRelation
	- [N] DB DbTrackable
	- [N] DB IdParser
	- [U] BaseClasses.Repository obsolete
	- [N] StringExtensions: ReadOnlySpan<char>.ToDouble
	- [U] Old SimpleDB obsolete
	- [N] TreeItemExtensions: HasVisibleChildren
	- [N] FlatTreeItem: HasVisibleChildren prop
	- [U] FlatTree: HasVisibleChildren
	- [N] TreeItemExtensions: ExpandToRoot
	- [U] TreeItem extension methods moved from MH.Utils.Tree to MH.Utils.Tree.TreeItemExtensions
	- [U] MH.Utils.Tree methods moved to MH.Utils.Tree.TreeU
	- [U] Tree.GetParentOf changed to extension method
	- [U] Tree.ToFlatTreeItems changed to extension method
	- [U] Tree.ItemMove changed to extension method and renamed to Move
	- [U] All Tree related classes moved to MH.Utils.Tree
	- [U] Old SimpleDB DataAdapters set as obsolete
	- [N] TreeItemExtensions: GetThisAndItems<T>
	- [U] MH.Utils.Imaging moved to MH.Utils.Imaging.ImagingU
	- [U] Orientation enum moved out of ImagingU class

4.0.0:
	- [U] Tasks: RunOnUiThread
	- [N] Tasks: DoWorkAsync
	- [U] Tasks: UiTaskScheduler removed
	- [U] WorkTask: Start(Func<CancellationToken, Task> work)
	- [U] BindingU: obsolete removed
	- [U] ViewBinder: removed (use MH.UI.ViewBinder instead)
	- [U] Tree: ItemMove: collapse old parent if it is empty
	- [B] BindingU: Binding to same source and property with different getter

3.5.0:
	- [U] Tree: ToFlatTreeItems method with startLevel param
	- [N] IHasOwner
	- [N] ExtObservableCollection: Owner property
	- [N] ExtObservableCollectionExtensions: Sort
	- [U] ExtObservableCollection: NotifyCollectionChangedEventArgs
	- [U] TreeItem: Items with owner
	- [U] ICollectionViewRow: Hash
	- [N] FlatTree
	- [N] MenuItemSeparator
	- [U] MenuItem: with items as ITreeItem to support separator
	- [N] BindingU: Bind methods without target
	- [N] Tree: DoForAll method
	- [U] MenuItem: command with optional icon ctor
	- [N] BindingScope
	- [N] DisposableExtensions: DisposeWith BindingScope
	- [C] ViewBinder: Obsolete, use MH.UI.Binding.ViewBinder instead

3.4.0:
	- [N] SelectionManager
	- [N] Generic ReferenceEqualityComparer

3.3.0:
	- [N] ByteU: Read/Write big median methods
	- [N] ByteU: CopyBytes method
	- [N] ByteU: StartsWith method
	- [N] XmpU: XMP read and lossless write with extended XMP support for JPEG
	- [N] MathU: GreatestCommonDivisor
	- [N] XmpU: UpdateDimensions
	- [U] SimpleDB: Notify Changes change on UI thread

3.2.0:
	- [U] BindingU: invoke init onChange action
	- [N] TreeDataAdapter: ItemMovedEvent
	- [N] SimpleDB: public DbDir property
	- [B] TableDataAdapter: db path to table props file

3.1.0:
	- [N] BindingU: nested binding
	- [U] BindingU: more performant methods without Expression
	- [U] ViewBinder: more performant methods without Expression

3.0.1:
	- [C] ExtObservableCollection: RemoveItems nullable itemAction
	- [B] ExtObservableCollection: Add/Remove Items NotifyCollectionChanged
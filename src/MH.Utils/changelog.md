3.5.0:
	- [U] Tree: ToFlatTreeItems method with startLevel param
	- [N] IHasOwner
	- [N] ExtObservableCollection: Owner property
	- [N] ExtObservableCollectionExtensions: Sort
	- [U] ExtObservableCollection: NotifyCollectionChangedEventArgs
	- [U] TreeItem: Items with owner
	- [U] ICollectionViewRow: Hash
	- [N] FlatTree

3.4.0:
	- [N] SelectionManager
	- [N] Generic ReferenceEqualityComparer

3.3.0:
	- [N] ByteU: Read/Write big endian methods
	- [N] ByteU: CopyBytes method
	- [N] ByteU: StartsWith method
	- [N] XmpU: XMP read and lossless write with extended XMP support for JPEG
	- [N] MathU: GreatestCommonDivisor
	- [N] XmpU: UpdateDimensions
	- [U] SimpleDB: Notifiy Changes change on UI thread

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
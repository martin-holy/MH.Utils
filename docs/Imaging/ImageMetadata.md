# JPEG Metadata

MH.Utils.Imaging provides reading, modifying, and writing metadata in JPEG images.

It supports EXIF and XMP metadata through a high-level `ImageMetadata` API, while also allowing direct access to XMP for custom properties.

**Writing metadata does not recompress or modify the JPEG image data.** Only the metadata segments are changed.

## Read metadata

Common metadata can be read directly through `ImageMetadata`.

```csharp
var metadata = new ImageMetadata("photo.jpg", JpegMetadataLoad.All);

Debug.WriteLine($"Orientation: {metadata.Orientation}");
Debug.WriteLine($"Comment: {metadata.Comment}");
Debug.WriteLine($"Rating: {metadata.Rating}");
Debug.WriteLine($"Width: {metadata.Width}");
Debug.WriteLine($"Height: {metadata.Height}");
Debug.WriteLine($"Keywords: {string.Join(", ", metadata.Keywords ?? [])}");

if (metadata.People is { } people)
  foreach (var person in people)
    Debug.WriteLine($"Person: {person.PersonDisplayName}, rect: {person.Rectangle}");
```

## Write metadata

Common metadata can be modified directly through `ImageMetadata`.

```csharp
var metadata = new ImageMetadata("photo.jpg");

metadata.Rating = 5;
metadata.Comment = "Ocellated lizard";
metadata.Keywords = ["Animal/Lizard", "Outdoor"];

var people = metadata.EnsurePeople();
if (!people.Any(x => x.PersonDisplayName == "Alejandro"))
  people.Add("Alejandro", "0.66875, 0.341667, 0.17125, 0.228333");

metadata.Write("photo.jpg");
```

## Custom XMP properties

The XMP API can be used with custom namespaces and supports properties stored as attributes, elements, and arrays.

## Read custom XMP properties

Custom XMP properties can be read using the same XMP document API.

```csharp
var metadata = new ImageMetadata("photo.jpg", JpegMetadataLoad.All);

if (metadata.Jpeg.Xmp.Doc is { } doc) {
  XNamespace customNs = "customNamespace";

  Debug.WriteLine(doc.GetProperty(customNs + "myAttributeProperty"));
  Debug.WriteLine(doc.GetProperty(customNs + "myElementProperty"));

  var values = doc.GetArray(customNs + "myArrayProperty");
  Debug.WriteLine(string.Join(", ", values ?? []));
}
```

## Write custom XMP properties

Properties that are not exposed by `ImageMetadata` can be accessed directly through the XMP document.

```csharp
var metadata = new ImageMetadata("photo.jpg");
var doc = metadata.Jpeg.Xmp.EnsureDoc();

XNamespace customNs = "customNamespace";
doc.SetProperty(customNs + "myAttributeProperty", "attribute value");
doc.SetProperty(customNs + "myElementProperty", "element value", XmpValueStyle.Element);
doc.SetArray(customNs + "myArrayProperty", ["value1", "value2"]);

metadata.Write("photo.jpg");
```

## Remove metadata

All metadata can be removed from a JPEG without recompressing the image.

```csharp
JpegFile.RemoveMetadata("photo.jpg");
```

## Notes

The library is currently under development. The API may change before the first stable release, and real-world JPEG samples and feedback are welcome.
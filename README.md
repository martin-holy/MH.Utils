# MH.Utils

## Overview

**MH.Utils** is a collection of cross-platform utility classes and extensions designed to simplify common programming tasks in .NET applications. The library focuses on providing reusable components that enhance code readability and reduce boilerplate.

## Features

- **Extensions**: A variety of extension methods for collections, strings, dates, and more.
- **Base Classes**: Fundamental classes like `ObservableObject` and `RelayCommand` for MVVM design patterns.
- **Interfaces**: Common interfaces for tree-like structures and data adapters.
- **Drag and Drop Helpers**: Simplified drag-and-drop functionality for UI applications.
- **JPEG metadata**: Reading and writing EXIF and XMP metadata — including Keywords, People regions, Comment, Rating, GPS, and custom XMP properties — without re-encoding image data. Metadata is written **in place** when there is enough padding space in the existing segments; otherwise the file is rewritten losslessly.  
- **MP4 / MOV metadata**: Reading dimensions, orientation, duration, and frame rate, plus reading and **in-place writing** of Keywords, People, Comment, Rating, and GeoNameId.  
- **SimpleDB**: A lightweight in-memory, CSV-backed database with repositories, relations, and change tracking.  
- **Binary I/O helpers**: Span- and stream-based binary readers/writers (`BinarySpanReader`, `BinarySpanWriter`, `BinaryStreamWriter`) and a general-purpose `BinaryInPlaceWriter` for patching binary data inside files or streams.  
  
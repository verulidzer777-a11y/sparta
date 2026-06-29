# Spartan Language File Tool

A C# .NET 8 console application for reverse-engineering and extracting strings from Spartan (Slitherine, 2004) language files.

## Overview

SpartanLangTool analyzes binary language files (`lang_a.str`, `lang.str`, `lang_eng.str`) from the game Spartan to:

1. **Read binary files** - Loads language files as raw binary data
2. **Print header** - Displays the first 32 bytes in hex and text formats
3. **Detect pointer tables** - Identifies sequences of 32-bit little-endian pointers
4. **Extract UTF-16LE strings** - Scans for and extracts Unicode strings encoded in UTF-16LE format
5. **Save to file** - Exports all extracted strings to `strings.txt`

## Usage

```bash
cd SpartanLangTool
dotnet build
dotnet run
```

Place `lang_a.str` in the same directory as the executable, or modify the `inputFile` variable in `Program.cs`.

## Output

- **strings.txt** - Contains all extracted strings, one per line, sorted alphabetically
- **Console output** - Shows header information, pointer table locations, and extraction statistics

## File Formats

The tool specifically targets:
- `lang_a.str` - Steam version
- `lang.str` - Russian version
- `lang_eng.str` - English version
- `Localisation.txt` - Possible text-based localization file
- `Localisation_a.txt` - Alternative localization file

## Technical Details

- **Encoding**: UTF-16LE (Little-Endian Unicode)
- **.NET Version**: .NET 8.0
- **Language**: C#
- **Null Safety**: Enabled (`<Nullable>enable</Nullable>`)

## Implementation Features

### Header Analysis
Displays the raw binary header in hexadecimal and attempts ASCII interpretation.

### Pointer Table Detection
Scans for sequences of 32-bit little-endian pointers that:
- Point within file bounds
- Are monotonically increasing
- Occur in sequences of 3+ pointers

### String Extraction
Locates and extracts UTF-16LE encoded strings by:
- Following pointers from detected tables
- Scanning for valid string starts (printable ASCII or Unicode characters)
- Reading until null terminator (0x0000)
- Filtering out control characters
- Limiting extraction to 1000 characters per string

### Deduplication
Uses a `HashSet<string>` to eliminate duplicate strings across the file.

## Future Enhancements

- Support for other encoding formats
- Multi-file batch processing
- String offset mapping for game patching
- Reverse lookup (string to offset)
- Export to multiple formats (CSV, JSON, XML)

## License

This tool is provided for reverse-engineering and research purposes.

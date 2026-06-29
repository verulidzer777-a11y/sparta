using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpartanLangTool
{
    class Program
    {
        static void Main(string[] args)
        {
            const string inputFile = "lang_a.str";
            const string outputFile = "strings.txt";

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: {inputFile} not found.");
                return;
            }

            Console.WriteLine("=== Spartan Lang Tool ===");
            Console.WriteLine($"Reading {inputFile}...");

            try
            {
                byte[] fileData = File.ReadAllBytes(inputFile);
                Console.WriteLine($"File size: {fileData.Length} bytes\n");

                // Print header (first 32 bytes)
                PrintHeader(fileData);

                // Detect pointer tables
                Console.WriteLine("\n=== Detecting Pointer Tables ===");
                var pointerTables = DetectPointerTables(fileData);
                Console.WriteLine($"Found {pointerTables.Count} potential pointer tables\n");

                // Extract UTF-16LE strings
                Console.WriteLine("=== Extracting UTF-16LE Strings ===");
                var extractedStrings = ExtractUTF16LEStrings(fileData, pointerTables);
                Console.WriteLine($"Extracted {extractedStrings.Count} strings\n");

                // Save to file
                SaveStrings(extractedStrings, outputFile);
                Console.WriteLine($"Strings saved to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void PrintHeader(byte[] data)
        {
            Console.WriteLine("Header (first 32 bytes as hex):");
            int headerSize = Math.Min(32, data.Length);
            for (int i = 0; i < headerSize; i++)
            {
                Console.Write($"{data[i]:X2} ");
                if ((i + 1) % 16 == 0)
                    Console.WriteLine();
            }
            Console.WriteLine();

            Console.WriteLine("Header (first 32 bytes as ASCII/UTF-16LE where possible):");
            var headerBytes = data.Take(Math.Min(32, data.Length)).ToArray();
            try
            {
                string headerStr = Encoding.UTF8.GetString(headerBytes).Replace("\0", ".");
                Console.WriteLine($"UTF-8: {headerStr}");
            }
            catch { }
        }

        static List<uint> DetectPointerTables(byte[] data)
        {
            var pointerTables = new List<uint>();
            
            // Look for sequences of 32-bit little-endian pointers
            // Pointers typically point within the file and are somewhat sequential
            for (int i = 0; i < data.Length - 4; i += 4)
            {
                uint pointer = BitConverter.ToUInt32(data, i);
                
                // Check if pointer is within reasonable file bounds
                if (pointer > 0 && pointer < data.Length && pointer < data.Length - 100)
                {
                    // Check if next few values are also valid pointers (increasing)
                    bool likelyPointerTable = true;
                    uint prevPointer = pointer;
                    
                    int sequenceCount = 0;
                    for (int j = i + 4; j < Math.Min(i + 40, data.Length - 4); j += 4)
                    {
                        uint nextPointer = BitConverter.ToUInt32(data, j);
                        if (nextPointer > 0 && nextPointer < data.Length && nextPointer >= prevPointer)
                        {
                            sequenceCount++;
                            prevPointer = nextPointer;
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    if (sequenceCount >= 2)
                    {
                        pointerTables.Add((uint)i);
                        i += sequenceCount * 4; // Skip ahead
                    }
                }
            }

            foreach (var offset in pointerTables)
            {
                Console.WriteLine($"Pointer table at offset: 0x{offset:X8}");
            }

            return pointerTables;
        }

        static List<string> ExtractUTF16LEStrings(byte[] data, List<uint> pointerTables)
        {
            var extractedStrings = new HashSet<string>();
            var stringOffsets = new HashSet<int>();

            // Collect offsets from pointer tables
            foreach (var tableOffset in pointerTables)
            {
                int offset = (int)tableOffset;
                while (offset < data.Length - 4)
                {
                    uint pointer = BitConverter.ToUInt32(data, offset);
                    if (pointer > 0 && pointer < data.Length)
                    {
                        stringOffsets.Add((int)pointer);
                    }
                    else
                    {
                        break;
                    }
                    offset += 4;
                }
            }

            // Also scan the entire file for UTF-16LE strings
            for (int i = 0; i < data.Length - 2; i += 2)
            {
                if (IsValidUTF16LEStringStart(data, i))
                {
                    stringOffsets.Add(i);
                }
            }

            // Extract strings from offsets
            foreach (var offset in stringOffsets.OrderBy(x => x))
            {
                string str = ExtractUTF16LEString(data, offset);
                if (!string.IsNullOrEmpty(str) && str.Length > 2) // Filter out very short/empty strings
                {
                    extractedStrings.Add(str);
                }
            }

            return extractedStrings.OrderBy(s => s).ToList();
        }

        static bool IsValidUTF16LEStringStart(byte[] data, int offset)
        {
            // Check if there's a valid UTF-16LE character sequence
            if (offset + 2 > data.Length)
                return false;

            // Look for common starting characters or ASCII range
            ushort firstChar = BitConverter.ToUInt16(data, offset);
            
            // ASCII printable range or common Unicode
            if ((firstChar >= 0x20 && firstChar <= 0x7E) || // ASCII printable
                (firstChar >= 0xA0 && firstChar <= 0xFFFF))  // Extended ASCII and Unicode
            {
                // Verify there's a null terminator within reasonable distance
                for (int i = offset + 2; i < Math.Min(offset + 1000, data.Length); i += 2)
                {
                    ushort ch = BitConverter.ToUInt16(data, i);
                    if (ch == 0)
                        return true;
                }
            }

            return false;
        }

        static string ExtractUTF16LEString(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length)
                return string.Empty;

            try
            {
                var chars = new List<char>();
                int i = offset;
                
                while (i + 1 < data.Length)
                {
                    ushort ch = BitConverter.ToUInt16(data, i);
                    
                    if (ch == 0)
                        break; // Null terminator
                    
                    // Skip control characters except common ones
                    if (char.IsControl((char)ch) && ch != 0x09 && ch != 0x0A && ch != 0x0D)
                    {
                        break;
                    }
                    
                    chars.Add((char)ch);
                    i += 2;
                    
                    // Reasonable string length limit
                    if (chars.Count > 1000)
                        break;
                }

                return new string(chars.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        static void SaveStrings(List<string> strings, string outputFile)
        {
            using (StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8))
            {
                writer.WriteLine($"// Extracted strings from lang_a.str");
                writer.WriteLine($"// Total strings: {strings.Count}");
                writer.WriteLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();

                foreach (var str in strings)
                {
                    writer.WriteLine(str);
                }
            }
        }
    }
}

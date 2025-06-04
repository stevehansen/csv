using System;
using System.Linq;
using Csv;

// Test 1: The problematic case
string importDataString = "Test;\"A\nB\nC\nD\nE\nF\nG\nH\";testing with very long string;123123";
var options = new CsvOptions
{
    Separator = ';',
    HeaderMode = HeaderMode.HeaderAbsent,
    AllowNewLineInEnclosedFieldValues = true,
    AllowBackSlashToEscapeQuote = false,
};

Console.WriteLine("=== Test 1: Multiline quoted field ===");
var result = CsvReader.ReadFromText(importDataString, options).ToArray();

Console.WriteLine($"Number of records: {result.Length}");
if (result.Length > 0)
{
    var record = result[0];
    Console.WriteLine($"ColumnCount: {record.ColumnCount}");
    Console.WriteLine($"Headers.Length: {record.Headers.Length}");
    Console.WriteLine($"Headers: [{string.Join(", ", record.Headers.Select(h => $"\"{h}\""))}]");
    Console.WriteLine($"Values.Length: {record.Values.Length}");
    Console.WriteLine($"Values: [{string.Join(", ", record.Values.Select(v => $"\"{v.Replace("\n", "\\n")}\""))}]");
}

// Test 2: Same data but without newlines in quotes - should work correctly
Console.WriteLine("\n=== Test 2: Same data without newlines ===");
string importDataString2 = "Test;\"ABCDEFGH\";testing with very long string;123123";
var result2 = CsvReader.ReadFromText(importDataString2, options).ToArray();

Console.WriteLine($"Number of records: {result2.Length}");
if (result2.Length > 0)
{
    var record = result2[0];
    Console.WriteLine($"ColumnCount: {record.ColumnCount}");
    Console.WriteLine($"Headers.Length: {record.Headers.Length}");
    Console.WriteLine($"Headers: [{string.Join(", ", record.Headers.Select(h => $"\"{h}\""))}]");
    Console.WriteLine($"Values.Length: {record.Values.Length}");
    Console.WriteLine($"Values: [{string.Join(", ", record.Values.Select(v => $"\"{v}\""))}]");
}

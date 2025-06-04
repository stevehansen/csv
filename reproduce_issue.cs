using System;
using System.Linq;
using Csv;

class Program
{
    static void Main()
    {
        string importDataString = "Test;\"A\nB\nC\nD\nE\nF\nG\nH\";testing with very long string;123123";
        var options = new CsvOptions
        {
            Separator = ';',
            HeaderMode = HeaderMode.HeaderAbsent,
            AllowNewLineInEnclosedFieldValues = true,
            AllowBackSlashToEscapeQuote = false,
        };

        var result = CsvReader.ReadFromText(importDataString, options).ToArray();
        
        Console.WriteLine($"Number of records: {result.Length}");
        if (result.Length > 0)
        {
            var record = result[0];
            Console.WriteLine($"ColumnCount: {record.ColumnCount}");
            Console.WriteLine($"Headers.Length: {record.Headers.Length}");
            Console.WriteLine($"Headers: [{string.Join(", ", record.Headers.Select(h => $"\"{h}\""))}]");
            Console.WriteLine($"Values.Length: {record.Values.Length}");
            Console.WriteLine($"Values: [{string.Join(", ", record.Values.Select(v => $"\"{v}\""))}]");
        }
    }
}
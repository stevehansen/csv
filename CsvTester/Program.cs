using System.Diagnostics;
using System.Linq;
using Csv;

namespace CsvTester
{
    static class Program
    {
        static void Main()
        {
            Test1Comma();
            Test2Semicolon();
            Test3SkipRows();
            Test4TrimData();
        }

        static void Test1Comma()
        {
            var lines = CsvReader.ReadFromText("A,B,C\n1,2,3\n4,5,6").ToArray();
            Debug.Assert(lines.Length == 2);
            Debug.Assert(lines[0][0] == "1");
            Debug.Assert(lines[0]["A"] == "1");
            Debug.Assert(lines[1]["A"] == "4");
            Debug.Assert(lines[1][2] == "6");
            Debug.Assert(lines[1]["C"] == "6");
        }

        static void Test2Semicolon()
        {
            var lines = CsvReader.ReadFromText("A;B;C\n1;2;3\n4;5;6").ToArray();
            Debug.Assert(lines.Length == 2);
            Debug.Assert(lines[0][0] == "1");
            Debug.Assert(lines[0]["A"] == "1");
            Debug.Assert(lines[1]["A"] == "4");
            Debug.Assert(lines[1][2] == "6");
            Debug.Assert(lines[1]["C"] == "6");
        }

        static void Test3SkipRows()
        {
            var lines = CsvReader.ReadFromText("skip this\nand this\nA,B,C\n1,2,3\n4,5,6", new CsvOptions { RowsToSkip = 2 }).ToArray();
            Debug.Assert(lines.Length == 2);
            Debug.Assert(lines[0][0] == "1");
            Debug.Assert(lines[0]["A"] == "1");
            Debug.Assert(lines[1]["A"] == "4");
            Debug.Assert(lines[1][2] == "6");
            Debug.Assert(lines[1]["C"] == "6");
        }

        static void Test4TrimData()
        {
            var lines = CsvReader.ReadFromText(" A , B ,  C\n1   ,2   ,3\n   4,5,    6", new CsvOptions { TrimData = true }).ToArray();
            Debug.Assert(lines.Length == 2);
            Debug.Assert(lines[0][0] == "1");
            Debug.Assert(lines[0]["A"] == "1");
            Debug.Assert(lines[1]["A"] == "4");
            Debug.Assert(lines[1][2] == "6");
            Debug.Assert(lines[1]["C"] == "6");
        }
    }
}
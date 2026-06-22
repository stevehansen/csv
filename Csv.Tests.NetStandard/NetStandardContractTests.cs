using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests.NetStandard
{
    /// <summary>
    /// Pins the netstandard2.0 build of Csv at runtime. The main Csv.Tests suite runs net9.0
    /// only, so without this project the ns2.0 behavioral path - where the <c>MemoryText</c>
    /// type alias is <c>string</c>, and a <c>default(MemoryText)</c> empty would be <c>null</c>
    /// rather than <c>""</c> - is verified by inspection alone. These tests, together with the
    /// linked <see cref="RowUnificationTests"/>, run against the real ns2.0 assembly.
    /// </summary>
    [TestClass]
    public class NetStandardContractTests
    {
        [TestMethod]
        public void LoadedCsvAssemblyIsTheNetStandardBuild()
        {
            // ICsvLineSpan is a net8.0+ public type. Its absence proves the resolved Csv
            // reference is the netstandard2.0 asset, so the assertions in this project really
            // do exercise the ns2.0 surface rather than an accidentally-loaded net8 build.
            Assert.IsNull(typeof(CsvReader).Assembly.GetType("Csv.ICsvLineSpan"),
                "expected the netstandard2.0 build of Csv (ICsvLineSpan is net8.0+ only)");
        }

        [TestMethod]
        public void MissingColumnUnderReturnEmpty_IsNonNullEmptyString()
        {
            // The headline regression the CsvLine<TPolicy> unification had to avoid: on ns2.0
            // MemoryText == string, so the missing-column empty must be "" and never null.
            var options = new CsvOptions { ReturnEmptyForMissingColumn = true };
            var line = CsvReader.ReadFromText("a,b\n1,2\n", options).Single();

            string value = line["NoSuchCol"];

            Assert.IsNotNull(value, "missing-column lookup must not be null on netstandard2.0");
            Assert.AreEqual(string.Empty, value);
        }

        [TestMethod]
        public void StrictMissingColumn_StillThrows()
        {
            // Without ReturnEmptyForMissingColumn the lookup must still throw, matching the
            // net8 surface (CsvReader.Get -> ArgumentOutOfRangeException with the column name).
            var line = CsvReader.ReadFromText("a,b\n1,2\n").Single();

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = line["NoSuchCol"]);
        }

        [TestMethod]
        public void ReadAndWriteRoundTrip_Work()
        {
            // Smoke test: the core string read + write paths function on the ns2.0 build.
            var line = CsvReader.ReadFromText("name,age\nAlice,30\n").Single();
            Assert.AreEqual("Alice", line["name"]);
            Assert.AreEqual("30", line["age"]);
            Assert.AreEqual("Alice,30", line.Raw);

            var written = CsvWriter.WriteToText(new[] { "name", "age" }, new[] { new[] { "Bob", "40" } });
            StringAssert.Contains(written, "name,age");
            StringAssert.Contains(written, "Bob,40");
        }
    }
}

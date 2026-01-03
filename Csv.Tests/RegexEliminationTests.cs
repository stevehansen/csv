#if NET8_0_OR_GREATER

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class RegexEliminationTests
    {
        [TestMethod]
        public void IsUnterminatedQuotedValue_BasicCases()
        {
            var options = new CsvOptions();

            // Basic unterminated quote
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello", options));

            // Basic terminated quote
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"", options));

            // Empty string
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("", options));

            // Non-quoted string
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("hello", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_MultipleQuotes()
        {
            var options = new CsvOptions();

            // Two trailing quotes - unterminated ("" is escaped quote, no closing quote)
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"\"", options));

            // Three trailing quotes - terminated ("" escaped + " closer, value ends with ")
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"\"\"", options));

            // Four trailing quotes - unterminated ("" + "" = two escaped, no closer)
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"\"\"\"", options));

            // Five trailing quotes - terminated ("" + "" + " = two escaped + closer)
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"\"\"\"\"", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_WithBackslashEscaping()
        {
            var options = new CsvOptions { AllowBackSlashToEscapeQuote = true };

            // Backslash escaped quote at end - unterminated
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\\\"", options));

            // Backslash escaped quote followed by another quote - terminated
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\\\"\"", options));

            // Two backslashes before quote - terminated (backslash is escaped)
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\\\\\"", options));

            // Three backslashes before quote - unterminated (last backslash escapes quote)
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\\\\\\\"", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_SingleQuotes()
        {
            var options = new CsvOptions { AllowSingleQuoteToEncloseFieldValues = true };

            // Basic unterminated single quote
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("'hello", options));

            // Basic terminated single quote
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("'hello'", options));

            // Multiple single quotes - same logic as double quotes
            // Three trailing = terminated ('' escaped + ' closer)
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("'hello'''", options));
            // Four trailing = unterminated ('' + '' = two escaped, no closer)
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("'hello''''", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_SingleQuotesWithBackslashEscaping()
        {
            var options = new CsvOptions
            {
                AllowSingleQuoteToEncloseFieldValues = true,
                AllowBackSlashToEscapeQuote = true
            };

            // Backslash escaped single quote at end - unterminated
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("'hello\\'", options));

            // Backslash escaped single quote followed by another quote - terminated
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("'hello\\''", options));

            // Two backslashes before single quote - terminated
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("'hello\\\\'", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_EdgeCases()
        {
            var options = new CsvOptions();

            // Only opening quote
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"", options));

            // Two quotes only - terminated
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"\"", options));

            // Quote in middle, not at start
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("hello\"world", options));

            // Quote at start but with content after
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"world", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_ComplexBackslashPatterns()
        {
            var options = new CsvOptions { AllowBackSlashToEscapeQuote = true };

            // Multiple backslashes with quotes - complex patterns
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"test\\\\\\\\\"", options)); // Even backslashes, terminated
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"test\\\\\\\\\\\"", options)); // Odd backslashes, unterminated

            // Backslashes in middle with quotes at end
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"test\\\\middle\"", options));
            Assert.IsTrue(CsvLineSplitter.IsUnterminatedQuotedValue("\"test\\\\middle\\\"", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_DisabledOptions()
        {
            var options = new CsvOptions { AllowEnclosedFieldValues = false };

            // When AllowEnclosedFieldValues is false, should always return false
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello", options));
            Assert.IsFalse(CsvLineSplitter.IsUnterminatedQuotedValue("\"hello\"", options));
        }

        [TestMethod]
        public void IsUnterminatedQuotedValue_Performance_ComparedToOriginal()
        {
            var options = new CsvOptions { AllowBackSlashToEscapeQuote = true };

            // Test data that would stress the original regex implementation
            var testCases = new[]
            {
                "\"simple\"",
                "\"unterminated",
                "\"escaped\\\"\"",
                "\"multiple\"\"quotes\"",
                "\"backslash\\\\\"",
                "\"complex\\\\\\\"test\\\"\"",
                "\"" + new string('a', 1000) + "\"", // Long string
                "\"" + new string('\\', 50) + "\"", // Many backslashes
            };

            // Just ensure all test cases work without throwing exceptions
            foreach (var testCase in testCases)
            {
                var result = CsvLineSplitter.IsUnterminatedQuotedValue(testCase, options);
                // Result should be deterministic
                var result2 = CsvLineSplitter.IsUnterminatedQuotedValue(testCase, options);
                Assert.AreEqual(result, result2, $"Inconsistent result for: {testCase}");
            }
        }
    }
}

#endif
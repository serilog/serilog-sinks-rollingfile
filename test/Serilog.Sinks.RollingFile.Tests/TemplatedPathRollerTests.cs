using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Serilog.Sinks.RollingFile.Tests
{
    public class TemplatedPathRollerTests
    {
        [Fact]
        public void SpecifierCannotBeProvidedInDirectory()
        {
            var ex = Assert.Throws<ArgumentException>(() => new TemplatedPathRoller("{Date}\\log.txt"));
            Assert.True(ex.Message.Contains("directory"));
        }
        
        [Fact]
        public void TheLogFileIncludesDateToken()
        {
            var roller = new TemplatedPathRoller("Logs\\log.{Date}.txt");
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 0, out path);
            AssertEqualAbsolute("Logs\\log.20130714.txt", path);
        }

        [Fact]
        public void ANonZeroIncrementIsIncludedAndPadded()
        {
            var roller = new TemplatedPathRoller("Logs\\log.{Date}.txt");
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 12, out path);
            AssertEqualAbsolute("Logs\\log.20130714_012.txt", path);
        }

        static void AssertEqualAbsolute(string path1, string path2)
        {
            var abs1 = Path.GetFullPath(path1);
            var abs2 = Path.GetFullPath(path2);
            Assert.Equal(abs1, abs2);
        }

        [Fact]
        public void TheRollerReturnsTheLogFileDirectory()
        {
            var roller = new TemplatedPathRoller("Logs\\log.{Date}.txt");
            AssertEqualAbsolute("Logs", roller.LogFileDirectory);
        }

        [Fact]
        public void IfNoTokenIsSpecifiedDashFollowedByTheDateIsImplied()
        {
            var roller = new TemplatedPathRoller("Logs\\log.txt");
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 0, out path);
            AssertEqualAbsolute("Logs\\log-20130714.txt", path);
        }

        [Fact]
        public void TheLogFileIsNotRequiredToIncludeAnExtension()
        {
            var roller = new TemplatedPathRoller("Logs\\log-{Date}");
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 0, out path);
            AssertEqualAbsolute("Logs\\log-20130714", path);
        }

        [Fact]
        public void TheLogFileIsNotRequiredToIncludeADirectory()
        {
            var roller = new TemplatedPathRoller("log-{Date}");
            var now = new DateTime(2013, 7, 14, 3, 24, 9, 980);
            string path;
            roller.GetLogFilePath(now, 0, out path);
            AssertEqualAbsolute("log-20130714", path);
        }

        [Fact]
        public void MatchingExcludesSimilarButNonmatchingFiles()
        {
            var roller = new TemplatedPathRoller("log-{Date}.txt");
            const string similar1 = "log-0.txt";
            const string similar2 = "log-helloyou.txt";
            var matched = roller.SelectMatches(new[] { similar1, similar2 });
            Assert.Equal(0, matched.Count());
        }

        [Theory]
        [InlineData("Logs\\log-{Date}.txt")]
        [InlineData("Logs\\log-{Hour}.txt")]
        [InlineData("Logs\\log-{HalfHour}.txt")]
        public void TheDirectorSearchPatternUsesWildcardInPlaceOfDate(string template)
        {
            var roller = new TemplatedPathRoller(template);
            Assert.Equal("log-*.txt", roller.DirectorySearchPattern);
        }

        [Theory]
        [InlineData("log-{Date}.txt", "log-20131210.txt", "log-20131210_031.txt")]
        [InlineData("log-{Hour}.txt", "log-2013121013.txt", "log-2013121013_031.txt")]
        [InlineData("log-{HalfHour}.txt", "log-201312100100.txt", "log-201312100230_031.txt")]
        public void MatchingSelectsFiles(string template, string zeroth, string thirtyFirst)
        {
            var roller = new TemplatedPathRoller(template);
            var matched = roller.SelectMatches(new[] { zeroth, thirtyFirst }).ToArray();
            Assert.Equal(2, matched.Count());
            Assert.Equal(0, matched[0].SequenceNumber);
            Assert.Equal(31, matched[1].SequenceNumber);
        }

        [Theory]
        [InlineData("log-{Date}.txt", "log-20150101.txt", "log-20141231.txt")]
        [InlineData("log-{Hour}.txt", "log-2015010110.txt", "log-2015010109.txt")]
        [InlineData("log-{HalfHour}.txt", "log-201501011400.txt", "log-201501011330.txt")]
        public void MatchingParsesSubstitutions(string template, string newer, string older)
        {
            var roller = new TemplatedPathRoller(template);
            var matched = roller.SelectMatches(new[] { older, newer }).OrderByDescending(m => m.DateTime).Select(m => m.Filename).ToArray();
            Assert.Equal(new[] { newer, older }, matched);
        }
    }
}


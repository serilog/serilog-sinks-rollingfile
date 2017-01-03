using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Serilog.Events;
using Serilog.Sinks.RollingFile.Tests.Support;
using Serilog.Configuration;

namespace Serilog.Sinks.RollingFile.Tests
{
    public class RollingFileSinkTests
    {
        [Fact]
        public void LogEventsAreEmittedToTheFileNamedAccordingToTheEventTimestamp()
        {
            TestRollingEventSequence(Some.InformationEvent());
        }

        [Fact]
        public void EventsAreWrittenWhenSharingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.RollingFile(pf, shared: true),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenBufferingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.RollingFile(pf, buffered: true),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenDiskFlushingIsEnabled()
        {
            // Doesn't test flushing, but ensures we haven't broken basic logging
            TestRollingEventSequence(
                (pf, wt) => wt.RollingFile(pf, flushToDiskInterval: TimeSpan.FromMilliseconds(50)),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void WhenTheDateChangesTheCorrectFileIsWritten()
        {
            var e1 = Some.InformationEvent();
            var e2 = Some.InformationEvent(e1.Timestamp.AddDays(1));
            TestRollingEventSequence(e1, e2);
        }

        [Fact]
        public void WhenRetentionCountIsSetOldFilesAreDeleted()
        {
            LogEvent e1 = Some.InformationEvent(),
                     e2 = Some.InformationEvent(e1.Timestamp.AddDays(1)),
                     e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

            TestRollingEventSequence(
                (pf, wt) => wt.RollingFile(pf, retainedFileCountLimit: 2),
                new[] { e1, e2, e3 },
                files =>
                {
                    Assert.Equal(3, files.Count);
                    Assert.True(!System.IO.File.Exists(files[0]));
                    Assert.True(System.IO.File.Exists(files[1]));
                    Assert.True(System.IO.File.Exists(files[2]));
                });
        }

        [Fact]
        public void IfTheLogFolderDoesNotExistItWillBeCreated()
        {
            var fileName = Some.String() + "-{Date}.txt";
            var temp = Some.TempFolderPath();
            var folder = Path.Combine(temp, Guid.NewGuid().ToString());
            var pathFormat = Path.Combine(folder, fileName);

            ILogger log = null;

            try
            {
                log = new LoggerConfiguration()
                    .WriteTo.RollingFile(pathFormat, retainedFileCountLimit: 3)
                    .CreateLogger();

                log.Write(Some.InformationEvent());

                Assert.True(Directory.Exists(folder));
            }
            finally
            {
                var disposable = (IDisposable)log;
                if (disposable != null) disposable.Dispose();
                Directory.Delete(temp, true);
            }
        }

        static void TestRollingEventSequence(params LogEvent[] events)
        {
            TestRollingEventSequence(
                (pf, wt) => wt.RollingFile(pf, retainedFileCountLimit: null),
                events);
        }

        static void TestRollingEventSequence(
            Action<string, LoggerSinkConfiguration> configureFile,
            IEnumerable<LogEvent> events,
            Action<IList<string>> verifyWritten = null)
        {
            var fileName = Some.String() + "-{Date}.txt";
            var folder = Some.TempFolderPath();
            var pathFormat = Path.Combine(folder, fileName);

            var config = new LoggerConfiguration();
            configureFile(pathFormat, config.WriteTo);
            var log = config.CreateLogger();

            var verified = new List<string>();

            try
            {
                foreach (var @event in events)
                {
                    Clock.SetTestDateTimeNow(@event.Timestamp.DateTime);
                    log.Write(@event);

                    var expected = pathFormat.Replace("{Date}", @event.Timestamp.ToString("yyyyMMdd"));
                    Assert.True(System.IO.File.Exists(expected));

                    verified.Add(expected);
                }
            }
            finally
            {
                log.Dispose();
                verifyWritten?.Invoke(verified);
                Directory.Delete(folder, true);
            }
        }
    }
}

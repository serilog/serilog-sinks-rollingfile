using System;
using System.Threading;
using Serilog.Sinks.RollingFile.Tests.Support;
using Xunit;

namespace Serilog.Sinks.RollingFile.Tests
{
    public class RollingFileLoggerConfigurationExtensionsTests
    {
        [Fact]
        public void BufferingIsNotAvailableWhenSharingEnabled()
        {
            Assert.Throws<ArgumentException>(() => 
                new LoggerConfiguration()   
                    .WriteTo.RollingFile("logs", buffered: true, shared: true));
        }

        [Fact]
        public void SinkCanBeConfiguredAndDisposedWhenFlushIntervalSpecified()
        {
            using (var temp = TempFolder.ForCaller())
            using (var logger = new LoggerConfiguration()
                .WriteTo.RollingFile(temp.AllocateFilename(), flushToDiskInterval: TimeSpan.FromMilliseconds(500))
                .CreateLogger())
            {
                logger.Information("Hello, rolling file.");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}

using System;
using Xunit;

namespace Serilog.Tests
{
    public class RollingFileLoggerConfigurationExtensionsTests
    {
        [Fact]
        public void BuffferingIsNotAvailableWhenSharingEnabled()
        {
#if SHARING
            Assert.Throws<ArgumentException>(() => 
                new LoggerConfiguration()
                    .WriteTo.RollingFile("logs", buffered: true, shared: true));
#endif
        }
    }
}

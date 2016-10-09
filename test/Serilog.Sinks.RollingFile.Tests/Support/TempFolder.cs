using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Serilog.Sinks.RollingFile.Tests.Support
{
    class TempFolder : IDisposable
    {
        static readonly Guid Session = Guid.NewGuid();

        readonly string _tempFolder;

        public TempFolder(string name)
        {
            _tempFolder = System.IO.Path.Combine(
                Environment.GetEnvironmentVariable("TMP"),
                "Serilog.Sinks.RollingFile.Tests",
                Session.ToString("n"),
                name);

            Directory.CreateDirectory(_tempFolder);
        }

        public string Path => _tempFolder;

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempFolder))
                    Directory.Delete(_tempFolder, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        public static TempFolder ForCaller([CallerMemberName] string caller = null)
        {
            if (caller == null) throw new ArgumentNullException(nameof(caller));
            return new TempFolder(caller);
        }

        public string AllocateFilename(string ext = null)
        {
            return System.IO.Path.Combine(Path, Guid.NewGuid().ToString("n") + "." + (ext ?? "tmp"));
        }
    }
}

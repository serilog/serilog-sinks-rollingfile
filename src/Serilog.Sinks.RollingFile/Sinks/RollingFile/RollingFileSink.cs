// Copyright 2013-2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.File;
using Serilog.Sinks.RollingFile.RetentionPolicies;

namespace Serilog.Sinks.RollingFile
{
    /// <summary>
    /// Write log events to a series of files. Each file will be named according to
    /// the date of the first log entry written to it. Only simple date-based rolling is
    /// currently supported.
    /// </summary>
    public sealed class RollingFileSink : ILogEventSink, IDisposable
    {
        readonly TemplatedPathRoller _roller;
        readonly ITextFormatter _textFormatter;
        readonly long? _fileSizeLimitBytes;
        readonly IList<IRetentionPolicy> _retentionPolicies;
        readonly Encoding _encoding;
        readonly bool _buffered;
        readonly bool _shared;
        readonly object _syncRoot = new object();

        bool _isDisposed;
        DateTime? _nextCheckpoint;
        ILogEventSink _currentFile;

        /// <summary>Construct a <see cref="RollingFileSink"/>.</summary>
        /// <param name="pathFormat">String describing the location of the log files,
        /// with {Date} in the place of the file date. E.g. "Logs\myapp-{Date}.log" will result in log
        /// files such as "Logs\myapp-2013-10-20.log", "Logs\myapp-2013-10-21.log" and so on.</param>
        /// <param name="textFormatter">Formatter used to convert log events to text.</param>
        /// <param name="fileSizeLimitBytes">The maximum size, in bytes, to which a log file will be allowed to grow.
        /// For unrestricted growth, pass null. The default is 1 GB.</param>
        /// <param name="retainedFileCountLimit">The maximum number of log files that will be retained,
        /// including the current log file. For unlimited retention, pass null. The default is 31.
        /// Exclusive with <paramref name="retainedFileAgeLimit"/>.</param>
        /// <param name="encoding">Character encoding used to write the text file. The default is UTF-8.</param>
        /// <param name="buffered">Indicates if flushing to the output file can be buffered or not. The default
        /// is false.</param>
        /// <param name="shared">Allow the log files to be shared by multiple processes. The default is false.</param>
        /// <param name="retainedFileAgeLimit">The maximum age of log files that will be retained,
        /// including the current log file. For unlimited retention, pass null (default).
        /// This will be applied after <paramref name="retainedFileCountLimit"/>.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>The file will be written using the UTF-8 character set.</remarks>
        public RollingFileSink(string pathFormat,
                              ITextFormatter textFormatter,
                              long? fileSizeLimitBytes,
                              int? retainedFileCountLimit,
                              Encoding encoding = null,
                              bool buffered = false,
                              bool shared = false,
                              TimeSpan? retainedFileAgeLimit = null)
        {
            if (pathFormat == null) throw new ArgumentNullException(nameof(pathFormat));
            if (fileSizeLimitBytes.HasValue && fileSizeLimitBytes < 0) throw new ArgumentException("Negative value provided; file size limit must be non-negative");
            if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1) throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1");
            if (retainedFileAgeLimit.HasValue && retainedFileAgeLimit <= TimeSpan.Zero) throw new ArgumentException("Zero or negative value provided; retained file age limit must be a positive time span");
            
#if !SHARING
            if (shared)
                throw new NotSupportedException("File sharing is not supported on this platform.");
#endif

            if (shared && buffered)
                throw new ArgumentException("Buffering is not available when sharing is enabled.");

            _roller = new TemplatedPathRoller(pathFormat);
            _textFormatter = textFormatter;
            _fileSizeLimitBytes = fileSizeLimitBytes;
            _retentionPolicies = new List<IRetentionPolicy>();
            if (retainedFileCountLimit.HasValue)
            {
                _retentionPolicies.Add(new FileCountRetentionPolicy(_roller, retainedFileCountLimit.Value));
            }
            if (retainedFileAgeLimit.HasValue)
            {
                _retentionPolicies.Add(new FileAgeRetentionPolicy(_roller, retainedFileAgeLimit.Value));
            }
            _encoding = encoding;
            _buffered = buffered;
            _shared = shared;
        }

        /// <summary>
        /// Emit the provided log event to the sink.
        /// </summary>
        /// <param name="logEvent">The log event to write.</param>
        /// <remarks>Events that come in out-of-order (e.g. around the rollovers)
        /// may end up written to a later file than their timestamp
        /// would indicate.</remarks>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            lock (_syncRoot)
            {
                if (_isDisposed) throw new ObjectDisposedException("The rolling file has been disposed.");

                AlignCurrentFileTo(Clock.DateTimeNow);

                // If the file was unable to be opened on the last attempt, it will remain
                // null until the next checkpoint passes, at which time another attempt will be made to
                // open it.
                _currentFile?.Emit(logEvent);
            }
        }

        void AlignCurrentFileTo(DateTime now)
        {
            if (!_nextCheckpoint.HasValue)
            {
                OpenFile(now);
            }
            else if (now >= _nextCheckpoint.Value)
            {
                CloseFile();
                OpenFile(now);
            }
        }

        void OpenFile(DateTime now)
        {
            var date = now.Date;

            // We only take one attempt at it because repeated failures
            // to open log files REALLY slow an app down.
            _nextCheckpoint = date.AddDays(1);

            var existingFiles = Enumerable.Empty<string>();
            try
            {
                existingFiles = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                                         .Select(Path.GetFileName);
            }
            catch (DirectoryNotFoundException) { }

            var latestForThisDate = _roller
                .SelectMatches(existingFiles)
                .Where(m => m.Date == date)
                .OrderByDescending(m => m.SequenceNumber)
                .FirstOrDefault();

            var sequence = latestForThisDate != null ? latestForThisDate.SequenceNumber : 0;

            const int maxAttempts = 3;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                string path;
                _roller.GetLogFilePath(now, sequence, out path);

                try
                {
#if SHARING
                    _currentFile = _shared ?
                        (ILogEventSink)new SharedFileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding) :
                        new FileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered);
#else
                    _currentFile = new FileSink(path, _textFormatter, _fileSizeLimitBytes, _encoding, _buffered);
#endif
                }
                catch (IOException ex)
                {
                    var errorCode = Marshal.GetHRForException(ex) & ((1 << 16) - 1);
                    if (errorCode == 32 || errorCode == 33)
                    {
                        SelfLog.WriteLine("Rolling file target {0} was locked, attempting to open next in sequence (attempt {1})", path, attempt + 1);
                        sequence++;
                        continue;
                    }

                    throw;
                }

                ApplyRetentionPolicies(path);
                return;
            }
        }

        void ApplyRetentionPolicies(string currentFilePath)
        {
            foreach (var policy in _retentionPolicies)
            {
                policy.Apply(currentFilePath);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_currentFile == null) return;
                CloseFile();
                _isDisposed = true;
            }
        }

        void CloseFile()
        {
            if (_currentFile != null)
            {
                (_currentFile as IDisposable)?.Dispose();
                _currentFile = null;
            }

            _nextCheckpoint = null;
        }
    }
}
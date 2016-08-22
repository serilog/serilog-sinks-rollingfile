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

using Serilog.Sinks.RollingFile.RetentionPolicies;
using System;
using System.IO;
using System.Linq;
using Serilog.Debugging;

namespace Serilog.Sinks.RollingFile.Sinks.RollingFile.RetentionPolicies
{
    internal class FileAgeRetentionPolicy : IRetentionPolicy
    {
        private readonly TemplatedPathRoller _roller;
        private readonly TimeSpan _retainedFileAgeLimit;

        public FileAgeRetentionPolicy(TemplatedPathRoller roller, TimeSpan retainedFileAgeLimit)
        {
            if (roller == null)
                throw new ArgumentNullException("roller");

            _roller = roller;
            _retainedFileAgeLimit = retainedFileAgeLimit;
        }

        public void Apply(string currentFilePath)
        {
            var currentFileName = Path.GetFileName(currentFilePath);

            // We consider the current file to exist, even if nothing's been written yet,
            // because files are only opened on response to an event being processed.
            var potentialMatches = Directory.GetFiles(_roller.LogFileDirectory, _roller.DirectorySearchPattern)
                .Select(Path.GetFileName)
                .Union(new[] { currentFileName });

            var newestFirst = _roller
                .SelectMatches(potentialMatches)
                .OrderByDescending(m => m.Date)
                .ThenByDescending(m => m.SequenceNumber)
                .Select(m => new { m.Filename, m.Date });

            var maxAge = DateTimeOffset.Now - _retainedFileAgeLimit;

            var toRemove = newestFirst
                .Where(f => StringComparer.OrdinalIgnoreCase.Compare(currentFileName, f.Filename) != 0
                            && f.Date < maxAge)
                .Select(f => f.Filename)
                .ToList();

            foreach (var obsolete in toRemove)
            {
                var fullPath = Path.Combine(_roller.LogFileDirectory, obsolete);
                try
                {
                    System.IO.File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    SelfLog.WriteLine("Error {0} while removing obsolete file {1}", ex, fullPath);
                }
            }
        }
    }
}

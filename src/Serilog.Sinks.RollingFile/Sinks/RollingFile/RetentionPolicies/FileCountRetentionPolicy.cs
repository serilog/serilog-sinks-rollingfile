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

using Serilog.Debugging;
using System;
using System.IO;
using System.Linq;

namespace Serilog.Sinks.RollingFile.RetentionPolicies
{
    internal class FileCountRetentionPolicy : IRetentionPolicy
    {
        private readonly TemplatedPathRoller _roller;
        private readonly int? _retainedFileCountLimit;

        public FileCountRetentionPolicy(TemplatedPathRoller roller, int? retainedFileCountLimit)
        {
            if (roller == null)
                throw new ArgumentNullException(nameof(roller));

            if (retainedFileCountLimit.HasValue && retainedFileCountLimit < 1)
                throw new ArgumentException("Zero or negative value provided; retained file count limit must be at least 1");

            _roller = roller;
            _retainedFileCountLimit = retainedFileCountLimit;
        }

        public void Apply(string currentFilePath)
        {
            if (_retainedFileCountLimit == null) return;

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
                .Select(m => m.Filename);

            var toRemove = newestFirst
                .Where(n => StringComparer.OrdinalIgnoreCase.Compare(currentFileName, n) != 0)
                .Skip(_retainedFileCountLimit.Value - 1)
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

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace NovaAPI.Services
{
    internal static class NovaFileLogger
    {
        private const int MaxQueuedLines = 10000;
        private static readonly BlockingCollection<LogLine> Queue =
            new BlockingCollection<LogLine>(MaxQueuedLines);

        private static readonly Task Writer = Task.Factory.StartNew(
            WriteLoop,
            TaskCreationOptions.LongRunning);

        public static void AppendLine(string path, string line)
        {
            if (string.IsNullOrWhiteSpace(path) || Queue.IsAddingCompleted)
                return;

            Queue.TryAdd(new LogLine(path, line ?? string.Empty));
        }

        public static void Shutdown()
        {
            try
            {
                Queue.CompleteAdding();
                Writer.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Logging must never block application shutdown.
            }
        }

        private static void WriteLoop()
        {
            foreach (var item in Queue.GetConsumingEnumerable())
            {
                try
                {
                    File.AppendAllText(item.Path, item.Line + Environment.NewLine);
                }
                catch
                {
                    // Logging is best-effort; never bring down request processing.
                }
            }
        }

        private sealed class LogLine
        {
            public LogLine(string path, string line)
            {
                Path = path;
                Line = line;
            }

            public string Path { get; private set; }
            public string Line { get; private set; }
        }
    }
}

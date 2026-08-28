namespace UrlRouter.Services
{
    /// <summary>
    /// Size-capped append-only log of routing decisions. The whole point of this tool is
    /// invisible background behaviour, so when a link lands in the wrong browser this file
    /// is the only way to see what was matched and why.
    /// </summary>
    public static class RouterLog
    {
        private const long MaxBytes = 256 * 1024;

        public static bool Enabled { get; set; } = true;

        public static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UrlRouter");

        public static string LogPath => Path.Combine(LogDirectory, "router.log");

        public static void Write(string message)
        {
            if (!Enabled)
                return;

            try
            {
                Directory.CreateDirectory(LogDirectory);
                RollIfTooLarge();

                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Logging must never be the reason a link fails to open.
            }
        }

        private static void RollIfTooLarge()
        {
            var file = new FileInfo(LogPath);
            if (!file.Exists || file.Length < MaxBytes)
                return;

            File.Move(LogPath, LogPath + ".1", overwrite: true);
        }

        /// <summary>Most recent lines, newest first, for the Setup tab's log view.</summary>
        public static IReadOnlyList<string> ReadRecent(int lines = 200)
        {
            try
            {
                if (!File.Exists(LogPath))
                    return Array.Empty<string>();

                using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                var all = new List<string>();
                while (reader.ReadLine() is { } line)
                    all.Add(line);

                all.Reverse();
                return all.Take(lines).ToList();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }
    }
}

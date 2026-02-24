using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlourishWellness.Services
{
    public class LogService
    {
        private readonly string _logDirectory = "Logs";
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public LogService()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public async Task LogAsync(string action, string userEmail)
        {
            await _semaphore.WaitAsync();
            try
            {
                var fileName = Path.Combine(_logDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");
                var logEntry = $"[{DateTime.Now:HH:mm:ss}] User: {userEmail} - Action: {action}{Environment.NewLine}";
                await File.AppendAllTextAsync(fileName, logEntry);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<string>> GetLogsAsync(DateTime date)
        {
            var fileName = Path.Combine(_logDirectory, $"log-{date:yyyy-MM-dd}.txt");
            if (File.Exists(fileName))
            {
                // Use a shared read if possible, but simple read is usually fine unless writing heavily
                // We can use the semaphore here too to be safe against reading while writing partial line
                await _semaphore.WaitAsync();
                try
                {
                    var lines = await File.ReadAllLinesAsync(fileName);
                    return lines.ToList();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            return new List<string>();
        }

        public List<DateTime> GetAvailableLogDates()
        {
            if (!Directory.Exists(_logDirectory)) return new List<DateTime>();

            var files = Directory.GetFiles(_logDirectory, "log-*.txt");
            var dates = new List<DateTime>();
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file); // log-2023-10-27
                var datePart = fileName.Replace("log-", "");
                if (DateTime.TryParse(datePart, out var date))
                {
                    dates.Add(date);
                }
            }
            return dates.OrderByDescending(d => d).ToList();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ForbiddenWordsScanner
{
    public class ScannerEngine
    {
        public event Action<int>? ProgressChanged;
        public event Action<string>? LogChanged;
        public event Action<Dictionary<string, int>>? ScanCompleted;

        private CancellationTokenSource _cts = new();
        private ManualResetEventSlim _pauseEvent = new(true);
        private readonly object _statsLock = new();
        private Dictionary<string, int> _wordStats = new();

        // THIS METHOD FIXES THE EVENT ERROR
        public void Log(string message)
        {
            LogChanged?.Invoke(message);
        }

        public void Pause() => _pauseEvent.Reset();
        public void Resume() => _pauseEvent.Set();
        public void Stop() => _cts.Cancel();

        public async Task StartScanAsync(string targetFolder, string[] forbiddenWords, string outputFolder)
        {
            _cts = new CancellationTokenSource();
            _pauseEvent.Set();
            _wordStats.Clear();

            await Task.Run(() =>
            {
                try
                {
                    string quarantineFolder = Path.Combine(outputFolder, "Quarantine");
                    string censoredFolder = Path.Combine(outputFolder, "Censored");

                    Directory.CreateDirectory(quarantineFolder);
                    Directory.CreateDirectory(censoredFolder);

                    string[] files = Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);
                    int totalFiles = files.Length;

                    if (totalFiles == 0)
                    {
                        Log("No files found in the folder.");
                        ScanCompleted?.Invoke(new Dictionary<string, int>());
                        return;
                    }

                    int processed = 0;
                    Log($"Found {totalFiles} files. Starting scan...");

                    foreach (string file in files)
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        _pauseEvent.Wait(_cts.Token);

                        processed++;
                        int percent = (int)((double)processed / totalFiles * 100);
                        ProgressChanged?.Invoke(percent);

                        ProcessFile(file, forbiddenWords, quarantineFolder, censoredFolder);
                    }

                    var top10 = _wordStats.OrderByDescending(kv => kv.Value).Take(10).ToDictionary(kv => kv.Key, kv => kv.Value);
                    Log("Scan completed successfully.");
                    ScanCompleted?.Invoke(top10);
                }
                catch (OperationCanceledException)
                {
                    Log("Scan cancelled.");
                }
                catch (Exception ex)
                {
                    Log($"Error: {ex.Message}");
                }
            }, _cts.Token);
        }

        private void ProcessFile(string file, string[] forbiddenWords, string quarantineFolder, string censoredFolder)
        {
            try
            {
                string content = File.ReadAllText(file);
                bool isInfected = false;
                int totalReplacements = 0;

                foreach (string word in forbiddenWords)
                {
                    if (string.IsNullOrWhiteSpace(word)) continue;
                    string pattern = $@"\b{Regex.Escape(word)}\b";
                    MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);

                    if (matches.Count > 0)
                    {
                        isInfected = true;
                        totalReplacements += matches.Count;
                        content = Regex.Replace(content, pattern, "******", RegexOptions.IgnoreCase);

                        lock (_statsLock)
                        {
                            if (!_wordStats.ContainsKey(word.ToLower())) _wordStats[word.ToLower()] = 0;
                            _wordStats[word.ToLower()] += matches.Count;
                        }
                    }
                }

                if (isInfected)
                {
                    string fileName = Path.GetFileName(file);
                    string qPath = Path.Combine(quarantineFolder, fileName);
                    File.Copy(file, qPath, true);

                    string cPath = Path.Combine(censoredFolder, fileName);
                    File.WriteAllText(cPath, content);

                    Log($"Infected: {fileName} | Replacements: {totalReplacements}");
                }
            }
            catch { /* Ignore unreadable files */ }
        }

        public void StartConsoleScan(string folder, string[] words)
        {
            string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    bool infected = false;
                    foreach (var word in words)
                    {
                        if (Regex.IsMatch(content, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase))
                        {
                            content = Regex.Replace(content, $@"\b{Regex.Escape(word)}\b", "******", RegexOptions.IgnoreCase);
                            infected = true;
                        }
                    }
                    if (infected) File.WriteAllText(file + ".censored", content);
                }
                catch { }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Strings_Analyze
{
    public class Scanner
    {
        private string[] strings;
        private List<Pattern> patterns = new List<Pattern>();
        private List<Regex> ignoreList = new List<Regex>();
        private int progress = 0;

        /* Event handlers */
        public EventHandler<ProgressChangedEventArgsProgressEventArgs> OnProgressChanged;
        public class ProgressChangedEventArgsProgressEventArgs : EventArgs {
            public double Value { get; set; }
        }

        /* Constructor */
        public Scanner()
        {
            Regex.CacheSize = 128;

            // check if "DomainIgnoreList.pat" exists
            string ignoreListPath = "DomainIgnoreList.pat";
            if (File.Exists(ignoreListPath))
            {
                // load regular expressions from ignore list
                ignoreList.AddRange(Utils.ReadRegexesFromFile(ignoreListPath));
            }
        }

        /**
         * Check if this `url` or `domain` can be ignored
         */
        private bool CanIgnore(Result result, MatchGroup group)
        {
            if (ignoreList.Count == 0) return false;

            try
            {
                if (group == MatchGroup.url)
                {
                    Uri uri = new Uri(result.Value);
                    string domain = uri.DnsSafeHost;

                    foreach (Regex re in ignoreList)
                    {
                        if (re.IsMatch(domain))
                            return true;
                    }
                }
                else if (group == MatchGroup.domain)
                {
                    foreach (Regex re in ignoreList)
                    {
                        if (re.IsMatch(result.Value))
                            return true;
                    }
                }
            }
            catch (Exception)
            {

            }

            return false;
        }

        /**
         * Load pattern files recursively
         */
        public void Load(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string _path in Directory.EnumerateFileSystemEntries(path))
                {
                    Load(_path);
                }
            } else if (File.Exists(path) && path.EndsWith(".pat"))
            {
                FileInfo info = new FileInfo(path);

                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            try
                            {
                                string line = reader.ReadLine().Trim();
                                if (line.Length == 0 || line.StartsWith("#")) continue;

                                string[] array = Utils.Parse(line);

                                if (array.Length < 4)
                                {
                                    if (line.Length > 255)
                                        line = line.Substring(0, 255);
                                    throw new Exception($"Invalid pattern: {line}");
                                }

                                var pattern = new Pattern
                                {
                                    Type = MatchType.Informative,
                                    Group = MatchGroup.unknown,
                                    Description = array[2]
                                };

                                MatchGroup group;
                                if (Enum.TryParse(array[1], out group))
                                {
                                    pattern.Group = group;
                                } else
                                {
                                    pattern.CustomGroup = array[1];
                                }

                                Regex regex = new Regex(array[3], RegexOptions.Compiled);

                                if (array.Length == 5)
                                {
                                    if (array[4].Contains("i"))
                                    {
                                        regex = new Regex(array[3], RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    if (array[4].Contains("f"))
                                    {
                                        pattern.OptFullString = true;
                                    }
                                }

                                pattern.Regex = regex;

                                if (array[0] == "1") pattern.Type = MatchType.Interesting;
                                if (array[0] == "2") pattern.Type = MatchType.Miscellaneous;
                                if (array[0] == "3") pattern.Type = MatchType.Warning;
                                if (array[0] == "4") pattern.Type = MatchType.Critical;

                                patterns.Add(pattern);
                            }
                            catch (Exception err)
                            {
                                System.Windows.MessageBox.Show("File: " + info.Name + "\n\n" + err.ToString(), "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                                Environment.Exit(1);
                            }
                        }
                    }
                }
            }
        }
        
        /**
         * Scanning thread
         */
        private void Scan(object _args)
        {
            ScanThreadArgs args = (ScanThreadArgs)_args;
            var results = new List<Result>();

            for (int i = 0; i < args.value; i++)
            {
                try
                {
                    string _string = strings[args.offset + i];

                    if (_string.Length > 1200)
                    {
                        _string = _string.Substring(0, 1200);
                    }

                    foreach (var pattern in patterns)
                    {
                        if (pattern.OptFullString)
                        {
                            if (pattern.Regex.IsMatch(_string))
                            {
                                var result = new Result
                                {
                                    Group = (pattern.Group == MatchGroup.unknown ? pattern.CustomGroup : Enum.GetName(typeof(MatchGroup), pattern.Group).ToLower()),
                                    Description = pattern.Description,
                                    Value = _string,
                                    Type = pattern.Type
                                };

                                if (!CanIgnore(result, pattern.Group))
                                    results.Add(result);
                            }

                            continue;
                        }

                        var matches = pattern.Regex.Matches(_string);

                        foreach (Match match in matches)
                        {
                            var result = new Result
                            {
                                Group = (pattern.Group == MatchGroup.unknown ? pattern.CustomGroup : Enum.GetName(typeof(MatchGroup), pattern.Group).ToLower()),
                                Description = pattern.Description,
                                Value = match.Groups[0].Value,
                                Type = pattern.Type
                            };

                            if (!CanIgnore(result, pattern.Group))
                                results.Add(result);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    Environment.Exit(1);
                }

                progress++;
            }

            lock (args.results)
            {
                args.results.AddRange(results);
            }
        }

        private struct ScanThreadArgs
        {
            public int offset;
            public int value;
            public List<Result> results;
        }

        /**
         * Scan strings file using multi-threading
         */
        public List<Result> ScanFile(string path)
        {
            var results = new List<Result>();
            strings = File.ReadAllLines(path);

            const int thread_count = 4;
            var threads = new Thread[thread_count];
            int value = (int)Math.Ceiling((double)strings.Length / (double)thread_count);

            int offset = 0;

            // create and start scanning threads
            for (int i = 0; i < thread_count; i++)
            {
                int real_value = value;

                if (strings.Length < offset + value)
                {
                    real_value = (offset + value) - strings.Length;
                }

                threads[i] = new Thread(new ParameterizedThreadStart(Scan));
                threads[i].Start(new ScanThreadArgs { 
                    offset = offset,
                    value = real_value,
                    results = results
                });

                offset += value;
            }

            // wait for all threads to complete execution
            bool flag = false;

            while (!flag)
            {
                flag = true;

                for (int i = 0; i < thread_count; i++)
                {
                    if (threads[i].IsAlive)
                    {
                        flag = false;
                        break;
                    }
                }

                OnProgressChanged?.Invoke(this, new ProgressChangedEventArgsProgressEventArgs
                {
                    Value = ((double)progress / (double)strings.Length) * (double)100
                });

                Thread.Sleep(1000);
                if (flag) break;
            }

            // return results
            OnProgressChanged?.Invoke(this, new ProgressChangedEventArgsProgressEventArgs { Value = 100 });
            return results;
        }

        internal class Pattern
        {
            public Regex Regex { get; set; }
            public MatchType Type { get; set; }

            public MatchGroup Group { get; set; }
            public string CustomGroup { get; set; }


            public string Description { get; set; }

            public bool OptFullString = false;
        }

        public class Result
        {
            public uint LineNumber { get; set; }
            public string Group { get; set; }
            public string Description { get; set; }
            public string Value { get; set; }
            public MatchType Type { get; set; }
        }

        public enum MatchType
        {
            Informative,   // 0 (black)
            Interesting,   // 1 (black, bold)
            Miscellaneous, // 2 (blue, bold)
            Warning,       // 3 (orange, bold)
            Critical       // 4 (red, bold)
        }

        // hardcoded match groups
        public enum MatchGroup
        {
            unknown,

            malware,
            suspicious,

            antidebug,
            antimalware,
            antivm,

            compression,
            credentials,
            cryptography,
            diagnostic,
            filesystem,
            registry,
            execution,
            http,
            keys,
            library,
            management,
            memory,
            network,
            dyndns,
            obfuscation,
            process,
            ransomware,
            security,
            tor,
            useragent,
            utility,

            domain,
            ip,
            url,

            other,
        }
    }
}

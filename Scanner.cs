using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Diagnostics;

namespace Strings_Analyze
{
    public class Scanner
    {
        private List<Pattern> patterns = new List<Pattern>();
        private List<Regex> ignoreList = new List<Regex>();

        /* Event handlers */
        public EventHandler<ProgrssChangedEventArgsProgressEventArgs> OnProgressChanged;
        public class ProgrssChangedEventArgsProgressEventArgs : EventArgs {
            public double Value { get; set; }
        }

        /* Constructor */
        public Scanner()
        {
            Regex.CacheSize = 256;

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

        public List<Result> Scan(string str)
        {
            var results = new List<Result>();

            foreach (var pattern in patterns)
            {
                try
                {
                    if (pattern.OptFullString)
                    {
                        if (pattern.Regex.IsMatch(str))
                        {
                            var result = new Result
                            {
                                Group = (pattern.Group == MatchGroup.unknown ? pattern.CustomGroup : Enum.GetName(typeof(MatchGroup), pattern.Group).ToLower()),
                                Description = pattern.Description,
                                Value = str,
                                Type = pattern.Type
                            };

                            if (!CanIgnore(result, pattern.Group))
                                results.Add(result);
                        }

                        continue;
                    }

                    var matches = pattern.Regex.Matches(str);

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
                catch (Exception err)
                {
                    System.Windows.MessageBox.Show(
                        "Group:        " + pattern.Group.ToString() + "\n" +
                        "CustomGroup:  " + (pattern.CustomGroup != null ? pattern.CustomGroup : "-") + "\n" +
                        "Description:  " + pattern.Description + "\n\n" + err.ToString(), 
                        "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    Environment.Exit(1);
                }
            }

            return results;
        }

        public List<Result> ScanFile(string path)
        {
            var results = new List<Result>();
            uint linenum = 1;

            long stringsCount = Utils.GetStringCount(path);
            long stringScanned = 0;

            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine().Trim();
                            if (line.Length == 0) continue;

                            string[] strings = line.Split(new char[] { '\0' });

                            foreach (string str in strings)
                            {
                                if (str.Length < 4) continue;

                                List<Result> _results = null;
                                if (str.Length > 1048)
                                {
                                    _results = Scan(str.Remove(1048));
                                }
                                else
                                {
                                    _results = Scan(str);
                                }

                                foreach (var _result in _results)
                                    _result.LineNumber = linenum;

                                results.AddRange(_results);
                            }

                            linenum++;

                            stringScanned += 1;
                            OnProgressChanged?.Invoke(this, new ProgrssChangedEventArgsProgressEventArgs
                            {
                                Value = ((double)stringScanned / (double)stringsCount) * (double)100
                            });
                        }

                        OnProgressChanged?.Invoke(this, new ProgrssChangedEventArgsProgressEventArgs { Value = 100 });
                    }
                }
            }
            catch (Exception err)
            {
                System.Windows.MessageBox.Show(err.ToString(), "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            } finally
            {
                //System.Windows.MessageBox.Show("Analysis finished!");
            }

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

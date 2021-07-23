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

        /* Event handlers */
        public EventHandler<ProgrssChangedEventArgsProgressEventArgs> OnProgressChanged;
        public class ProgrssChangedEventArgsProgressEventArgs : EventArgs {
            public double Value { get; set; }
        }

        /* Constructor */
        public Scanner()
        {
            Regex.CacheSize = 128;
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
#if DEBUG
                //Trace.WriteLine($"[*] Reading patterns from file \"{info.Name}\"");
#endif

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
                                    Group = array[1],
                                    Description = array[2],
                                    Type = MatchType.Informative
                                };

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

#if DEBUG
                            //Trace.WriteLine($"[*]   Loaded pattern {pattern.GetHashCode()}");
#endif
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

#if DEBUG
            //Trace.WriteLine($"[*] All patterns loaded");
#endif
        }

        public List<Result> Scan(string str)
        {
            var results = new List<Result>();

            foreach (var pattern in patterns)
            {
                try
                {
#if DEBUG
                //Trace.WriteLine($"[*] Testing \"{str}\" against pattern {signature.GetHashCode()}");
#endif

                    if (pattern.OptFullString)
                    {
                        if (pattern.Regex.IsMatch(str))
                        {
                            results.Add(new Result
                            {
                                Group = pattern.Group,
                                Description = pattern.Description,
                                Value = str,
                                Type = pattern.Type
                            });
                        }

                        continue;
                    }

                    var matches = pattern.Regex.Matches(str);

#if DEBUG
                //if (matches.Count == 0)
                //{
                //    Trace.WriteLine($"[*]   No matches");
                //} else
                //{
                //    Trace.WriteLine($"[*]   Found {matches.Count} matches");
                //}
#endif

                    foreach (Match match in matches)
                    {
                        var result = new Result
                        {
                            Group = pattern.Group,
                            Description = pattern.Description,
                            Value = match.Groups[0].Value,
                            Type = pattern.Type
                        };

                        results.Add(result);
                    }
                }
                catch (Exception err)
                {
                    System.Windows.MessageBox.Show(
                        "Group: " + pattern.Group + "\n" + 
                        "Description: " + pattern.Description + "\n\n" + err.ToString(), 
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
#if DEBUG
            //Trace.WriteLine($"[*] Started scanning file \"{path}\"");
#endif

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

#if DEBUG
                        //Trace.WriteLine($"[*]   Line number = {linenum}");
#endif

                            linenum++;

                            stringScanned += 1;
                            OnProgressChanged?.Invoke(this, new ProgrssChangedEventArgsProgressEventArgs
                            {
                                Value = ((double)stringScanned / (double)stringsCount) * (double)100
                            });
                        }

                        // completed
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

            // do something here
            // ...

            return results;
        }

        internal class Pattern
        {
            public Regex Regex { get; set; }
            public MatchType Type { get; set; }
            public string Group { get; set; }
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
    }
}

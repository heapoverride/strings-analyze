using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Strings_Analyze
{
    class Utils
    {
        public static Regex[] ReadRegexesFromFile(string path)
        {
            List<Regex> regexes = new List<Regex>();

            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine().Trim();

                        if (line.Length > 0 && !line.StartsWith("#"))
                        {
                            regexes.Add(new Regex(line, RegexOptions.IgnoreCase | RegexOptions.Compiled));
                        }
                    }
                }
            }

            return regexes.ToArray();
        }

        //public static long GetStringCount(string path)
        //{
        //    long strings = 0;

        //    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        //    {
        //        using (StreamReader reader = new StreamReader(stream))
        //        {
        //            while (!reader.EndOfStream)
        //            {
        //                string line = reader.ReadLine();
        //                if (line.Length > 0) strings++;
        //            }
        //        }
        //    }

        //    return strings;
        //}

        public static string[] Parse(string text, char quote = '"')
        {
            List<string> results = new List<string>();
            string temp = "";
            bool inquote = false;

            char[] array = text.ToCharArray();

            for (int i=0; i<array.Length; i++)
            {
                if (!inquote)
                {
                    if (array[i] == ' ')
                    {
                        if (temp.Length > 0) 
                            results.Add(temp);
                        temp = "";
                    }
                    else if (array[i] == quote)
                    {
                        inquote = true;
                    }
                    else
                    {
                        temp += array[i];
                    }
                }
                else
                {
                    if (array[i] == quote)
                    {
                        results.Add(temp);
                        temp = "";
                        inquote = false;
                    } else if (array[i] == '\\' && array.Length > i + 1) {
                        switch (array[i+1])
                        {
                            case '"':
                                temp += "\""; i++;
                                break;

                            case '\\':
                                temp += "\\"; i++;
                                break;

                            default:
                                temp += array[i];
                                break;
                        }
                    } else
                    {
                        temp += array[i];
                    }
                }
            }

            if (inquote)
                throw new Exception("Parser error");

            if (temp.Length > 0)
                results.Add(temp);

            return results.ToArray();
        }

		public static class FlashWindow
		{
			public const uint FLASHW_STOP = 0;
			public const uint FLASHW_CAPTION = 1;
			public const uint FLASHW_TRAY = 2;
			public const uint FLASHW_ALL = 3;
			public const uint FLASHW_TIMER = 4;
			public const uint FLASHW_TIMERNOFG = 12;

			public static bool Flash(IntPtr handle)
			{
				if (Win2000OrLater)
				{
					WinAPI.FLASHWINFO fi = Create_FLASHWINFO(handle, FLASHW_ALL | FLASHW_TIMERNOFG, uint.MaxValue, 0);

					return WinAPI.FlashWindowEx(ref fi);
				}
				return false;
			}

			private static WinAPI.FLASHWINFO Create_FLASHWINFO(IntPtr handle, uint flags, uint count, uint timeout)
			{
				WinAPI.FLASHWINFO fi = new WinAPI.FLASHWINFO();
				fi.cbSize = Convert.ToUInt32(Marshal.SizeOf(fi));
				fi.hwnd = handle;
				fi.dwFlags = flags;
				fi.uCount = count;
				fi.dwTimeout = timeout;

				return fi;
			}

			public static bool Flash(IntPtr handle, uint count)
			{
				if (Win2000OrLater)
				{
					WinAPI.FLASHWINFO fi = Create_FLASHWINFO(handle, FLASHW_ALL, count, 0);
					return WinAPI.FlashWindowEx(ref fi);
				}
				return false;
			}

			public static bool Start(IntPtr handle)
			{
				if (Win2000OrLater)
				{
					WinAPI.FLASHWINFO fi = Create_FLASHWINFO(handle, FLASHW_ALL, uint.MaxValue, 0);
					return WinAPI.FlashWindowEx(ref fi);
				}
				return false;
			}

			public static bool Stop(IntPtr handle)
			{
				if (Win2000OrLater)
				{
					WinAPI.FLASHWINFO fi = Create_FLASHWINFO(handle, FLASHW_STOP, uint.MaxValue, 0);
					return WinAPI.FlashWindowEx(ref fi);
				}
				return false;
			}

			private static bool Win2000OrLater
			{
				get { return System.Environment.OSVersion.Version.Major >= 5; }
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows.Interop;

namespace Strings_Analyze
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        MainWindow window;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            window = new MainWindow();
            window.Show();

            //string text = System.IO.File.ReadAllText("parse.txt");
            //System.Windows.MessageBox.Show(String.Join("\n", Utils.Parse(text)));
            //Environment.Exit(0);

            // Future format idea!
            /*
                    + "malware" 3 "Predator stealer (Predator the Thief)"
                        - "\\\\AppData\\\\Roaming\\\\.+\\\\General\\\\IeEdgePasswords.txt" i
                        - "Predator The Thief"
             */

#if DEBUG
            Trace.WriteLine("Strings Analyze launched");
#endif

            if (e.Args.Length == 0)
            {
                OpenFileDialog dialog = new OpenFileDialog
                {
                    CheckFileExists = true,
                    ShowReadOnly = true
                };

                dialog.FileOk += Dialog_FileOk;

                bool? result = dialog.ShowDialog();

                if (result.HasValue && !result.Value)
                    Environment.Exit(0);
            } else if (e.Args.Length == 1)
            {
                string filepath = e.Args[0];

                if (!File.Exists(filepath))
                {
                    MessageBox.Show(new Exception($"File \"{filepath}\" not found!").ToString());
                    Environment.Exit(1);
                    return;
                }

                ScanAndShowResults(filepath);
            }
        }

        private void Dialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ScanAndShowResults((sender as OpenFileDialog).FileName);
        }

        private void ScanAndShowResults(string path)
        {
            window.Title += $" [{new FileInfo(path).Name}]";
            window.taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            window.taskBarItemInfo.ProgressValue = 0;

            var list = new List<Scanner.Result>();

            new Task(() => {
                try
                {
                    var scanner = new Scanner();
                    scanner.Load("Patterns");

                    scanner.OnProgressChanged += (object sender, Scanner.ProgrssChangedEventArgsProgressEventArgs e) =>
                    {
                        Dispatcher.BeginInvoke((Action)delegate {
                            window.progress.Value = e.Value;

                            if (e.Value == 100)
                            {
                                window.taskBarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
                                Utils.FlashWindow.Flash(new WindowInteropHelper(window).Handle);
                            }
                            else
                            {
                                window.taskBarItemInfo.ProgressValue = e.Value / (double)100;
                            }
                        });
                    };

                    var results = scanner.ScanFile(path);

                    foreach (var result in results)
                        list.Add(result);

                    Dispatcher.BeginInvoke((Action)delegate {
                        window.results.ItemsSource = list;
                    });
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.ToString());
                    Environment.Exit(1);
                }
            }).Start();
        }
    }
}

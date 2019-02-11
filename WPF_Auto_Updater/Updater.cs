﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WPF_Auto_Update
{
    public static class Updater
    {
        // The absolute URI from where the latest EXE can be downloaded.
        public static string RemoteFileURI { get; set; }
        // The URI that will respond to an HTTP GET request with the latest assembly version (e.g. "1.3.0").  It must be parsable by Version.Parse().
        public static string ServiceURI { get; set; }
        // The length of time that a downloaded update will attempt to replace the original file before timing out.  Use Duration.Forever to specify no timeout and continue trying indefinitely.
        public static Duration UpdateTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public static string AppName
        {
            get
            {
                return Application.ResourceAssembly.ManifestModule.Assembly.GetName().Name;
            }
        }
        public static string FileName
        {
            get
            {
                return Application.ResourceAssembly.ManifestModule.Name;
            }
        }
        // Call this from the MainWindow constructor.
        public static void CheckCommandLineArgs()
        {
            if (ServiceURI == null || RemoteFileURI == null)
            {
                throw new Exception("AutoUpdater - RemoteFileURI and ServiceURI must be set.");
            }
           
            var args = Environment.GetCommandLineArgs().ToList();
            if (args.Contains("-wpfautoupdate") && args.Last() != "-wpfautoupdate" && File.Exists(args[args.IndexOf("-wpfautoupdate") + 1]) && Path.GetFileName(args[args.IndexOf("-wpfautoupdate") + 1]) == FileName)
            {
                var success = false;
                var startTime = DateTime.Now;
                while (DateTime.Now - startTime < UpdateTimeout && !success)
                {
                    try
                    {
                        File.Copy(Application.ResourceAssembly.ManifestModule.Assembly.Location, args[args.IndexOf("-wpfautoupdate") + 1], true);
                        success = true;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }

                if (success == false)
                {
                    MessageBox.Show("Update failed.  Please close all " + AppName + " windows, then try again.", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Update successful!  " + AppName + " will now restart.", "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Process.Start(args[args.IndexOf("-wpfautoupdate") + 1]);
                }
                Application.Current.Shutdown();
                return;
            }
        }
        // Call on MainWindow.Loaded event to check on start-up.
        public static async Task CheckForUpdates(bool Silent)
        {
            if (ServiceURI == null || RemoteFileURI == null)
            {
                throw new Exception("AutoUpdater - RemoteFileURI and ServiceURI must be set.");
            }
            try
            {
                WebClient webClient = new WebClient();
                HttpClient httpClient = new HttpClient();
                var result = await httpClient.GetAsync(ServiceURI);
                var strServerVersion = await result.Content.ReadAsStringAsync();
                var serverVersion = Version.Parse(strServerVersion);
                var thisVersion = Application.ResourceAssembly.ManifestModule.Assembly.GetName().Version;
                if (serverVersion > thisVersion)
                {
                    var strFilePath = Path.GetTempPath() + FileName;
                    var dialogResult = MessageBox.Show("A new version of " + AppName + " is available!  Would you like to download it now?", "New Version Available", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        if (File.Exists(strFilePath))
                        {
                            File.Delete(strFilePath);
                        }

                        var windowProgress = new Window();
                        windowProgress.DragMove();
                        windowProgress.Height = 150;
                        windowProgress.Width = 400;
                        windowProgress.WindowStyle = WindowStyle.None;
                        windowProgress.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        var progressControl = new DownloadProgressControl();
                        windowProgress.Content = progressControl;
                        webClient.DownloadProgressChanged += (sender, args) => {
                            progressControl.progressBar.Value = args.ProgressPercentage;
                        };
                        windowProgress.Show();
                        await webClient.DownloadFileTaskAsync(new Uri(RemoteFileURI), strFilePath);

                        windowProgress.Close();

                        var psi = new ProcessStartInfo()
                        {
                            FileName = strFilePath,
                            Arguments = $"-wpfautoupdate \"${Application.ResourceAssembly.ManifestModule.Assembly.Location}\"",
                            Verb = "RunAs"
                        };
                       
                        Process.Start(psi);
                        Application.Current.Shutdown();
                        return;
                    }
                }
                else
                {
                    if (!Silent)
                    {
                        MessageBox.Show(AppName + " is up-to-date.", "No Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch
            {
                if (!Silent)
                {
                    MessageBox.Show("Unable to contact the server.  Check your network connection or try again later.", "Server Unreachable", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                return;
            
            }
            
        }
    }
}

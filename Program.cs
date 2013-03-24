/* Copyright 2013 Corey Bonnell

   This file is part of Aufs4Win.

    Aufs4Win is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Aufs4Win is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Aufs4Win.  If not, see <http://www.gnu.org/licenses/>.
*/

using Dokan;
using System;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cbonnell.Aufs4Win
{
    class Program
    {
        private const string USAGE_STRING_FORMAT = "usage: {0} [XML config file path]";

        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception eObj = e.ExceptionObject as Exception;
                string errorMessage = "FATAL ERROR: " + eObj;
                Trace.TraceError(errorMessage);
                Console.Error.WriteLine(errorMessage);
                Environment.Exit(-1);
            };
            if (args.Length < 1)
            {
                string usageStr = Program.formatUsageString();
                Console.Error.WriteLine(usageStr);
                return -1;
            }
            ConfigurationManager configMan = new ConfigurationManager(args[0]);
            MergedVolumeInfo volumeInfo = configMan.ReadConfiguration();

            string assemblyGuid = Program.getAssemblyGuid();
            using (TraceListener etwListener = new EventProviderTraceListener(assemblyGuid))
            {
                Trace.Listeners.Add(etwListener);

                AufsImpl aufsImplObj = new AufsImpl(volumeInfo);
                DokanOptions options = new DokanOptions();
                options.DebugMode = false;
                options.UseStdErr = false;
                options.ThreadCount = 0; // use default thread count
                options.MountPoint = volumeInfo.DriveLetter + @":\";
                options.VolumeLabel = volumeInfo.DriveLabel;
                DokanNet.DokanMain(options, aufsImplObj);
            }
            DokanNet.DokanUnmount(volumeInfo.DriveLetter);
            return 0;
        }

        private static string getAssemblyGuid()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            object[] attrs = asm.GetCustomAttributes(typeof(GuidAttribute), false);
            return ((GuidAttribute)attrs[0]).Value;
        }

        private static string formatUsageString()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string exeName = Path.GetFileName(asm.Location);
            return String.Format(USAGE_STRING_FORMAT, exeName);
        }

    }
}
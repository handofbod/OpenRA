﻿#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using ICSharpCode.SharpZipLib.Zip;
using OpenRA.FileFormats;

namespace OpenRA.Utility
{
	static class Util
	{
		public static void ExtractFromPackage(string srcPath, string package, string[] files, string destPath)
		{
			if (!Directory.Exists(srcPath)) { Console.WriteLine("Error: Path {0} does not exist", srcPath); return; }
			if (!Directory.Exists(destPath)) { Console.WriteLine("Error: Path {0} does not exist", destPath); return; }

			FileSystem.Mount(srcPath);
			if (!FileSystem.Exists(package)) { Console.WriteLine("Error: Could not find {0}", package); return; }
			FileSystem.Mount(package);

			foreach (string s in files)
			{
                var destFile = P.E(destPath) / s;
				using (var sourceStream = FileSystem.Open(s))
				using (var destStream = destFile.Create())
				{
					Console.WriteLine("Status: Extracting {0}", s);
					destStream.Write(sourceStream.ReadAllBytes());
				}
			}
		}

        static IEnumerable<ZipEntry> GetEntries(this ZipInputStream z)
        {
            for (; ; )
            {
                var e = z.GetNextEntry();
                if (e != null) yield return e; else break;
            }
        }

        public static void ExtractZip(this ZipInputStream z, PathElement destPath, List<string> extracted)
		{
            foreach (var entry in z.GetEntries())
            {
                if (!entry.IsFile) continue;

                Console.WriteLine("Status: Extracting {0}", entry.Name);
                var destDir = (destPath / entry.Name).DirName();
                destDir.CreateDir();
                var destFilename = destPath / entry.Name;
                extracted.Add(destFilename.ToString());

                using (var f = destFilename.Create())
                {
                    int bufSize = 2048;
                    byte[] buf = new byte[bufSize];
                    while ((bufSize = z.Read(buf, 0, buf.Length)) > 0)
                        f.Write(buf, 0, bufSize);
                }
            }

			z.Close();
		}
		
		public static string GetPipeName()
		{
			return "OpenRA.Utility" + Guid.NewGuid().ToString();
		}	
		
		public static void CallWithAdmin(string command)
		{
			switch (Environment.OSVersion.Platform)
			{
			case PlatformID.Unix:
				// Unix platforms are expected to run the utility as root
				CallWithoutAdmin(command);
				break;
			default:
				CallWithAdminWindows(command);
				break;
			}
		}
				
		static void CallWithAdminWindows(string command)
		{			
			string pipename = Util.GetPipeName();
			var p = new Process();
			p.StartInfo.FileName = "OpenRA.Utility.exe";
			p.StartInfo.Arguments = command + " --pipe " + pipename;
			p.StartInfo.CreateNoWindow = true;

			// do we support elevation?
			if (Environment.OSVersion.Version >= new Version(6,0))
				p.StartInfo.Verb = "runas";

			try
			{
				p.Start();
			}
			catch (Win32Exception e)
			{
				if (e.NativeErrorCode == 1223) //ERROR_CANCELLED
					return;
				throw e;
			}
			
			var pipe = new NamedPipeClientStream(".", pipename, PipeDirection.In);
			pipe.Connect();

			using (var reader = new StreamReader(pipe))
			{
				while (!p.HasExited)
					Console.WriteLine(reader.ReadLine());
			}
		}
		
		static void CallWithoutAdmin(string command)
		{
			var p = new Process();
			p.StartInfo.FileName = "mono";
			p.StartInfo.Arguments = "OpenRA.Utility.exe " + command;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = true;
			p.Start();
			
			using (var reader = p.StandardOutput)
			{
				while(!p.HasExited)
					Console.WriteLine(reader.ReadLine());
			}
		}
	}
}

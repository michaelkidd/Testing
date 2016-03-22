namespace TaxExDeployBuilder
{
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Diagnostics;
	using System.IO;
	using System.IO.Compression;

	class Program
	{
		static void Main(string[] args)
		{
            //Code.Zip
            // .Site folders - DONE
            // .sql files
            // /Uploads/.rpt files
            // get files from d:\Projects (.Site for App, Whorder, Web, Portal), save to staging location
            // get sql files (if applicable), save to staging location
            // get report files from Taxex/3008, save to staging location /Uploads
            // get relaese note files from TaxEx/3008 and save to staging location
			string stagingPath = @"c:\temp\CodeDeploy";
			string projectDir = ConfigurationManager.AppSettings["ProjectsPath"];
			string codeDir = Path.Combine(projectDir, "CrystalSolutions", "Source", "TaxEx", "Code");
			string reportsPath = @"\\server-test-01.corp.crystalsolutioninc.com\TaxEx\3008\Uploads\TaxEx-Engine\Release\CustomReport";
			string releaseNotesPath = @"\\server-test-01.corp.crystalsolutioninc.com\TaxEx\3008\Uploads\TaxEx-Engine\Release\ReleaseNotes";
			List<string> directoriesToCopy = new List<string>() { "TaxEx.Ui.Web.Site", "TaxEx.Ui.Web.App.Site", "TaxEx.Ui.Web.WhOrder.Site", "TaxEx.Ui.Web.Portal.Site" };
			if (Directory.Exists(stagingPath))
			{
				Directory.Delete(stagingPath, true);
			}
			Directory.CreateDirectory(stagingPath);

            Console.Write("Version [x.xxx.x.x]: ");
            string versionNum = Console.ReadLine();
            Console.Write("Include Sql files [y/n]: ");
            char shouldIncludeSql = Console.ReadKey().KeyChar;
            Console.WriteLine("");
            Console.Write("Include Report files [y/n]: ");
            char shouldIncludeReports = Console.ReadKey().KeyChar;
            Console.WriteLine("");

            DateTime reportAsOfDate = reportAsOfDate = DateTime.Now.AddDays(-45);
            if (shouldIncludeReports == 'y')
            {
                Console.Write("Report as of Date [m/d/yy]: ");
                string reportAsOfDateInput = Console.ReadLine();
                Console.WriteLine("");

                if (!DateTime.TryParse(reportAsOfDateInput, out reportAsOfDate))
                    reportAsOfDate = DateTime.Now.AddDays(-45);
            }

            // Code copy
            if (Directory.Exists(codeDir))
			{
				foreach (string directory in directoriesToCopy)
				{
					Console.WriteLine("Copying " + directory + " to " + stagingPath);
					var process = new Process
					{
						StartInfo = new ProcessStartInfo
						{
							FileName = "xcopy",
							Arguments =
								Path.Combine(codeDir, directory) + @"\* " + Path.Combine(stagingPath, directory) +
								" /e /y /h /r /c /v /i /q",
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardError = true,
							RedirectStandardOutput = true
						}
					};

					process.Start();

					string stderrx = process.StandardError.ReadToEnd();
					process.WaitForExit();
					process.Dispose();

					if (!String.IsNullOrEmpty(stderrx))
					{
						Console.WriteLine(stderrx);
					}
				}
			}
			else
			{
				Console.WriteLine("Unable to locate code directory in " + projectDir);
			}

            // Uploads copy
            if (shouldIncludeReports == 'y')
            {
                Directory.CreateDirectory(Path.Combine(stagingPath, "Uploads"));
                Directory.CreateDirectory(Path.Combine(stagingPath, "Uploads", "Reports"));

                Console.WriteLine("Copying Report files newer than " + reportAsOfDate.ToShortDateString() + " to " + Path.Combine(stagingPath, "Uploads"));
                foreach (string file in Directory.GetFiles(reportsPath))
                {
                    if (File.GetLastWriteTime(file) > reportAsOfDate)
                    {
                        File.Copy(file, Path.Combine(stagingPath, "Uploads", "Reports", Path.GetFileName(file)));
                    }
                }
            }

			// Release Notes copy
			Directory.CreateDirectory(Path.Combine(stagingPath, "Uploads", "ReleaseNotes"));

			Console.WriteLine("Copying Release Notes to " + Path.Combine(stagingPath, "ReleaseNotes"));
			foreach (string file in Directory.GetFiles(releaseNotesPath))
			{
				if (File.GetLastWriteTime(file) > reportAsOfDate)
				{
					File.Copy(file, Path.Combine(stagingPath, "Uploads", "ReleaseNotes", Path.GetFileName(file)));
				}
			}

			// Sql copy
			if (shouldIncludeSql == 'y')
			{
				Console.WriteLine("Copying Sql Sync files.");
				File.Copy(Path.Combine(@"\\server-test-01.corp.crystalsolutioninc.com\Releases\DatabaseSyncFiles", "PreSyncUpdate.sql"), Path.Combine(stagingPath, "PreSyncUpdate.sql"));
				File.Copy(@"\\server-test-01.corp.crystalsolutioninc.com\Releases\DatabaseSyncFiles\Sync.sql", Path.Combine(stagingPath, "Sync.sql"));
				File.Copy(Path.Combine(@"\\server-test-01.corp.crystalsolutioninc.com\Releases\DatabaseSyncFiles", "PostSyncUpdate.sql"), Path.Combine(stagingPath, "PostSyncUpdate.sql"));                
			}
			else
			{
				Console.WriteLine("Skipping Sql Sync files.");
			}

            // zip creation
            String finalFilename = "TaxEx[" + versionNum.Replace('.', '-') + "].zip";
            Console.WriteLine("Creating " + finalFilename + " file");
			ZipFile.CreateFromDirectory(stagingPath, Path.Combine(@"C:\temp", "Code.zip"));

            // copy to Google Drive
            Console.WriteLine(@"Copying " + finalFilename + @" to Google Drive location (\\server-test-01.corp.crystalsolutioninc.com\TaxEx-AllServers-Sync\Code)");
            File.Copy(Path.Combine(@"C:\temp", "Code.zip"), @"\\server-test-01.corp.crystalsolutioninc.com\TaxEx-AllServers-Sync\Code\ " + finalFilename, true);

            // remove files from staging area
            Console.WriteLine("Cleaning up temp and staging files...");
			File.Delete(Path.Combine(@"C:\temp", "Code.zip"));
			Directory.Delete(stagingPath, true);

			Console.WriteLine("Program Completed.");
			Console.ReadKey();
		}
	}
}
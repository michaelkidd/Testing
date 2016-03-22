namespace ClientDeploy
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Mail;
    using System.Text;
    using NLog;
    using TaxEx.Apps.Internal.WebAdmin.Site.CodeLibrary.Websites;
    using System.Collections.Generic;

    class Program
	{
		static void Main(string[] args)
		{
			String siteString = ConfigurationManager.AppSettings["Websites"];
			string[] serverSites = null;
			
			Logger logger = LogManager.GetCurrentClassLogger();
			String message = String.Empty;
			String serverName = ConfigurationManager.AppSettings["ServerName"];
			String newCodePath = ConfigurationManager.AppSettings["CodeToBeAppliedPath"];
			String newCodeFile = Path.Combine(newCodePath, "Code.zip");
			String stagingPath = ConfigurationManager.AppSettings["StagingPath"];
			String websiteCodePath = ConfigurationManager.AppSettings["WebSiteFolderPath"];
			String archivePath = ConfigurationManager.AppSettings["WebSiteArchivePath"];
			String newCodeZipFile = Path.Combine(stagingPath, "Code.zip");
			bool unzipCompleted = true;
			StringBuilder sb = new StringBuilder();
			
			logger.Info("----------- Starting Deploy Program ----------");
			if (!String.IsNullOrEmpty(siteString))
			{
				serverSites = siteString.Split(',');
			}
			else
			{
				logger.Warn("No sites found in CodeDeploy.config, skipping deploy.");
			}

			if (serverSites != null)
			{
				foreach (string site in serverSites)
				{
					CleanupLogFiles(websiteCodePath, site, logger);
				}

                Dictionary<string, bool> siteSetup = new Dictionary<string, bool>();
                try
                {
                    if (File.Exists(Path.Combine(newCodePath, "DeployConfig.txt")))
                    {
                        String line;
                        System.IO.StreamReader file =
                           new System.IO.StreamReader(Path.Combine(newCodePath, "DeployConfig.txt"));

                        while ((line = file.ReadLine()) != null && !String.IsNullOrEmpty(line))
                        {
                            string[] info = line.Split('\t');
                            siteSetup.Add(info[0], Boolean.Parse(info[1]));
                        }

                        file.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Messed up the file: " + ex.ToString());
                    throw;
                }

                logger.Info("Checking for new code at: " + newCodeFile);
				if (File.Exists(newCodeFile) && Directory.Exists(stagingPath))
				{
					sb.AppendLine("Results of Deploy program, which was run at " + DateTime.Now.ToString() + "<br><br>");

					try
					{
						foreach (string directory in System.IO.Directory.GetDirectories(stagingPath))
						{
							Directory.Delete(directory, true);
						}
						foreach (String file in System.IO.Directory.GetFiles(stagingPath))
						{
							File.Delete(file);
						}
					}
					catch (Exception ex)
					{
						logger.Error(ex.ToString());
					}

					File.Copy(Path.Combine(newCodePath, "Code.zip"), newCodeZipFile);
					logger.Info("New code found, unzipping to: " + stagingPath);

					try
					{
						ZipFile.ExtractToDirectory(newCodeZipFile, stagingPath);
						File.Delete(newCodeZipFile);
					}
					catch (Exception ex)
					{
						sb.AppendLine("Unzip failed:" + "<br>");
						sb.AppendLine(ex.ToString() + "<br>");
						logger.Error(ex.ToString());
						unzipCompleted = false;
					}

					if (unzipCompleted)
					{
						foreach (string site in ConfigurationManager.AppSettings["Websites"].Split(','))
						{
							if (siteSetup.ContainsKey(site) && siteSetup[site] == true)
							{
								sb.AppendLine("<b>Deploying to: " + site + "</b><br>");
								logger.Info("Stopping site " + site + " in IIS...");

								WebsiteHelper.StopWebsite(serverName, site, out message);
								logger.Info(message);

								logger.Info("Backing up site " + Path.Combine(websiteCodePath, site) + "\\ into " + archivePath + "\\");

								// if more than 5 files in the Archive dir, delete oldest
								if (Directory.GetFiles(Path.Combine(archivePath, site)).Count() > 4)
								{
									logger.Info("Removing oldest backup from " + Path.Combine(archivePath, site));
									File.Delete(Directory.GetFiles(Path.Combine(archivePath, site)).OrderBy(x => x).First());
								}

								try
								{
									String currentSitePath = Path.Combine(websiteCodePath, site);
									if (!Directory.Exists(Path.Combine(archivePath, site)))
									{
										Directory.CreateDirectory(Path.Combine(archivePath, site));
									}

									ZipFile.CreateFromDirectory
										(
											currentSitePath,
											Path.Combine(archivePath, site) + @"\" +
											String.Format("{0}-{1}", site, DateTime.Now.ToString("yyyyMMdd-Hmm")) +
											".zip",
											CompressionLevel.Fastest,
											true
										);
								}
								catch (Exception ex)
								{
									logger.Error(ex.ToString());
									sb.AppendLine("Archiving site failed:" + "<br>");
									sb.AppendLine(ex.ToString() + "<br>");
								}

								string dbId = ConfigurationManager.AppSettings[site + "-DbId"];
								String taxexPath = Path.Combine(ConfigurationManager.AppSettings["TaxExPath"], dbId);

								logger.Info("Copying code to: " + Path.Combine(websiteCodePath, site));

								var process = new Process
								{
									StartInfo = new ProcessStartInfo
									{
										FileName = "xcopy",
										Arguments =
											stagingPath + @"\* " + Path.Combine(websiteCodePath, site) +
											" /e /y /h /r /c /v /i /q /exclude:" + stagingPath.Substring(0, stagingPath.IndexOf("\\") + 1) +
											"\\TaxEx-CodeDeploy\\excludeFiles.txt",
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
									logger.Error(stderrx);

									sb.AppendLine("Copy site failed:" + "<br>");
									sb.AppendLine(stderrx.ToString() + "<br>");
								}

								string reportPath = Path.Combine(taxexPath, "Uploads", "TaxEx-Engine", "Release", "CustomReport");
								if (Directory.Exists(taxexPath) && Directory.Exists(Path.Combine(stagingPath, "Uploads", "Reports")))
								{
									logger.Info("Copying Report files to " + reportPath + "...");

									var process2 = new Process
									{
										StartInfo = new ProcessStartInfo
										{
											FileName = "xcopy",
											Arguments = Path.Combine(stagingPath, "Uploads", "Reports")
											            + @"\* " + reportPath + @" /e /y /h /r /c /v /i /q",
											UseShellExecute = false,
											CreateNoWindow = true,
											RedirectStandardError = true,
											RedirectStandardOutput = true
										}
									};

									process2.Start();

									string stderrx2 = process2.StandardError.ReadToEnd();
									process2.WaitForExit();
									process2.Dispose();

									if (!String.IsNullOrEmpty(stderrx2))
									{
										logger.Info(stderrx2);

										sb.AppendLine("Copy Report files failed:" + "<br>");
										sb.AppendLine(stderrx2.ToString() + "<br>");
									}
								}

								string releaseNotes = Path.Combine(taxexPath, "Uploads", "TaxEx-Engine", "Release", "ReleaseNotes");
								if (Directory.Exists(taxexPath) && Directory.Exists(Path.Combine(stagingPath, "Uploads", "ReleaseNotes")))
								{
									logger.Info("Copying Release Notes files to " + releaseNotes + "...");

									var process3 = new Process
									{
										StartInfo = new ProcessStartInfo
										{
											FileName = "xcopy",
											Arguments = Path.Combine(stagingPath, "Uploads", "ReleaseNotes")
														+ @"\* " + releaseNotes + @" /e /y /h /r /c /v /i /q",
											UseShellExecute = false,
											CreateNoWindow = true,
											RedirectStandardError = true,
											RedirectStandardOutput = true
										}
									};

									process3.Start();

									string stderrx3 = process3.StandardError.ReadToEnd();
									process3.WaitForExit();
									process3.Dispose();

									if (!String.IsNullOrEmpty(stderrx3))
									{
										logger.Info(stderrx3);

										sb.AppendLine("Copy Release Notes files failed:" + "<br>");
										sb.AppendLine(stderrx3.ToString() + "<br>");
									}
								}

								// Check for Sql files and run them against the database
								if (File.Exists(Path.Combine(stagingPath, "PreSyncUpdate.sql"))
								    || File.Exists(Path.Combine(stagingPath, "Sync.sql"))
								    || File.Exists(Path.Combine(stagingPath, "PostSyncUpdate.sql")))
								{
									logger.Info("Running sql sync script(s) on " + site + " database...");

									var process3 = new Process
									{
										StartInfo = new ProcessStartInfo
										{
											FileName = stagingPath.Substring(0, stagingPath.IndexOf("\\") + 1) + @"\TaxEx-CodeDeploy\SyncDatabase.bat",
											Arguments = ConfigurationManager.AppSettings["SqlServerName"] + " " + site,
											UseShellExecute = false,
											CreateNoWindow = true,
											RedirectStandardError = true,
											RedirectStandardOutput = true
										}
									};

									process3.Start();
									string stderrx3 = process3.StandardError.ReadToEnd();
									process3.WaitForExit();
									process3.Dispose();

									if (!String.IsNullOrEmpty(stderrx3))
									{
										if (stderrx3.Contains("Invalid filename."))
											logger.Warn(stderrx3);
										else
										{
											logger.Error(stderrx3);

											sb.AppendLine("Running Sync failed:" + "<br>");
											sb.AppendLine(stderrx3.ToString() + "<br>");
										}
									}
								}

								logger.Info("Cleaning up app pools and restarting for " + site + "...");
								WebsiteHelper.RecycleApplicationPoolsByWebsite(serverName, site, out message);
								logger.Info(message);
								WebsiteHelper.StartWebsite(serverName, site, out message);
								logger.Info(message);

								sb.AppendLine("<b>Deploy to " + site + " finished." + "</b><br><br>");
							}
							else
							{
								logger.Info(site + " is not configured to be deployed to.");

								sb.AppendLine(site + " is not configured to be deployed to." + "<br><br>");
							}
						}
						
						logger.Info("Cleaning up files in staging directory: " + stagingPath);

						try
						{
							foreach (string directory in System.IO.Directory.GetDirectories(stagingPath))
							{
								Directory.Delete(directory, true);
							}
							foreach (String file in System.IO.Directory.GetFiles(stagingPath))
							{
								File.Delete(file);
							}
						}
						catch (Exception ex)
						{
							logger.Error(ex.ToString());
							sb.AppendLine("Cleaning up staging directory failed:" + "<br>");
							sb.AppendLine(ex.ToString() + "<br>");
						}

						// clean up copy of sql files and Uploads that xcopy copied over
						foreach (string file in Directory.GetFiles(stagingPath))
						{
							File.Delete(file);
						}

						if (Directory.Exists(stagingPath))
						{
							foreach (string directory in Directory.GetDirectories(stagingPath))
							{
								Directory.Delete(directory, true);
							}
						}
					}
				}
				else
				{
					logger.Info("No new code found in " + newCodePath + " to deploy.");
				}
			}

			try
			{
				if (!String.IsNullOrEmpty(sb.ToString()))
				{
					using (SmtpClient client = new SmtpClient())
					{
						using (MailMessage mail = new MailMessage())
						{
							mail.IsBodyHtml = true;
							mail.From = new MailAddress("support@crystalsolutioninc.com");
							mail.To.Add("michael.kidd@crystalsolutioninc.com, yasmine.rodriguez@crystalsolutioninc.com");
							mail.Subject = siteString + " Deploy Status";
							mail.Body = sb.ToString();

							client.Send(mail);
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.Info("Unable to send status email:");
				logger.Info(ex.ToString());
			}

			logger.Info("***** Deploy Program Complete! *****");
		}

		private static void CleanupLogFiles(string websitePath, string site, Logger logger)
		{
			String[] siteFolders = { "TaxEx.Ui.Web.App.Site", "TaxEx.Ui.Web.WhOrder.Site", "TaxEx.Ui.Web.Portal.Site" };
			logger.Info("Cleaning up log files for " + site);

			foreach (string folder in siteFolders)
			{
				if (Directory.Exists(Path.Combine(websitePath, site, folder, "logs")))
				{
					foreach (string file in Directory.GetFiles(Path.Combine(websitePath, site, "TaxEx.Ui.Web.App.Site", "logs")))
					{
						if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-14))
						{
							File.Delete(file);
						}
					}
				}
			}
		}
	}
}

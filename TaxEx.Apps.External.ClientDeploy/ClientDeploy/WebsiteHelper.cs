using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaxEx.Apps.Internal.WebAdmin.Site.CodeLibrary.Websites
{
	using System.Diagnostics;

	public class WebsiteHelper
    {
        public static void RecycleApplicationPoolsByWebsite(string serverName, string websiteName, out string message)
        {
            message = String.Empty;
            string itemMessage = String.Empty;
            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
                ApplicationCollection appCollection = site.Applications;
                List<string> appPools = new List<string>();
                foreach(Application application in appCollection)
                {
                    string appPoolName = application.ApplicationPoolName;

                    if (appPools.IndexOf(appPoolName) > -1)
                        continue;

                    appPools.Add(appPoolName);
                    ApplicationPool appPool = manager.ApplicationPools.FirstOrDefault(ap => ap.Name == appPoolName);
                    RecycleApplicationPool(appPool, out itemMessage);
                    message += itemMessage;
                }
            }
        }

        public static void RecycleApplicationPool(ApplicationPool appPool, out string message)
        {
            //Get the current state of the app pool
            bool appPoolRunning = appPool.State == ObjectState.Started || appPool.State == ObjectState.Starting;
            bool appPoolStopped = appPool.State == ObjectState.Stopped || appPool.State == ObjectState.Stopping;

            //The app pool is running, so stop it first.
            if (appPoolRunning)
            {
                //Wait for the app to finish before trying to stop
                while (appPool.State == ObjectState.Starting) { System.Threading.Thread.Sleep(1000); }

                //Stop the app if it isn't already stopped
                if (appPool.State != ObjectState.Stopped)
                {
                    appPool.Stop();
                }
                appPoolStopped = true;
            }

            //Only try restart the app pool if it was running in the first place, because there may be a reason it was not started.
            if (appPoolStopped && appPoolRunning)
            {
                //Wait for the app to finish before trying to start
                while (appPool.State == ObjectState.Stopping) { System.Threading.Thread.Sleep(1000); }

                //Start the app
                appPool.Start();
            }

            message = String.Format("Application Pool: {0} recycled.", appPool.Name);
        }

        public static void StopApplicationPool(ApplicationPool appPool, out string message)
        {
            //Get the current state of the app pool
            bool appPoolRunning = appPool.State == ObjectState.Started || appPool.State == ObjectState.Starting;
            bool appPoolStopped = appPool.State == ObjectState.Stopped || appPool.State == ObjectState.Stopping;

            //The app pool is running, so stop it first.
            if (appPoolRunning)
            {
                //Wait for the app to finish before trying to stop
                while (appPool.State == ObjectState.Starting) { System.Threading.Thread.Sleep(1000); }

                //Stop the app if it isn't already stopped
                if (appPool.State != ObjectState.Stopped)
                {
                    appPool.Stop();
                }
                appPoolStopped = true;
            }

            message = String.Format("Application Pool: {0} stopped.", appPool.Name);
        }

		public static void StartApplicationPool(ApplicationPool appPool, out string message)
		{
			//Get the current state of the app pool
			bool appPoolRunning = appPool.State == ObjectState.Started || appPool.State == ObjectState.Starting;
			bool appPoolStopped = appPool.State == ObjectState.Stopped || appPool.State == ObjectState.Stopping;

			//The app pool is running, so stop it first.
			if (!appPoolRunning)
			{
				//Wait for the app to finish before trying to stop
				while (appPool.State == ObjectState.Starting) { System.Threading.Thread.Sleep(1000); }

				//Stop the app if it isn't already stopped
				if (appPool.State != ObjectState.Started)
				{
					appPool.Start();
				}
				appPoolRunning = false;
			}

			message = String.Format("Application Pool: {0} started.", appPool.Name);
		}
		
        public static void StopWebsite(string serverName, string websiteName, out string message)
        {
            message = String.Empty;

            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
                site.Stop();
				while (site.State != ObjectState.Stopped) { }
            }

            message = String.Format("Website: {0} stopped.", websiteName);
        }

        public static void StartWebsite(string serverName, string websiteName, out string message)
        {
            message = String.Empty;

            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
				site.Start();
				while (site.State != ObjectState.Started) { }
            }

            message = String.Format("Website: {0} started.", websiteName);
        }

		public static void StartApplicationPools(string serverName, string websiteName, out string message)
		{
			message = String.Empty;
			string itemMessage = String.Empty;
			using (ServerManager manager = ServerManager.OpenRemote(serverName))
			{
				Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
				ApplicationCollection appCollection = site.Applications;
				List<string> appPools = new List<string>();
				foreach (Application application in appCollection)
				{
					string appPoolName = application.ApplicationPoolName;

					if (appPools.IndexOf(appPoolName) > -1)
						continue;

					appPools.Add(appPoolName);
					ApplicationPool appPool = manager.ApplicationPools.FirstOrDefault(ap => ap.Name == appPoolName);
					StartApplicationPool(appPool, out itemMessage);
					message += itemMessage;
				}
			}
		}

		public static void DeployCode(string serverName, string websiteName, out string message)
		{
			string output = String.Empty;
			try
			{
				WebsiteHelper.StopWebsite(serverName, websiteName, out output);
				var process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = @"\\server-test-01\Releases\DeployScripts\SyncWithBuild-ContinuousIntegration\" + websiteName + "-FullDeploy-WithLogging.bat"
					}
				};
				process.Start();
				process.WaitForExit();
				WebsiteHelper.StartWebsite(serverName, websiteName, out output);
				message = "Deployment to " + websiteName + " completed.";
			}
			catch (Exception ex)
			{
				message = ex.ToString();
			}

			message += output;
		}

        public static void StopApplicationPools(string serverName, string websiteName, out string message)
        {
            message = String.Empty;
            string itemMessage = String.Empty;
            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
                ApplicationCollection appCollection = site.Applications;
                List<string> appPools = new List<string>();
                foreach (Application application in appCollection)
                {
                    string appPoolName = application.ApplicationPoolName;

                    if (appPools.IndexOf(appPoolName) > -1)
                        continue;

                    appPools.Add(appPoolName);
                    ApplicationPool appPool = manager.ApplicationPools.FirstOrDefault(ap => ap.Name == appPoolName);
                    StopApplicationPool(appPool, out itemMessage);
                    message += itemMessage;
                }
            }
        }

        public static void CheckWebsiteHealth(string serverName, string websiteName, out string message)
        {
            message = String.Empty;
            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
                if (site == null)
                    throw new Exception("website lookup failed.");

				message += String.Format("Website {0} Is Running: {1}", site.Name, (site.State == ObjectState.Started).ToString());
            }
        }

        public static void RecycleWebsite(string serverName, string websiteName, out string message)
        {
            message = String.Empty;
        
            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
                if (site == null)
                    throw new Exception("website lookup failed.");

                RecycleWebsite(site, out message);
            }
        }

        public static void RecycleWebsite(Microsoft.Web.Administration.Site site, out string message)
        {
            //Get the current state of the app pool
            bool siteRunning = site.State == ObjectState.Started || site.State == ObjectState.Starting;
            bool siteStopped = site.State == ObjectState.Stopped || site.State == ObjectState.Stopping;

            //The app pool is running, so stop it first.
            if (siteRunning)
            {
                //Wait for the app to finish before trying to stop
                while (site.State == ObjectState.Starting) { System.Threading.Thread.Sleep(1000); }

                //Stop the app if it isn't already stopped
                if (site.State != ObjectState.Stopped)
                {
                    site.Stop();
                }
                siteStopped = true;
            }

            //Only try restart the app pool if it was running in the first place, because there may be a reason it was not started.
            if (siteStopped && siteRunning)
            {
                //Wait for the app to finish before trying to start
                while (site.State == ObjectState.Stopping) { System.Threading.Thread.Sleep(1000); }

                //Start the app
                site.Start();
            }

            message = String.Format("Website: {0} restarted.", site.Name);
        }


        public static void RecycleApplicationPools(ApplicationPoolCollection appPoolCollection, out string message)
        {
            message = String.Empty;
            if (appPoolCollection == null)
            {
                throw new ArgumentException("appPoolCollection");
            }

            foreach (ApplicationPool appPool in appPoolCollection)
            {
                RecycleApplicationPool(appPool, out message);
            }
        }

        public static void RecycleApplicationPools(string serverName, out string message)
        {
            message = String.Empty;

            if (string.IsNullOrEmpty(serverName))
            {
                throw new ArgumentException("invalid arguments");
            }

            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                foreach (ApplicationPool appPool in manager.ApplicationPools)
                {
                    RecycleApplicationPool(appPool, out message);
                }
            }
        }

        public static string GetHostName(string serverName, string websiteName)
        {
            string hostName = String.Empty;

            using (ServerManager manager = ServerManager.OpenRemote(serverName))
            {
                Microsoft.Web.Administration.Site site = manager.Sites.FirstOrDefault(ap => ap.Name == websiteName);
                hostName = site.Bindings[0].Host;
            }

            return hostName;
        }

        public static void ListWebsites(string serverName)
        {
            using (ServerManager serverManager = ServerManager.OpenRemote(serverName))
            {
                foreach (Microsoft.Web.Administration.Site site in serverManager.Sites)
                {
                    Console.WriteLine("Site: " + site.Name);
					Microsoft.Web.Administration.Configuration webConfig = serverManager.GetWebConfiguration("Default Web Site");
                    //ConfigurationSection section = webConfig.GetSection("system.webServer/defaultDocument");
                    //foreach (ConfigurationElement item in section.GetCollection("files"))
                    //{
                    //    Console.WriteLine(item["value"]);
                    //}
                }

                GetWebsiteInformation(serverManager);
            }
        }

        public static void GetWebsiteInformation(ServerManager serverManager)
        {
            SiteCollection sites = serverManager.Sites;
            foreach (Microsoft.Web.Administration.Site site in sites)
            {
                ApplicationDefaults defaults = site.ApplicationDefaults;

                //get the name of the ApplicationPool under which the Site runs
                string appPoolName = defaults.ApplicationPoolName;

                Console.WriteLine("Website Name: " + site.Name);
                Console.WriteLine("Website Id: " + site.Id.ToString());
                Console.WriteLine("Autostart on: " + site.ServerAutoStart.ToString());
                Console.WriteLine("Main Application Pool: " + appPoolName);

                ConfigurationAttributeCollection attributes = defaults.Attributes;
                foreach (ConfigurationAttribute configAttribute in attributes)
                {
                    //put code here to work with each ConfigurationAttribute
                    Console.WriteLine("Configuration Attribute Name: " + configAttribute.Name);
                }

                ConfigurationAttributeCollection attributesCollection = site.Attributes;
                foreach (ConfigurationAttribute attribute in attributesCollection)
                {
                    //put code here to work with each ConfigurationAttribute
                }

                //Get the Binding objects for this Site
                BindingCollection bindings = site.Bindings;
                foreach (Microsoft.Web.Administration.Binding binding in bindings)
                {
                    //put code here to work with each Binding
                    Console.WriteLine("Binding Host: " + binding.Host);
                }

                //retrieve the State of the Site
                ObjectState siteState = site.State;

                //Get the list of all Applications for this Site
                ApplicationCollection applications = site.Applications;
                foreach (Microsoft.Web.Administration.Application application in applications)
                {
                    //put code here to work with each Application
                    Console.WriteLine("Application AppPoolName: " + application.ApplicationPoolName);
                    Console.WriteLine("Application Path: " + application.Path);
                    Console.WriteLine("Application IsLocallyStored: " + application.IsLocallyStored.ToString());


                    VirtualDirectoryCollection directories = application.VirtualDirectories;
                    foreach (VirtualDirectory directory in directories)
                    {
                        ConfigurationAttributeCollection attribues = directory.Attributes;
                        foreach (ConfigurationAttribute attribute in attributes)
                        {
                            //put code here to work with each attribute
                        }

                        ConfigurationChildElementCollection childElements = directory.ChildElements;
                        foreach (ConfigurationElement element in childElements)
                        {
                            //put code here to work with each ConfigurationElement
                        }

                        //get the directory.Path
                        string path = directory.Path;

                        //get the physical path
                        string physicalPath = directory.PhysicalPath;
                    }
                }
            }
        }

        public void GetApplicationPools(ServerManager serverManager)
        {
            ApplicationPoolCollection applicationPools = serverManager.ApplicationPools;
            foreach (ApplicationPool pool in applicationPools)
            {
                //get the AutoStart boolean value
                bool autoStart = pool.AutoStart;

                //get the name of the ManagedRuntimeVersion
                string runtime = pool.ManagedRuntimeVersion;

                //get the name of the ApplicationPool
                string appPoolName = pool.Name;

                //get the identity type
                ProcessModelIdentityType identityType = pool.ProcessModel.IdentityType;

                //get the username for the identity under which the pool runs
                string userName = pool.ProcessModel.UserName;

                //get the password for the identity under which the pool runs
                string password = pool.ProcessModel.Password;
            }
        }

        public static void CreateApplicationPool(ServerManager serverManager)
        {
            ApplicationPool myApplicationPool = null;

            //we will create a new ApplicationPool named 'MyApplicationPool'
            //we will first check to make sure that this pool does not already exist
            //since the ApplicationPools property is a collection, we can use the Linq FirstOrDefault method
            //to check for its existence by name
            if (serverManager.ApplicationPools != null && serverManager.ApplicationPools.Count > 0)
            {
                if (serverManager.ApplicationPools.FirstOrDefault(p => p.Name == "MyApplicationPool") == null)
                {
                    //if we find the pool already there, we will get a referecne to it for update
                    myApplicationPool = serverManager.ApplicationPools.FirstOrDefault(p => p.Name == "MyApplicationPool");
                }
                else
                {
                    //if the pool is not already there we will create it
                    myApplicationPool = serverManager.ApplicationPools.Add("MyApplicationPool");
                }
            }
            else
            {
                //if the pool is not already there we will create it
                myApplicationPool = serverManager.ApplicationPools.Add("MyApplicationPool");
            }

            if (myApplicationPool != null)
            {
                //for this sample, we will set the pool to run under the NetworkService identity
                myApplicationPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;

                //we set the runtime version
                myApplicationPool.ManagedRuntimeVersion = "v4.0";

                //we save our new ApplicationPool!
                serverManager.CommitChanges();
            }
        }

        public static void SetApplicationPoolIndentity(ServerManager serverManager, ApplicationPool appPool, string userName, string password)
        {
            if (appPool != null)
            {
                //for this sample, we will set the pool to run under the identity of a specific user
                appPool.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
                appPool.ProcessModel.UserName = userName;
                appPool.ProcessModel.Password = password;

                //we set the runtime version
                appPool.ManagedRuntimeVersion = "v4.0";

                //we save our new ApplicationPool!
                serverManager.CommitChanges();
            }
        }

        public static void CreateWebsite(ServerManager serverManager, ApplicationPool appPool)
        {
            if (serverManager.Sites != null && serverManager.Sites.Count > 0)
            {
                //we will first check to make sure that the site isn't already there
                if (serverManager.Sites.FirstOrDefault(s => s.Name == "MySite") == null)
                {
                    //we will just pick an arbitrary location for the site
                    string path = @"c:\MySiteFolder\";

                    //we must specify the Binding information
                    string ip = "*";
                    string port = "80";
                    string hostName = "*";

                    string bindingInfo = string.Format(@"{0}:{1}:{2}", ip, port, hostName);

                    //add the new Site to the Sites collection
                    Microsoft.Web.Administration.Site site = serverManager.Sites.Add("MySite", "http", bindingInfo, path);

                    //set the ApplicationPool for the new Site
                    site.ApplicationDefaults.ApplicationPoolName = appPool.Name;

                    //save the new Site!
                    serverManager.CommitChanges();
                }
            }
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceProcess;
using TGServiceInterface;
using TGServiceInterface.Components;

namespace TGServerService
{
	/// <summary>
	/// The windows service the application runs as
	/// </summary>
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
	sealed class Service : ServiceBase, ITGSService, ITGConnectivity
	{
		/// <summary>
		/// The logging ID used for <see cref="Service"/> events
		/// </summary>
		public const byte LoggingID = 0;

		/// <summary>
		/// The service version <see cref="string"/> based on the <see cref="FileVersionInfo"/>
		/// </summary>
		public static readonly string VersionString = "/tg/station 13 Server Service v" + FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

		/// <summary>
		/// Singleton instance
		/// </summary>
		static Service ActiveService;

		/// <summary>
		/// Cancels WCF's user impersonation to allow clean access to writing log files
		/// </summary>
		public static void CancelImpersonation()
		{
			WindowsIdentity.Impersonate(IntPtr.Zero);
		}

		/// <summary>
		/// Writes an event to Windows the event log
		/// </summary>
		/// <param name="message">The log message</param>
		/// <param name="id">The <see cref="EventID"/> of the message</param>
		/// <param name="eventType">The <see cref="EventLogEntryType"/> of the event</param>
		/// <param name="loggingID">The logging source ID for the event</param>
		public static void WriteEntry(string message, EventID id, EventLogEntryType eventType, byte loggingID)
		{
			ActiveService.EventLog.WriteEntry(message, eventType, (int)id + loggingID);
		}

		/// <summary>
		/// Constructs and runs a <see cref="Service"/>. Do not add any code that might need debugging here as it's near impossible to get Windows to debug it properly without timing out
		/// </summary>
		public static void Launch()
		{
			if (ActiveService != null)
				throw new Exception("There is already a Service instance running!");
			ActiveService = new Service();
			Run(ActiveService);
		}

		/// <summary>
		/// Sets up the service name. Do not add any more code due to the reasons outlined in <see cref="Launch"/>
		/// </summary>
		Service()
		{
			ServiceName = "TG Station Server";
		}

		/// <summary>
		/// The WCF host that contains <see cref="ITGSService"/> connects to
		/// </summary>
		ServiceHost serviceHost;
		/// <summary>
		/// Map of <see cref="InstanceConfig.Name"/> to the respective <see cref=""/>
		/// </summary>
		IDictionary<string, ServiceHost> hosts;
		/// <summary>
		/// List of <see cref="ServerInstance.LoggingID"/>s in use
		/// </summary>
		IList<int> UsedLoggingIDs = new List<int>();

		/// <summary>
		/// Migrates the .NET config from <paramref name="oldVersion"/> to <paramref name="oldVersion"/> + 1
		/// </summary>
		/// <param name="oldVersion">The version to migrate from</param>
		void MigrateSettings(int oldVersion)
		{
			var Config = Properties.Settings.Default;
			switch (oldVersion)
			{
				case 6: //switch to per-instance configs
					var IC = DeprecatedInstanceConfig.CreateFromNETSettings();
					IC.Save();
					Config.InstancePaths.Add(IC.InstanceDirectory);
					break;
			}
		}

		/// <summary>
		/// Overrides and saves the configured <see cref="Properties.Settings.RemoteAccessPort"/> if requested by command line parameters
		/// </summary>
		/// <param name="args">The command line parameters for the <see cref="Service"/></param>
		void ChangePortFromCommandLine(string[] args)
		{
			var Config = Properties.Settings.Default;

			for (var I = 0; I < args.Length - 1; ++I)
				if (args[I].ToLower() == "-port")
				{
					try
					{
						var res = Convert.ToUInt16(args[I + 1]);
						if (res == 0)
							throw new Exception("Cannot bind to port 0");
						Config.RemoteAccessPort = res;
					}
					catch (Exception e)
					{
						throw new Exception("Invalid argument for \"-port\"", e);
					}
					Config.Save();
					break;
				}
		}

		/// <summary>
		/// Called by the Windows service manager. Initializes and starts configured <see cref="ServerInstance"/>s
		/// </summary>
		/// <param name="args">Command line arguments for the <see cref="Service"/></param>
		protected override void OnStart(string[] args)
		{
			Environment.CurrentDirectory = Directory.CreateDirectory(Path.GetTempPath() + "/TGStationServerService").FullName;  //MOVE THIS POINTER BECAUSE ONE TIME I ALMOST ACCIDENTALLY NUKED MYSELF BY REFACTORING! http://imgur.com/zvGEpJD.png

			SetupConfig();

			ChangePortFromCommandLine(args);

			SetupService();

			SetupInstances();

			OnlineAllHosts();
		}

		/// <summary>
		/// Writes some changes to the <see cref="Properties.Settings"/> that always need to be done.
		/// </summary>
		void PrePrepConfig()
		{
			var Config = Properties.Settings.Default;

			if (Config.InstancePaths == null)
				Config.InstancePaths = new StringCollection();
		}

		/// <summary>
		/// Upgrades up the service configuration
		/// </summary>
		void SetupConfig()
		{
			var Config = Properties.Settings.Default;
			if (Config.UpgradeRequired)
			{
				var newVersion = Config.SettingsVersion;
				Config.Upgrade();

				PrePrepConfig();
				
				for (var oldVersion = Config.SettingsVersion; oldVersion < newVersion; ++oldVersion)
					MigrateSettings(oldVersion);

				Config.SettingsVersion = newVersion;

				Config.UpgradeRequired = false;
				Config.Save();
			}
		}

		/// <summary>
		/// Creates the <see cref="ServiceHost"/> for <see cref="ITGSService"/>
		/// </summary>
		void SetupService()
		{
			serviceHost = CreateHost(this, Interface.ServiceInterfaceName);
			AddEndpoint(serviceHost, typeof(ITGSService));
			AddEndpoint(serviceHost, typeof(ITGConnectivity));
			serviceHost.Authorization.ServiceAuthorizationManager = new AdministrativeAuthorizationManager();	//only admins can diddle us
		}

		/// <summary>
		/// Opens all created <see cref="ServiceHost"/>s
		/// </summary>
		void OnlineAllHosts()
		{
			serviceHost.Open();
			foreach (var I in hosts)
				I.Value.Open();
		}

		/// <summary>
		/// Creates a <see cref="ServiceHost"/> for <paramref name="singleton"/> using the default pipe, <see cref="ServiceHost.CloseTimeout"/>, and the configured <see cref="Properties.Settings.RemoteAccessPort"/>
		/// </summary>
		/// <param name="singleton">The <see cref="ServiceHost.SingletonInstance"/></param>
		/// <returns>The created <see cref="ServiceHost"/></returns>
		static ServiceHost CreateHost(object singleton, string endpointPostfix)
		{
			return new ServiceHost(singleton, new Uri[] { new Uri(String.Format("net.pipe://localhost/{0}", endpointPostfix)), new Uri(String.Format("https://localhost:{0}/{1}", Properties.Settings.Default.RemoteAccessPort, endpointPostfix)) })
			{
				CloseTimeout = new TimeSpan(0, 0, 5)
			};
		}

		/// <summary>
		/// Creates <see cref="ServiceHost"/>s for all <see cref="ServerInstance"/>s as listed in <see cref="Properties.Settings.InstancePaths"/>
		/// </summary>
		void SetupInstances()
		{
			hosts = new Dictionary<string, ServiceHost>();
			var pathsToRemove = new List<string>();
			foreach (var I in Properties.Settings.Default.InstancePaths)
				if (SetupInstance(I) != null)
					pathsToRemove.Add(I);
		}

		/// <summary>
		/// Unlocks a <see cref="ServerInstance.LoggingID"/> acquired with <see cref="LockLoggingID"/>
		/// </summary>
		/// <param name="ID">The <see cref="ServerInstance.LoggingID"/> to unlock</param>
		void UnlockLoggingID(byte ID)
		{
			lock (UsedLoggingIDs)
			{
				UsedLoggingIDs.Remove(ID);
			}
		}

		/// <summary>
		/// Gets and locks a <see cref="ServerInstance.LoggingID"/>
		/// </summary>
		/// <returns>A logging ID for the <see cref="ServerInstance"/> must be released using <see cref="UnlockLoggingID(byte)"/></returns>
		byte LockLoggingID()
		{
			lock (UsedLoggingIDs)
			{
				for (byte I = 1; I < 100; ++I)
					if (!UsedLoggingIDs.Contains(I))
					{
						return I;
					}
			}
			throw new Exception("All logging IDs in use!");
		}

		/// <summary>
		/// Creates and starts a <see cref="ServiceHost"/> for a <see cref="ServerInstance"/> at <paramref name="path"/>
		/// </summary>
		/// <param name="path">The path to the <see cref="ServerInstance"/></param>
		/// <returns>The inactive <see cref="ServiceHost"/> on success, <see langword="null"/> on failure</returns>
		ServiceHost SetupInstance(string path)
		{
			ServerInstance instance;
			string instanceName;
			try
			{
				var config = InstanceConfig.Load(path);
				if (hosts.ContainsKey(path))
				{
					var datInstance = ((ServerInstance)hosts[path].SingletonInstance);
					WriteEntry(String.Format("Unable to start instance at path {0}. Has the same name as instance at path {1}. Detaching...", path, datInstance.ServerDirectory()), EventID.InstanceInitializationFailure, EventLogEntryType.Error, LoggingID);
					return null;
				}
				if (!config.Enabled)
					return null;
				var ID = LockLoggingID();
				WriteEntry(String.Format("Instance {0} ({1}) assigned logging ID {2}", config.Name, path, ID), EventID.InstanceIDAssigned, EventLogEntryType.Information, ID);
				instanceName = config.Name;
				instance = new ServerInstance(config, ID);
			}
			catch (Exception e)
			{
				WriteEntry(String.Format("Unable to start instance at path {0}. Detaching... Error: {1}", path, e.ToString()), EventID.InstanceInitializationFailure, EventLogEntryType.Error, LoggingID);
				return null;
			}

			var host = CreateHost(instance, String.Format("{0}/{1}", Interface.InstanceInterfaceName, instanceName));
			hosts.Add(instanceName, host);
			
			foreach (var J in Interface.ValidInterfaces)
				AddEndpoint(host, J);

			host.Authorization.ServiceAuthorizationManager = instance;
			return host;
		}

		/// <summary>
		/// Adds a WCF endpoint for a component <paramref name="typetype"/>
		/// </summary>
		/// <param name="host">The service host to add the component to</param>
		/// <param name="typetype">The type of the component</param>
		/// <param name="PipePrefix">The URI prefix for accessing the <paramref name="host"/></param>
		void AddEndpoint(ServiceHost host, Type typetype)
		{
			var bindingName = typetype.Name;
			host.AddServiceEndpoint(typetype, new NetNamedPipeBinding() { SendTimeout = new TimeSpan(0, 0, 30), MaxReceivedMessageSize = Interface.TransferLimitLocal }, bindingName);
			var httpsBinding = new WSHttpBinding()
			{
				SendTimeout = new TimeSpan(0, 0, 40),
				MaxReceivedMessageSize = Interface.TransferLimitRemote
			};
			var requireAuth = typetype.Name != typeof(ITGConnectivity).Name;
			httpsBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;
			httpsBinding.Security.Mode = requireAuth ? SecurityMode.TransportWithMessageCredential : SecurityMode.Transport;	//do not require auth for a connectivity check
			httpsBinding.Security.Message.ClientCredentialType = requireAuth ? MessageCredentialType.UserName : MessageCredentialType.None;
			host.AddServiceEndpoint(typetype, httpsBinding, bindingName);
		}

		/// <summary>
		/// Shutsdown the WCF <see cref="host"/> and calls <see cref="IDisposable.Dispose"/> on it's <see cref="ServerInstance"/>
		/// </summary>
		protected override void OnStop()
		{
			try
			{
				foreach (var I in hosts)
				{
					var host = I.Value;
					var instance = (ServerInstance)host.SingletonInstance;
					host.Close();
					instance.Dispose();
				}
			}
			catch (Exception e)
			{
				WriteEntry(e.ToString(), EventID.ServiceShutdownFail, EventLogEntryType.Error, LoggingID);
			}
			Properties.Settings.Default.Save();
			ActiveService = null;
		}

		/// <inheritdoc />
		public void VerifyConnection() { }

		/// <inheritdoc />
		public void PrepareForUpdate()
		{
			foreach (var I in hosts)
				((ServerInstance)I.Value.SingletonInstance).Reattach(false);
		}

		/// <inheritdoc />
		public ushort RemoteAccessPort()
		{
			return Properties.Settings.Default.RemoteAccessPort;
		}

		/// <inheritdoc />
		public string SetRemoteAccessPort(ushort port)
		{
			if (port == 0)
				return "Cannot bind to port 0";
			Properties.Settings.Default.RemoteAccessPort = port;
			return null;
		}

		/// <inheritdoc />
		public string Version()
		{
			return VersionString;
		}

		/// <inheritdoc />
		public bool SetPythonPath(string path)
		{
			if (!Directory.Exists(path))
				return false;
			Properties.Settings.Default.PythonPath = Path.GetFullPath(path);
			return true;
		}

		/// <inheritdoc />
		public string PythonPath()
		{
			return Properties.Settings.Default.PythonPath;
		}

		/// <inheritdoc />
		public IDictionary<string, string> ListInstances()
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public string CreateInstance(string Name, string path)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public string ImportInstance(string path)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public bool InstanceEnabled(string Name)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public string SetInstanceEnabled(string Name, bool enabled)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public string RenameInstance(string name, string new_name)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public string DetachInstance(string name)
		{
			throw new NotImplementedException();
		}
	}
}

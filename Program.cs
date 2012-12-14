using System;
using System.Collections.Generic;
using System.Text;
using ManagedUPnP;
using System.Windows.Forms;
using System.Net;
using System.Threading;

namespace Uppo
{
    class Program
    {
        const string UPPO_VERSION = "0.1a";
        const string UPPO_LONGNAME = "UPnP Port Opener v" + UPPO_VERSION;

        static UInt16 fwPort;
        static string fwDescription = "";
        static string fwProtocol = "TCP";

        static bool done = false;

        private static AutoEventedDiscoveryServices<Service> mdsServices;

        static List<Service> listServices = new List<Service>();

        static void Main(string[] args)
        {
            Console.WriteLine("\n "+UPPO_LONGNAME+", (c) 2012 piksel bitworks");
            Console.Title = UPPO_LONGNAME;

            if (args.Length < 2)
            {
                Console.WriteLine("\n Usage:");
                Console.WriteLine("   uppo PORT \"DESCRIPTION\"\n");
                return;
            }

            fwPort = UInt16.Parse(args[0]);
            fwDescription = args[1];

            // Setup Managed UPnP Logging
            ManagedUPnP.Logging.LogLines += new LogLinesEventHandler(Logging_LogLines);
            ManagedUPnP.Logging.Enabled = true;

            // Create discovery for all service and device types
            mdsServices = new AutoEventedDiscoveryServices<Service>(null);

            // Try to resolve network interfaces if OS supports it
            mdsServices.ResolveNetworkInterfaces = true;

            // Assign events
            mdsServices.CanCreateServiceFor += new AutoEventedDiscoveryServices<Service>.
                CanCreateServiceForEventHandler(mdsServices_CanCreateServiceFor);

            mdsServices.CreateServiceFor += new AutoEventedDiscoveryServices<Service>.
                CreateServiceForEventHandler(mdsServices_CreateServiceFor);

            mdsServices.StatusNotifyAction += new AutoEventedDiscoveryServices<Service>.
                StatusNotifyActionEventHandler(mdsServices_StatusNotifyAction);

            Console.WriteLine("\n# Checking firewall rules...");

            ManagedUPnP.WindowsFirewall.CheckUPnPFirewallRules(null);

            mdsServices.SearchComplete += new EventHandler(mdsServices_SearchComplete);

            Console.WriteLine("\n# Scanning for UPnP devices and services...");

            // Start async discovery
            mdsServices.ReStartAsync();

            while (!done) Thread.Sleep(1000);

#if DEBUG
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadLine();
#endif

        }

        private static void ForwardPort()
        {
            Console.WriteLine("\n# Filtering for router(s)...");
            List<Service> wicServices = new List<Service>();
            foreach (Service s in listServices)
            {
                if (s.FriendlyServiceTypeIdentifier == "WANIPConnection:1")
                {
                    wicServices.Add(s);
                    Console.WriteLine("  Found router: " + s.Device.FriendlyName);
                }
            }

            AddPortMappingArgs fwArgs = new AddPortMappingArgs
            {
                NewEnabled = true,
                NewExternalPort = fwPort,
                NewInternalPort = fwPort,
                NewLeaseDuration = 0,
                NewPortMappingDescription = fwDescription,
                NewProtocol = fwProtocol,
                NewRemoteHost = ""
            };
            
            string fwClientIP;
            foreach (Service s in wicServices)
            {
                fwClientIP = "0.0.0.0";
                Console.WriteLine(String.Format("\n# Attempting to open ports in device {0}...", s.Device.FriendlyName));
                foreach (IPAddress ip in s.Device.AdapterIPAddresses)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        fwClientIP = ip.ToString();
                    }
                }
                fwArgs.NewInternalClient = fwClientIP;
                Console.Write(String.Format("  Mapping {2}:{3} -> {0}:{1} ({4})...",
                    fwArgs.NewInternalClient,
                    fwArgs.NewInternalPort,
                    s.Device.UniqueDeviceName,
                    fwArgs.NewExternalPort,
                    fwArgs.NewProtocol
                ));
                try {
                    s.InvokeAction("AddPortMapping", fwArgs.ArgsArray);
                    Console.WriteLine(" Success!");
                }
                catch(Exception x) {
                    Console.WriteLine(" Failed!");
                    Console.WriteLine("   Exception: " + x.Message);
                }
                Console.Write("  Checking port mapping...");

                try {
                    UInt16 spcPort;
                    string spcClientIP;
                    bool spcEnabled;
                    string spcDescr;
                    UInt32 spcLease;

                    s.InvokeAction<UInt16,string,bool,string,UInt32>(
                        "GetSpecificPortMappingEntry",
                        out spcPort,
                        out spcClientIP,
                        out spcEnabled,
                        out spcDescr,
                        out spcLease,
                        new object[] { "", fwPort, fwProtocol });

                    if(spcClientIP == fwClientIP){
                        Console.WriteLine(" Success!");
                    }
                    else {
                        Console.WriteLine(" Error!");
                        Console.WriteLine("  Mapped to another address:" + spcClientIP);
                    }

                }
                catch (Exception x){
                    Console.WriteLine(" Failed!");
                    Console.WriteLine("   Exception: " + x.Message);
                }

            }

            Console.WriteLine("\n# All done!\n");

            done = true;

        }

        static void mdsServices_SearchComplete(object sender, EventArgs e)
        {
            ForwardPort();
        }

        static void mdsServices_StatusNotifyAction(object sender, AutoEventedDiscoveryServices<Service>.StatusNotifyActionEventArgs e)
        {
            Service service;
            switch (e.NotifyAction)
            {
                case AutoDiscoveryServices<Service>.NotifyAction.ServiceAdded:
                    service = (Service)e.Data;
                    Console.WriteLine("+ Added Service: " + service.Name);
                    listServices.Add(service);
                    break;

                case AutoDiscoveryServices<Service>.NotifyAction.DeviceRemoved:
                    string device = (string)e.Data;

                    List<Service> listPendRemove = new List<Service>();
                    foreach (Service s in listServices)
                        if (s.Device.UniqueDeviceName == device)
                            listPendRemove.Add(s);

                    foreach (Service r in listPendRemove)
                        listServices.Remove(r);

                    Console.WriteLine("- Removed Device: " + device);

                    break;

                case AutoDiscoveryServices<Service>.NotifyAction.ServiceRemoved:
                    service = (Service)e.Data;
                    Console.WriteLine("- Removed Service: " + service.Name);
                    listServices.Remove(service);
                    break;
            }
        }

        static void mdsServices_CreateServiceFor(object sender, AutoEventedDiscoveryServices<Service>.CreateServiceForEventArgs e)
        {
            e.CreatedAutoService = e.Service;
        }

        static void mdsServices_CanCreateServiceFor(object sender, AutoEventedDiscoveryServices<Service>.CanCreateServiceForEventArgs e)
        {
            e.CanCreate = true;
        }

        static void Logging_LogLines(object sender, LogLinesEventArgs e)
        {
            int maxlen = Console.BufferWidth - (e.Indent + 3);
            if (e.Lines.Length > maxlen)
            {
                string s = e.Lines;
                while (s.Length > 0)
                {
                    int cut = s.IndexOf("\r\n")+2;
                    if (cut == 1 || cut > maxlen)
                        cut = (s.Length > maxlen ? maxlen : s.Length);
                    Console.WriteLine(new String(' ', e.Indent + 2) + s.Substring(0, cut).Replace("\r\n", ""));
                    s = s.Substring(cut);
                }
                
            }
            else {
                Console.WriteLine(new String(' ', e.Indent + 2) + e.Lines);
            }
        }
    }

    struct AddPortMappingArgs
    {
        public string NewRemoteHost;
        public UInt16 NewExternalPort;
        public string NewProtocol;
        public UInt16 NewInternalPort;
        public string NewInternalClient;
        public bool NewEnabled;
        public string NewPortMappingDescription;
        public UInt32 NewLeaseDuration;

        public Object[] ArgsArray
        {
            get
            {
                var a = new Object[8];
                a[0] = NewRemoteHost;
                a[1] = NewExternalPort;
                a[2] = NewProtocol;
                a[3] = NewInternalPort;
                a[4] = NewInternalClient;
                a[5] = NewEnabled;
                a[6] = NewPortMappingDescription;
                a[7] = NewLeaseDuration;
                return a;
            }
        }
    }
}

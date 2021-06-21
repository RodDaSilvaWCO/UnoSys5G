using Microsoft.MobileBlazorBindings.WebView.Windows;
using System;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Platform.WPF;
using UnoSys5G.Core;
using System.Threading;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnoSys.Kernel;
using UnoSys5G.Core.Interfaces;
using UnoSys5G.Core.Models;

namespace UnoSys5G.Windows
{
    public class MainWindow : FormsApplicationPage, IApp
    {
        static string clientID = null;
        static int demoID = 0;
        static bool initialAutonomousMode = true;
        static bool play = false;
        static float currentSpeed = 1;
        static DateTime sysTime = default(DateTime);
        static uint meshNodeID = uint.MaxValue;
        static int collisionCount = 0;

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null)
            {
                if (args.Length > 0)
                {
                    // First parameter is ClientID
                    clientID = args[0];
                    if (args.Length > 1)
                    {
                        // 2nd parameter is DemoID
                        demoID = Int32.Parse(args[1]);
                        if (args.Length > 2)
                        {
                            // Thrid paramater for demo 2 is the NodeID
                            uint.TryParse(args[2], out meshNodeID);
                        }
                    }
                }
            }
            HostCrc32 crc32 = new HostCrc32();
            var c = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "appsettings.json"), optional: false);
            var hostConfig = c.Build();
            var kernelOptions = hostConfig.GetSection("DefaultKernelOptions").Get<KernelOptions>();
            var mniBase = hostConfig.GetSection("MeshNodeInfo").Get<MeshNodeInfo>();
            MeshNodeInfo meshNodeInfo = null;
            kernelOptions.NodeID = Guid.NewGuid();
            meshNodeInfo = new MeshNodeInfo(
                meshnodeid: (meshNodeID == uint.MaxValue ? mniBase.MeshNodeID : meshNodeID),
                meshnodepublickey: mniBase.MeshNodePublicKey,
                meshid: Convert.ToUInt64(demoID),               // drive by commandline parameter
                                                                // If configuration doesn't specify a MeshNodeNumber (i.e.; is 0) then base on NodeID 
                meshnodenumber: (mniBase.MeshNodeNumber == 0 ? BitConverter.ToUInt32(crc32.ComputeHash(kernelOptions.NodeID.ToByteArray()), 0) : mniBase.MeshNodeNumber),
                meshrelayserver: mniBase.MeshRelayServer,
                meshrelayseverport: mniBase.MeshRelayServerPort
                ) ;
            using (TimerService timerService = new TimerService())
            {
                var app = new System.Windows.Application();
                var mainWindow = new MainWindow(kernelOptions, timerService, meshNodeInfo);

                //mainWindow.Title = $"UnoSys5G - {(demoID == 0 ? "Mesh Broadcast Sync Demo" : "Mesh P2P Demo")}";
                mainWindow.Title = $"UnoSys5G Client:({clientID})";
               
                app.Run(mainWindow);
                if (mainWindow.NetworkOverlay != null)
                {
                    mainWindow.NetworkOverlay.Disconnect().Wait();
                }
            }
        }

        public MainWindow(IKernelOptions kernelOptions, TimerService timerService, IMeshNodeInfo meshNodeInfo)
        {
            //this.Background = new SolidColorBrush(Color.Black);

            Forms.Init();
            if (demoID != 0)
            {
                this.Width = (demoID == 1 ? 642 : 642 / 6);
                this.Height = (demoID == 1 ? 520 : 520 / 4);
                //this.Width =  642;
                //this.Height = 520;
            }
            else
            {
                this.Width = 520;
                this.Height = 670;
            }

            BlazorHybridWindows.Init();
            LoadApplication(new App(kernelOptions, timerService, meshNodeInfo, this));

        }

        public INetworkOverlay NetworkOverlay { get; set; }

        public bool IsAutonomousMode
        {
            get { return initialAutonomousMode; }
            set { initialAutonomousMode = value; }
        }

        public int DemoID
        {
            get
            {
                return demoID;
            }
        }

        public string ClientID
        {
            get
            {
                return clientID;
            }
        }

        public int StartDelay
        {
            get { return (demoID == 0 ? 4 : 4); }
        }

        public bool Play
        {
            get { return play; }
            set { play = value; }
        }

        public float CurrentSpeed
        {
            get { return currentSpeed; }
            set { currentSpeed = value; }
        }

        public int CollisionCount
        {
            get { return collisionCount; }
            set { collisionCount = value; }
        }

        public DateTime UnoSysClockTimeUtc
        {
            get { return sysTime; }
            set 
            {
               sysTime = value;
            }
        }
    }

    

    internal class PlaceHolderButNotUsedJournalManager : IJournalManager
    {
        public ulong[] ConfirmJournalEntries(IPeerSet peerSet, List<KeyValuePair<string, JournalEntry>> journalEntries, int sourceReplica)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.ConfirmJournalEntries");
        }

        public Task<BlockIOResponse> CreateJournalEntryAsync(IPeerSet peerSet, BlockFileInfo blockId, string context, uint crcNamePlaceHolder, BlockFileInfo deleteblockid, byte[] buffer, int bufferOffset, int bytesToWrite, int blockOffset, bool isPartialBlock, ulong blockConfirmFlagMask = 0, int sourceReplica = 0)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.CreateJournalEntryAsync");
        }

        public IJournal GetPeerSetJournal(IPeerSet peerSet)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.GetPeerSetJournal");
        }

        public void LoadUnconfirmedJournalEntries(IPeerSet peerSet) //, IProcessorConnectionPool connectionPool)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.LoadUnconfirmedJournalEntries");
        }

        public Task PeerSetConnectionEstablishedAsync(ulong peerSetId, ulong connectionId, bool isConnectionInBound)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.PeerSetConnectionEstablishedAsync");
        }

        public Task<ulong[]> SyncBlocksAsync(IPeerSet peerSet, int sourceReplica, List<ReplicaConfirmationResult> blocksToSyncWithReplica)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.SyncBlocksAsync");
        }

        public void UnLoadJournalEntries(IPeerSet peerSet)
        {
            throw new NotImplementedException("PlaceHolderButNotUsedJournalManager.UnLoadJournalEntries");
        }
    }
}

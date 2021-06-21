using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.MobileBlazorBindings;
using Xamarin.Essentials;
using Xamarin.Forms;
using UnoSys5G.Core;
//using UnoSys5G.Core.Services;
using UnoSys5G.Core.Interfaces;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnoSys.Kernel;
using UnoSys5G.Core.Contexts;
using UnoSys5G.Core.Models;
using UnoSys5G.Services;

namespace UnoSys5G
{
    public class App : Application
    {
        public App( IKernelOptions kernelOptions, TimerService timerService, IMeshNodeInfo meshNodeInfo, IApp app,  IFileProvider fileProvider = null)
        {
            var hostBuilder = MobileBlazorBindingsHost.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    // Adds web-specific services such as NavigationManager
                    services.AddBlazorHybrid();

                    // Register app-specific services

                    ILogger<IBootLoader> kernelLogger = new KernelLogger("KERN");
                    services.AddSingleton<IApp>(app);
                    services.AddTransient<HostCrc32>();
                    services.AddSingleton<IKernelOptions>(kernelOptions);
                    services.AddSingleton<IMeshNodeInfo>(meshNodeInfo);
                    services.AddSingleton<IKernelConcurrencyManager, KernelConcurrencyManager>();
                    services.AddSingleton<ILogger<IBootLoader>>(kernelLogger);
                    services.AddSingleton<CounterState>();
                    services.AddScoped<BrowserService>();
                    services.AddSingleton(timerService);
                    services.AddSingleton<ITimeContext, TimeContext>();
                    services.AddSingleton<ITime, TimeManager>();
                    services.AddSingleton<IJournalManager, PlaceHolderButNotUsedJournalManager>();
                    services.AddSingleton<INodeNameContext>(new NodeNameContext(Constants.MESH_RELAY_CLIENT));
                    services.AddSingleton<INetworkRelay, NetworkRelay>();
                    services.AddSingleton<IRelayPeerSetContext, RelayClientPeerSetContext>();
                    services.AddSingleton<IRelayManager, RelayManager>();
                    services.AddSingleton<INetworkOverlay, NetworkOverlay>();
                    if (app.DemoID == 0)
                    {
                        services.AddSingleton<IMeshSyncDemo, MeshSyncDemo>();
                        services.AddSingleton<ILogicalP2PDemo, PlaceHolderButNotUsedLogicalP2PDemo>();
                    }
                    else
                    {
                        services.AddSingleton<ILogicalP2PDemo, LogicalP2PDemo>();
                        services.AddSingleton<IMeshSyncDemo, PlaceHolderButNotUsedMeshSyncDemo>();
                    }

                }).UseWebRoot("wwwroot");
            if (fileProvider != null)
            {
                hostBuilder.UseStaticFiles(fileProvider);
            }
            else
            {
                hostBuilder.UseStaticFiles();
            }
            var host = hostBuilder.Build();

            MainPage = new ContentPage { Title = $"ClientID: {app.ClientID}" };
            //MainPage.BackgroundColor = Color.Black;
            NavigationPage.SetHasNavigationBar(MainPage, false);
//            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
            
            //MainPage = new ContentPage { };
            //MainPage = new ContentPage();
            host.AddComponent<Main>(parent: MainPage);
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }

       
    }

    public class PlaceHolderButNotUsedLogicalP2PDemo : ILogicalP2PDemo
    {
#pragma warning disable  // events are required to implement IRelayPeerSetContext interface but if aren't use generate a compiler warning - so disable warnings
        public event BufferReadyEventHandler BufferReady;
#pragma warning enable

        public Task BroadCastMeshNodeStateChange(IFrameInfo frameInfo)
        {
            throw new NotImplementedException();
        }

        public bool QueueSimulcast(int frameOrdinal, int framePieceOrdinal = -1)
        {
            throw new NotImplementedException();
        }

        public IFrameInfo FetchNextBufferedFrameInfo()
        {
            throw new NotImplementedException();
        }
    }

    public class PlaceHolderButNotUsedMeshSyncDemo : IMeshSyncDemo
    {
        public List<vD2DNodeState> CurrentMeshState => throw new NotImplementedException();

        public float CurrentXPos { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public float CurrentYPos { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsAutoMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public float Speed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Direction CurrentDirection { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public uint CurrentMeshStateHash => throw new NotImplementedException();

        public int FieldWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int FieldHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int Radius { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void AdjustPoint(float x, float y)
        {
            throw new NotImplementedException();
        }

        public float AdjustX(float x)
        {
            throw new NotImplementedException();
        }

        public float AdjustY(float y)
        {
            throw new NotImplementedException();
        }

        public Task BroadCastMeshNodeStateChange(vD2DNodeState stateChange)
        {
            throw new NotImplementedException();
        }

        public void SetRandomPoint()
        {
            throw new NotImplementedException();
        }
    }

    public class PlaceHolderButNotUsedJournalManager : IJournalManager
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

    public interface IApp
    {
        INetworkOverlay NetworkOverlay { get; set; }

        bool IsAutonomousMode { get; set; }

        int DemoID { get; }

        string ClientID { get; }

        int StartDelay { get; }

        bool Play { get; set; }

        float CurrentSpeed { get; set; }

        int CollisionCount { get; set; }

        DateTime UnoSysClockTimeUtc { get; set; }
    }
}

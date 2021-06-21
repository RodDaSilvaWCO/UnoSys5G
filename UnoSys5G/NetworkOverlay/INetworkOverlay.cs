using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnoSys.Kernel;
using UnoSys5G.Services;

namespace UnoSys5G.Core.Interfaces
{
    public interface INetworkOverlay
    {
        Task Connect();
        Task Disconnect();

        event Action TimerTick;
        event RelayPeerSetEventHandler StateChange;
        event RelayPeerSetEventHandler BroadCastStateless;

        ulong NodeID { get; }

        bool IsThisNode(ulong nodeid);

        Task<bool> MeshBroadCastMessageStatefulOneWayAsync(byte[] stateEntry);

        Task<bool> MeshBroadCastMessageStatelessOneWayAsync(byte[] payload );

        Task<IPeerSetOperationResponse> SendTwoWayAsync(IMeshRequestResponseMessage message );
    }
}

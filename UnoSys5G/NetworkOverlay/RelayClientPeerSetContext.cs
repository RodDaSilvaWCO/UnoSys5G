namespace UnoSys5G
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using UnoSys.Kernel;
    using UnoSys5G.Core;
    using UnoSys5G.Core.Interfaces;
    using UnoSys5G.Core.Models;

    public sealed class RelayClientPeerSetContext :  IRelayPeerSetContext, IDisposable, IKernelService
    {
        #region Members
        HostCrc32 _crc32 = null;
        private HttpListener httpListener = null;
        private bool listening = false;
        private Task backgroundListener = null;
        private string NodeInstanceListenerUrlTemplate = null;
        private INetworkRelay networkRelay = null;
        private TimerService timerService = null;
        private ITime timeManager = null;
       
        #endregion 

        #region Constructor
        public RelayClientPeerSetContext(ILogger<IBootLoader> logger,/*INodeInstallationContext niContext*/ IKernelOptions kernelOptions, IMeshNodeInfo meshNodeInfo,
            IKernelConcurrencyManager concurrencyManager, ITime timemanager, TimerService timerservice, INetworkRelay networkrelay, HostCrc32 crc32 )
        {
            _crc32 = crc32;
            timerService = timerservice;
            networkRelay = networkrelay;
            timeManager = timemanager;
            NodeInstanceListenerUrlTemplate = kernelOptions.NodeInstanceListenerUrlTemplate;
            ThisNodeID = BitConverter.ToUInt32(_crc32.ComputeHash(kernelOptions.NodeID.ToByteArray()), 0);
        Debug.Print($"RelayClientPeerSetContext.ctor({meshNodeInfo.MeshNodeID})");
            ThisNodeName = ThisNodeID.ToString();
            PeerSetID = BitConverter.ToUInt32(_crc32.ComputeHash(Guid.Empty.ToByteArray()), 0);
            RelaySet = new ConnectionMetaData[2, 1];
            RelaySet[0, 0] = new ConnectionMetaData(ThisNodeID, meshNodeInfo.MeshNodePublicKey, meshNodeInfo.MeshRelayServer, meshNodeInfo.MeshRelayServerPort, Constants.MESH_RELAY_CLIENT);
            RelaySet[1, 0] = new ConnectionMetaData(PeerSetID, meshNodeInfo.MeshNodePublicKey, meshNodeInfo.MeshRelayServer, meshNodeInfo.MeshRelayServerPort, Constants.MESH_RELAY_SERVER);
            
            CancellationTokenSource = networkRelay.CancellationTokenSource;
            PeerSet =
                RelayPeerSet.Create(
                    logger, concurrencyManager,
                    timeManager,
                    networkRelay.ConnectionPool,
                    PeerSetID,
                    RelaySet,
                    2,  // matrixLength
                    1,  // matrixWidth
                   -1,  // processor cluster not in RelaySet
                   -1,  // processor replica not in RelaySet
                    false,   // Journal Manager not needed
                    networkRelay.CancellationToken);
            ((RelayPeerSet)PeerSet).StateChange += ReceiveMeshStateChange;
            ((RelayPeerSet)PeerSet).ProcessMeshRequestResponseMessage += ReceiveMeshRequestResponseMessage;
            ((RelayPeerSet)PeerSet).Reliability += RelayClientPeerSetContext_Reliability;
            ((RelayPeerSet)PeerSet).ConnectionMaintenance += RelayClientPeerSetContext_ConnectionMaintenance;
        }

        private void RelayClientPeerSetContext_ConnectionMaintenance(ulong connectionId)
        {
            //Debug.Print($"0000000000000000000000 RelayClientPeerSetContext_ConnectionMaintenance 000000000000000000000000000000000");
        }

        private void RelayClientPeerSetContext_Reliability(ConnectionReliability connectionReliability)
        {
            //Debug.Print($"0000000000000000000000 RelayClientPeerSetContext_ConnectionReliability 000000000000000000000000000000000");
        }
        #endregion

        #region Events
#pragma warning disable  // events are required to implement IRelayPeerSetContext interface but if aren't use generate a compiler warning - so disable warnings
        public event RelayPeerSetEventHandler StateChange;
        public event RelayPeerSetEventHandler BroadCastStateless;
#pragma warning enable
        #endregion

        private void ReceiveMeshStateChange(object state)
        {
            // If we make it here the Relay CLIENT has received a Mesh State Change event so we want to update the meshState
            // by delegating to the NetworkOverlay
            StateChange?.Invoke(state);  // bubble the event upto NetworkOverlay.ReceiveMeshStateChange()


            //await Task.CompletedTask;

        }

        #region IDisposable Implementation
        public void Dispose()
        {
            ((RelayPeerSet)PeerSet).StateChange -= ReceiveMeshStateChange;
            ((RelayPeerSet)PeerSet).ProcessMeshRequestResponseMessage -= ReceiveMeshRequestResponseMessage;
            _crc32?.Dispose();
            _crc32 = null;
            RelaySet = null;
            ThisNodeName = null;
            PeerSet?.Dispose();
            PeerSet = null;
            httpListener?.Close();
            httpListener = null;
            backgroundListener?.Dispose();
            backgroundListener = null;
            CancellationTokenSource = null;  // We do NOT dispose here as this will be disposed inside NetworkRelay
            networkRelay = null;
            timerService = null;
            timeManager = null;
            //Debug.Print("********************** RelayClientPeerSetContext.Displosed()");
        }
        #endregion

       

        public void RestoreState(IntPtr state, int stateSize)
        {
            throw new NotImplementedException();
        }

        public IntPtr SaveState(ref int stateSize)
        {
            throw new NotImplementedException();
        }

        #region Events
        public event Action TimerTick;
        #endregion

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add(string.Format(NodeInstanceListenerUrlTemplate, "80", ThisNodeID));
            httpListener.Start();
            listening = true;
            backgroundListener = ListenForInboundRequest();
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            listening = false;
            CancellationTokenSource.Cancel();
            // *** NOTE:  We MUST NOT await the line below.  Instead we use the GetAwaiter().GetResult() to wait
            //            for the task to complete.   The reason is that using await causes the caller of this assembly
            //            to hold some reference to this type in memory which later prevents the UnoSys.Kernel.dll assembly
            //            (which contains this type) to unload.  This is likely due to the compiler generated "state machine"
            //            that gets inserted into the code when await is used.  This problem does not occur when using the pure
            //            api approach to waiting on the Task to complete (i.e.; GetAwaiter()).
            backgroundListener.GetAwaiter().GetResult();
            httpListener.Stop();
            await Task.CompletedTask;
        }


        #region IRelayPeerSetContext Implementation

        public ConnectionMetaData[,] RelaySet { get; private set; } = null;

        public int RelayNodeCount { get; } = 0;

        public ulong ThisNodeID { get; } = 0;

        public ulong PeerSetID { get; } = 0;

        public string ThisNodeName { get; private set; } = null;

        public IPeerSet PeerSet { get; private set; } = null;

        //public IMeshState<vD2DNodeState> MeshState
        //{
        //    get
        //    {
        //        throw new NotImplementedException();
        //    }
        //}
        public CancellationTokenSource CancellationTokenSource { get; private set; }

        public void StartTimer(int intervalInMilliseconds)
        {
            timerService.StartWithInterval(intervalInMilliseconds);
            timerService.TimerTick += TimerService_TimerTick;
        }

        public void StopTimer()
        {
            timerService.TimerTick -= TimerService_TimerTick;
            timerService.Stop();
        }
        #endregion




        #region Helpers
        private void TimerService_TimerTick()
        {
            if (TimerTick != null)
            {
                TimerTick.Invoke();
            }
        }



        private async Task<IPeerSetOperationResponse> ReceiveMeshRequestResponseMessage(IPeerSetOperation operationRequest, long elapseTime)
        {
            //Debug.Print($"~~~~~~ RelayClientPeerSetContext.ReceiveMeshRequestResponseMessage( BEGIN ) ");
            IPeerSetOperationResponse response = null;
            long elapseStartTime = timeManager.ProcessorUtcTimeInTicks;
            try
            {
                MeshRequestResponseMessageType messageType = MeshRequestResponseMessageType.UNINITIALIZED;
                ulong timestamp = 0;
                ulong fromConnectionId = 0;
                ulong toConnectionId = 0;
                byte[] payload = null;
                if (RelayPeerSet.ParseMeshRequestResponseRequest(operationRequest, out messageType, out timestamp, out fromConnectionId, out toConnectionId, out payload) == IPCOperationResponseCode.OK)
                {
                    switch (messageType)
                    {
                        case MeshRequestResponseMessageType.FETCH_VIDEO_FRAME:
                            {
                                PeerSetOperationResponse result = null;
                                // NOTE:  If we make it here we are in a Client Destination node of a P2P two-way call from the source Node to the Destination Node.
                                //        We have received the request from the "source" Node via the Relay and now need to process the message and send a response.
                                Debug.Print($"~~~~~~ RelayClientPeerSetContext.ReceiveMeshRequestResponseMessage( MADE IT HALF WAY!!!!  ) ");
                                // %TODO%:  FOR NOW - look up Frame goes here.
                                result = (PeerSetOperationResponse) networkRelay.ConnectionPool.EmptyPayLoadResponse(operationRequest, IPCOperationResponseCode.UNEXPECTED_OPERATION, elapseStartTime);
                                break;
                            }
                        default:
                            Debug.Print($"^^^^^^^^ RelayClientPeerSetContext.ReceiveMeshRequestResponseMessage( UNEXPECTED MessageType ) - {messageType}");
                            break;
                    }
                }
                else
                {
                    // There was a problem parsing the request so return an empty response error message
                    response = networkRelay.ConnectionPool.EmptyPayLoadResponse(operationRequest, IPCOperationResponseCode.INVALID_ARGUMENT, elapseStartTime);
                }
            }
            catch (Exception ex )
            {
                Debug.Print($"^^^^^^^^ RelayClientPeerSetContext.ReceiveMeshRequestResponseMessage( ERROR ) - {ex.Message}");
                // There was a problem so return an empty response error message
                response = networkRelay.ConnectionPool.EmptyPayLoadResponse(operationRequest, IPCOperationResponseCode.UNEXPECTED_OPERATION, elapseStartTime);
            }
            //Debug.Print($"^^^^^^^^ RelayClientPeerSetContext.ReceiveMeshRequestResponseMessage( END )");
            return await Task.FromResult<IPeerSetOperationResponse>(response);
        }


        private async Task ListenForInboundRequest()
        {
            // This listener listens for HTTP GET Requests and returns 403 Forbidden for each one.
            // External nodes will use this end point to check if this node is "up".  A 403 is a 
            // response that says "I am up but I don't support any operations" because this endpoint
            // doesn't actually do anything but respond with 403s as a signal of being up.
            while (listening)
            {
                try
                {
                    Task<HttpListenerContext> httpListenerTask = httpListener.GetContextAsync();
                    var tcs = new TaskCompletionSource<bool>();
                    HttpListenerContext socketContext = null;
                    Task resultTask = null;
                    using (networkRelay.CancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                    {
                        resultTask = await Task.WhenAny(new Task[] { httpListenerTask, tcs.Task }).ConfigureAwait(false);
                        if (httpListenerTask != resultTask)
                        {
                            // This routine was cancelled from outside (i.e.; via the passed in cancellationToken) so exit the loop 
                            // stop the HttpListener and allow the routine to end
                            break;
                        }
                        else
                        {
                            socketContext = ((Task<HttpListenerContext>)resultTask).Result;
                        }
                    }
                    //var requestQuery = socketContext.Request.Url.Query;
                    //if (!string.IsNullOrEmpty(requestQuery))
                    //{
                    //	if (requestQuery.ToUpper() == "?STOP")
                    //	{
                    //		// NOTE:  We get here if the service is either STOPPED or RESTARTED!
                    //		PAL.Log($"^^^Controller.ListenForInboundRequest() - STOP");
                    //		firmwareApplicationLifetime?.StopApplication();
                    //	}
                    //	if (requestQuery.ToUpper() == "?REBOOT")
                    //	{
                    //		// NOTE:  We get here if the ?REBOOT url is invoked (e.g.; http://localhost:50015/WorldComputerNode/BC811864/?REBOOT)
                    //		PAL.Log($"^^^Controller.ListenForInboundRequest() - REBOOT");
                    //		KernelContext.IsKernelRebooting = true;  // Signal the kernel is rebooting - tested within FileSystem.CbFsUnmount() and SessionManager.SaveState()
                    //		firmwareApplicationLifetime?.RebootApplication();
                    //	}
                    //	if (requestQuery.ToUpper() == "?RESTART")
                    //	{
                    //		// NOTE:  We get here if the ?RESTART url is invoked (e.g.; http://localhost:50015/WorldComputerNode/BC811864/?RESTART),
                    //		//        which causes the service to be restarted - ultimately causeing the ?STOP above case above to be invoked
                    //		PAL.Log($"^^^Controller.ListenForInboundRequest() - RESTART");
                    //		RestartService();
                    //	}
                    //}
                    socketContext.Response.StatusCode = 403;
                    socketContext.Response.Close();
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex);
                    Debug.Print($"{ex.ToString()}");
                }
            }  // while
        }
        #endregion
    }
}

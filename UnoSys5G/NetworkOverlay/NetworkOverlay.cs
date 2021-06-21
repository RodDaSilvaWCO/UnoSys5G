using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnoSys.Kernel;
using UnoSys5G.Core;
using UnoSys5G.Core.Interfaces;

namespace UnoSys5G
{
    public class NetworkOverlay : INetworkOverlay, IDisposable
    {
        #region Member fields
		private IRelayManager relayManager = null;
		private ITime timeManager = null;
		private IRelayPeerSetContext relayPeerSetContext = null;
		private HostCrc32 _crc32 = null;
		private ulong nodeID = 0;
		
		
		#endregion

		#region Constructors
		public NetworkOverlay(IKernelOptions kernelOptions, ITime timemanager, IRelayManager relaymanager,
							IRelayPeerSetContext rpsContext,  HostCrc32 crc32 )
        {
			timeManager = timemanager;
			relayManager = relaymanager;
			relayPeerSetContext = rpsContext;
			_crc32 = crc32;
			nodeID = relayManager.NodeID;
		}
		#endregion

		#region Events
		public event Action TimerTick;
		public event RelayPeerSetEventHandler StateChange;
		public event RelayPeerSetEventHandler BroadCastStateless;
		#endregion

		#region IDisposable Implmentation
		public void Dispose()
		{
			relayManager = null;
			timeManager = null;
			_crc32 = null;
			relayPeerSetContext = null;

		}
        #endregion


        #region INetworkOverlay Implementation
        public async Task Connect()
        {
			
			relayPeerSetContext.StateChange += ReceiveMeshStateChange;
            relayPeerSetContext.BroadCastStateless += RelayPeerSetContext_BroadCastStateless;
			await ((IKernelService)(relayManager)).StartAsync(relayManager.NetworkRelay.CancellationToken).ConfigureAwait(false);
            relayManager.TimerTick += TimerTickHandler;
			relayManager.StartTimer(10);
            
			//}
			//else
			// {
			//	throw new InvalidOperationException("Connection already established.");
			//}
		}

        private void RelayPeerSetContext_BroadCastStateless(object message)
        {
			// If we make it here the CLIENT has received a MESH BROADCAST STATELESS message
			BroadCastStateless?.Invoke(message); // pass along to interested "app level" subscribers 

		}

        private void ReceiveMeshStateChange(object meshstate)
        {
			// If we make it here the CLIENT has received a MESH STATE CHANGE event
			StateChange?.Invoke(meshstate);  // pass along to interested "app level" subscribers
		}


	

		private void TimerTickHandler()
        {
			TimerTick?.Invoke();
        }

        public async Task Disconnect()
        {
			if ( relayManager != null)
            {
				relayManager.TimerTick -= TimerTickHandler;
				relayManager.StopTimer();
				await ((IKernelService)(relayManager)).StopAsync(relayManager.NetworkRelay.CancellationToken).ConfigureAwait(false);
            }
			relayPeerSetContext.BroadCastStateless -= RelayPeerSetContext_BroadCastStateless;
			relayPeerSetContext.StateChange -= ReceiveMeshStateChange;
		}

		public ulong NodeID
        {
            get
            {
				return nodeID;
            }
        }

		

		public bool IsThisNode(ulong nodeid)
		{
			return nodeid == NodeID;
		}

		public async Task<bool> MeshBroadCastMessageStatefulOneWayAsync(byte[] stateEntry )
		{
            // NOTE:  A STATEFUL broadcast is performed by sending a single message to the Relay Server which will 
            //		  aggregate it with the current state of all Client nodes, and then send the aggregated state
			//		  to all of the connected nodes in the Mesh
            var connectionId = BitConverter.ToUInt32(_crc32.ComputeHash(Guid.Empty.ToByteArray()), 0);
            var pcPool = relayManager.NetworkRelay.ConnectionPool;
            bool result = false;
            if (!pcPool[connectionId].Link.IsNotConnected)
            {
                PeerSetOperationRequest operationRequest = new PeerSetOperationRequest(pcPool[connectionId],
                                        relayPeerSetContext.PeerSetID, IPCOperation.RELAY_MESH_NODE_STATEFUL_BROADCAST, pcPool.Nonce,
                                        CreateRequestBuffer(stateEntry), 0);
                CreateMeshStateEntryChangeRequest(operationRequest, timeManager.ProcessorUtcTimeInTicks, stateEntry);
                result = await operationRequest.TargetProcessorConnection.SendOneWayAsync(operationRequest,
                    CancellationToken.None, true).ConfigureAwait(false);
            }
            return result;
		}

		public async Task<bool> MeshBroadCastMessageStatelessOneWayAsync(byte[] message )
		{
			// NOTE:  The broadcast is performed by sending a single message to the Relay Server which will in turn 
			//		  immediately send it to all of the connected nodes in the Mesh.
			//		  The difference between this operation and that of MeshBroadCastMessageStatefulOneWayAsync() above
			//		  is that for this one the Relay does not maintain an aggregate state for the mesh, it instead
			//		  simply broadcasts the message to all other nodes, and assumes the nodes will process the information
			//		  accordingly.  This method does NOT return any response from the Relay Server - it has fire and forget semantics.
			var connectionId = BitConverter.ToUInt32(_crc32.ComputeHash(Guid.Empty.ToByteArray()), 0);
			var pcPool = relayManager.NetworkRelay.ConnectionPool;
			if (!pcPool[connectionId].Link.IsNotConnected)
			{
				PeerSetOperationRequest operationRequest = new PeerSetOperationRequest(pcPool[connectionId],
										relayPeerSetContext.PeerSetID, IPCOperation.RELAY_MESH_NODE_STATELESS_BROADCAST, pcPool.Nonce,
										CreateRequestBuffer(message), 0);
				CreateMeshStateEntryChangeRequest(operationRequest, timeManager.ProcessorUtcTimeInTicks, message);
				var fireAndForget = operationRequest.TargetProcessorConnection.SendOneWayAsync(operationRequest,
					CancellationToken.None, true).ConfigureAwait(false);
			}
			return await Task.FromResult<bool>(true);
		}

		public async Task<IPeerSetOperationResponse> SendTwoWayAsync(IMeshRequestResponseMessage message)
        {
			//Debug.Print($"CLIENT Sending Two-way Request Responset to Server (BEGIN) ");
			PeerSetOperationResponse result = null;
			try
			{
				// NOTE:  The two-way request/response call is acheived by sending a single message to the Relay Server which will in turn 
				//		  send it on (i.e.; relay it) to the designated target node, wait for a response and then return the response back to this node
				var connectionId = BitConverter.ToUInt32(_crc32.ComputeHash(Guid.Empty.ToByteArray()), 0);  // Relay Node
				var pcPool = relayManager.NetworkRelay.ConnectionPool;
				
				if (!pcPool[connectionId].Link.IsNotConnected)
				{
					PeerSetOperationRequest operationRequest = new PeerSetOperationRequest(pcPool[connectionId],
											relayPeerSetContext.PeerSetID, IPCOperation.RELAY_MESH_TWOWAY_REQUEST, pcPool.Nonce,
											CreateRelayMeshTwoWayRequestBuffer(message), 0);
					CreateRelayMeshTwoWayRequest(operationRequest, timeManager.ProcessorUtcTimeInTicks, message);
					result = (PeerSetOperationResponse)await operationRequest.TargetProcessorConnection.SendTwoWayAsync("NetworkRelay.SendTwoWayAsync()",operationRequest,
						CancellationToken.None, 50, true).ConfigureAwait(false);
				}
			}
			catch(Exception ex)
            {
				Debug.Print($"NetworkOverlay.SendTwoWayAsync() (ERROR) - {ex.Message} ");
			}
			//Debug.Print($"CLIENT Sending Two-way Request Responset to Server (END) - {result.ResponseCode} ");
			return result;
        }
        #endregion

        #region Helpers
        private byte[] CreateRequestBuffer( byte[] state )
		{
			// Request:  Operation + CorrelationId + timeStamp + state size + state 
			return new byte[(2*sizeof(int)) + (2 * sizeof(long)) + state.Length];
		}

		

		private void CreateMeshStateEntryChangeRequest(IPeerSetOperation operationRequest, long timestampinticks, byte[] state )
		{
			int bytesWritten = operationRequest.PayloadOffset;
			byte[] payload = operationRequest.Payload;
			try
			{
				// Request:  Operation + CorrelationId + timestamp + state size + state bytes
				Buffer.BlockCopy(BitConverter.GetBytes((int)operationRequest.RequestOperation), 0, payload, bytesWritten, sizeof(int));             // IO operation
				bytesWritten += sizeof(int);
				Buffer.BlockCopy(BitConverter.GetBytes(operationRequest.CorrelationId), 0, payload, bytesWritten, sizeof(long));                    // CorrelationId (i.e.; a unique value for each message)
				bytesWritten += sizeof(long);
				Buffer.BlockCopy(BitConverter.GetBytes(timestampinticks), 0, payload, bytesWritten, sizeof(long));                                  // timestamp
				bytesWritten += sizeof(long);
				Buffer.BlockCopy(BitConverter.GetBytes( state.Length), 0, payload, bytesWritten, sizeof(int));										// state size
				bytesWritten += sizeof(int);
				Buffer.BlockCopy(state, 0, payload, bytesWritten, state.Length);																	// state Bytes
				bytesWritten += state.Length;
			}
			catch (Exception ex)
			{
				Debug.Print(ex.Message);
				throw;
			}
		}

		private byte[] CreateRelayMeshTwoWayRequestBuffer(IMeshRequestResponseMessage message)
		{
			// Request:  Operation + CorrelationId + timeStamp + messageType + fromConnectionId + toConnectionId + payload size + payload 
			return new byte[(3 * sizeof(int)) + (4 * sizeof(long)) + message.Payload.Length];
		}

		private void CreateRelayMeshTwoWayRequest(IPeerSetOperation operationRequest, long timestampinticks, IMeshRequestResponseMessage message)
		{
			int bytesWritten = operationRequest.PayloadOffset;
			byte[] payload = operationRequest.Payload;
			try
			{
				// Request:  Operation + CorrelationId + timestamp + messageType + fromConnectionId + toConnectionId + state size + state bytes
				Buffer.BlockCopy(BitConverter.GetBytes((int)operationRequest.RequestOperation), 0, payload, bytesWritten, sizeof(int));             // IO operation
				bytesWritten += sizeof(int);
				Buffer.BlockCopy(BitConverter.GetBytes(operationRequest.CorrelationId), 0, payload, bytesWritten, sizeof(long));                    // CorrelationId (i.e.; a unique value for each message)
				bytesWritten += sizeof(long);
				Buffer.BlockCopy(BitConverter.GetBytes(timestampinticks), 0, payload, bytesWritten, sizeof(long));                                  // timestamp
				bytesWritten += sizeof(long);
				Buffer.BlockCopy(BitConverter.GetBytes((int)message.MessageType), 0, payload, bytesWritten, sizeof(int));							// messageType
				bytesWritten += sizeof(int);
				Buffer.BlockCopy(BitConverter.GetBytes(message.FromConnectionId), 0, payload, bytesWritten, sizeof(ulong));                         // fromConnectionId
				bytesWritten += sizeof(ulong);
				Buffer.BlockCopy(BitConverter.GetBytes(message.ToConnectionId), 0, payload, bytesWritten, sizeof(ulong));                           // toConnectionId
				bytesWritten += sizeof(ulong);
				Buffer.BlockCopy(BitConverter.GetBytes(message.Payload.Length), 0, payload, bytesWritten, sizeof(int));                             // state size
				bytesWritten += sizeof(int);
				Buffer.BlockCopy(message.Payload, 0, payload, bytesWritten, message.Payload.Length);                                                // state Bytes
				bytesWritten += message.Payload.Length;
			}
			catch (Exception ex)
			{
				Debug.Print(ex.Message);
				throw;
			}
		}
		#endregion
	}
}

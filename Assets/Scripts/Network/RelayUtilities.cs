using System.Linq;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models;

namespace DotsTopDownTD.Network
{
    public static class RelayUtilities
    {
        public static RelayServerData GetRelayData(Allocation allocation)
        {
            var endpoint = allocation.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls");
            if (endpoint == null)
            {
                throw new System.Exception("DTLS endpoint not found");
            }

            var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);
            var allocationId = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
            var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
            var hostConnectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);  // For host, same as connectionData
            var key = RelayHMACKey.FromByteArray(allocation.Key);

            return new RelayServerData(ref serverEndpoint, 0, ref allocationId, ref connectionData, ref hostConnectionData, ref key, true);
        }

        public static RelayServerData GetRelayData(JoinAllocation allocation)
        {
            var endpoint = allocation.ServerEndpoints.FirstOrDefault(e => e.ConnectionType == "dtls");
            if (endpoint == null)
            {
                throw new System.Exception("DTLS endpoint not found");
            }

            var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);
            var allocationId = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
            var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
            var hostConnectionData = RelayConnectionData.FromByteArray(allocation.HostConnectionData);
            var key = RelayHMACKey.FromByteArray(allocation.Key);

            return new RelayServerData(ref serverEndpoint, 0, ref allocationId, ref connectionData, ref hostConnectionData, ref key, true);
        }
    }
}
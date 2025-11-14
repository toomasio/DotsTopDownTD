// File: RelayUtilities.cs
using System.Linq;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models;

namespace DotsTopDownTD.Network
{
    public struct HostRelayData
    {
        public RelayServerData ServerData;
        public RelayServerData ThinClientData;
    }

    public static class RelayUtilities
    {
        // For joining players
        public static RelayServerData GetRelayData(JoinAllocation allocation)
        {
            var endpoint = allocation.ServerEndpoints.First(e => e.ConnectionType == "dtls");
            var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);

            var allocationId = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
            var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
            var hostConnectionData = RelayConnectionData.FromByteArray(allocation.HostConnectionData);
            var key = RelayHMACKey.FromByteArray(allocation.Key);

            return new RelayServerData(
                ref serverEndpoint, 0,
                ref allocationId,
                ref connectionData,
                ref hostConnectionData,
                ref key,
                isSecure: true
            );
        }

        // For host (server + thin client)
        public static HostRelayData GetRelayDataForHost(Allocation allocation)
        {
            var endpoint = allocation.ServerEndpoints.First(e => e.ConnectionType == "dtls");
            var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);

            var allocationId = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
            var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
            var key = RelayHMACKey.FromByteArray(allocation.Key);

            // Server uses connectionData for both fields
            var serverData = new RelayServerData(
                ref serverEndpoint, 0,
                ref allocationId,
                ref connectionData,
                ref connectionData,
                ref key,
                isSecure: true
            );

            // Thin client mimics a joined player — uses same data
            var thinClientData = new RelayServerData(
                ref serverEndpoint, 0,
                ref allocationId,
                ref connectionData,
                ref connectionData,
                ref key,
                isSecure: true
            );

            return new HostRelayData
            {
                ServerData = serverData,
                ThinClientData = thinClientData
            };
        }
    }
}
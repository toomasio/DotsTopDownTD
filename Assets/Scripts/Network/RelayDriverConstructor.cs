using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;

namespace DotsTopDownTD.Network
{
    public class RelayDriverConstructor : INetworkStreamDriverConstructor
    {
        private RelayServerData serverData;
        private RelayServerData clientData;

        public RelayDriverConstructor(RelayServerData serverData, RelayServerData clientData)
        {
            this.serverData = serverData;
            this.clientData = clientData;
        }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var settings = DefaultDriverBuilder.GetNetworkSettings();
            settings.WithRelayParameters(ref clientData);
            DefaultDriverBuilder.RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var settings = DefaultDriverBuilder.GetNetworkSettings(); 
            settings.WithRelayParameters(ref serverData);
            DefaultDriverBuilder.RegisterServerUdpDriver(world, ref driverStore, netDebug, settings);
        }
    }
}
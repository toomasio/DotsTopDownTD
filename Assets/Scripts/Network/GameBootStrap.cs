using System;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Scripting;

namespace DotsTopDownTD.Network
{
    [Preserve]
    public class GameBootstrap : ClientServerBootstrap
    {
        public static event Action<string> OnRelayJoinCodeGenerated;
        public static event Action OnConnectionComplete;

        #region Public API - Call from UI

        public static async void StartHostRelay(int maxConnections = 4)
        {
            try
            {
                await InitializeUnityServices();

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // Built-in Unity extension — clean and official
                var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

                Debug.Log($"[RELAY HOST] Join Code: <color=cyan><b>{joinCode}</b></color>");
                OnRelayJoinCodeGenerated?.Invoke(joinCode);

                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var relayClientData = joinAllocation.ToRelayServerData("dtls");

                SetupRelayHostAndConnect(relayServerData, relayClientData);
                OnConnectionComplete.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Relay Host Failed] {e.Message}\n{e.StackTrace}");
            }
        }

        public static async void JoinRelay(string joinCode)
        {
            try
            {
                await InitializeUnityServices();

                Debug.Log($"[RELAY CLIENT] Joining with code: {joinCode}");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                // Built-in Unity extension
                var relayClientData = joinAllocation.ToRelayServerData("dtls");
                
                SetupRelayClient(relayClientData);
                OnConnectionComplete.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Relay Join Failed] {e.Message}");
            }
        }

        public static void StartHostDirect(string address = "0.0.0.0", ushort port = 7777)
        {
            var endpoint = NetworkEndpoint.Parse(address, port);
            SetupDirectHostAndConnect(endpoint);
            Debug.Log($"[DIRECT HOST] Listening on {endpoint}");
        }

        public static void JoinDirect(string ip, ushort port = 7777)
        {
            var endpoint = NetworkEndpoint.Parse(ip, port);
            SetupDirectClient(endpoint);
        }

        #endregion

        #region Private Setup

        private static void SetupRelayHostAndConnect(RelayServerData relayServerData, RelayServerData relayClientData)
        {
            DisposeAllWorlds();

            // SERVER: Bind to Relay
            var oldDriver = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayClientData);
            var serverWorld = CreateServerWorld("ServerWorld");

            var clientWorld = CreateClientWorld("ClientWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldDriver;

            //create server entity
            var serverEM = serverWorld.EntityManager;
            var serverEntity = serverEM.CreateEntity(typeof(NetworkStreamRequestListen));
            serverEM.SetComponentData(serverEntity, new NetworkStreamRequestListen
            {
                Endpoint = NetworkEndpoint.AnyIpv4
            });

            //create client entity
            var clientEM = clientWorld.EntityManager;
            var clientEntity = clientEM.CreateEntity(typeof(NetworkStreamRequestConnect));
            clientEM.SetComponentData(clientEntity, new NetworkStreamRequestConnect
            {
                Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(relayServerData.Endpoint.Port)
            });

            World.DefaultGameObjectInjectionWorld = serverWorld;
            WorldUtilities.RegisterServerWorld(serverWorld);
            WorldUtilities.RegisterClientWorld(clientWorld);

            Debug.Log("[RELAY HOST] Client/Host ready — Local player connected");
        }

        private static void SetupRelayClient(RelayServerData relayClientData)
        {
            DisposeAllWorlds();

            var oldDriver = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(new RelayServerData(), relayClientData);
            var clientWorld = CreateClientWorld("ClientWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldDriver;

            World.DefaultGameObjectInjectionWorld = clientWorld;
            WorldUtilities.RegisterClientWorld(clientWorld);

            var entity = clientWorld.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
            clientWorld.EntityManager.SetComponentData(entity, new NetworkStreamRequestConnect
            {
                Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(relayClientData.Endpoint.Port)
            });

            Debug.Log("[RELAY CLIENT] Connected");
            OnConnectionComplete.Invoke();
        }

        private static void SetupDirectHostAndConnect(NetworkEndpoint listenEndpoint)
        {
            DisposeAllWorlds();

            var serverWorld = CreateServerWorld("ServerWorld");
            World.DefaultGameObjectInjectionWorld = serverWorld;

            var serverEM = serverWorld.EntityManager;
            var listenEntity = serverEM.CreateEntity(typeof(NetworkStreamRequestListen));
            serverEM.SetComponentData(listenEntity, new NetworkStreamRequestListen { Endpoint = listenEndpoint });

            var clientWorld = CreateClientWorld("ClientWorld");

            var clientEM = clientWorld.EntityManager;
            var connectEntity = clientEM.CreateEntity(typeof(NetworkStreamRequestConnect));
            clientEM.SetComponentData(connectEntity, new NetworkStreamRequestConnect
            {
                Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(listenEndpoint.Port)
            });

            WorldUtilities.RegisterServerWorld(serverWorld);
            WorldUtilities.RegisterClientWorld(clientWorld);

            OnConnectionComplete.Invoke();
        }

        private static void SetupDirectClient(NetworkEndpoint connectEndpoint)
        {
            DisposeAllWorlds();

            var clientWorld = CreateClientWorld("ClientWorld");
            World.DefaultGameObjectInjectionWorld = clientWorld;

            var em = clientWorld.EntityManager;
            var connectEntity = em.CreateEntity(typeof(NetworkStreamRequestConnect));
            em.SetComponentData(connectEntity, new NetworkStreamRequestConnect { Endpoint = connectEndpoint });

            WorldUtilities.RegisterClientWorld(clientWorld);

            OnConnectionComplete.Invoke();
        }

        private static void DisposeAllWorlds()
        {
            World.DisposeAllWorlds();
            World.DefaultGameObjectInjectionWorld = null;
        }

        private static async Task InitializeUnityServices()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        #endregion
    }
}
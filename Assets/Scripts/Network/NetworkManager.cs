using System;
using System.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace DotsTopDownTD.Network
{
    public class ConnectionManager : MonoBehaviour
    {
        public static Action OnConnectionComplete;
        private static ConnectionManager Instance { get; set; }

        private void Awake() => Instance = this;

        // Relay Hosting: Creates allocation, generates join code, sets up host (server + thin client)
        public void StartHostRelay(int maxConnections = 4)
        {
            StartCoroutine(InitializeHostRelay(maxConnections));
        }

        private static IEnumerator InitializeHostRelay(int maxConnections)
        {
            yield return InitializeServicesAndAuth();

            var allocationTask = RelayService.Instance.CreateAllocationAsync(maxConnections);
            yield return new WaitUntil(() => allocationTask.IsCompleted);

            if (allocationTask.IsFaulted)
            {
                Debug.LogError("CreateAllocation failed: " + allocationTask.Exception);
                yield break;
            }

            var joinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocationTask.Result.AllocationId);
            yield return new WaitUntil(() => joinCodeTask.IsCompleted);

            if (joinCodeTask.IsFaulted)
            {
                Debug.LogError("GetJoinCode failed: " + joinCodeTask.Exception);
                yield break;
            }

            string joinCode = joinCodeTask.Result;
            Debug.Log("Join Code: " + joinCode);  // Display this in UI later

            RelayServerData relayServerData = RelayUtilities.GetRelayData(allocationTask.Result);

            // Setup host with Relay
            SetupRelayHostAndConnect(relayServerData);
        }

        // Relay Joining: Joins using code, sets up client
        public void JoinRelay(string joinCode)
        {
            StartCoroutine(JoinRelayCoroutine(joinCode));
        }

        private static IEnumerator JoinRelayCoroutine(string joinCode)
        {
            yield return InitializeServicesAndAuth();

            var joinTask = RelayService.Instance.JoinAllocationAsync(joinCode);
            yield return new WaitUntil(() => joinTask.IsCompleted);

            if (joinTask.IsFaulted)
            {
                Debug.LogError("JoinAllocation failed: " + joinTask.Exception);
                yield break;
            }

            RelayServerData relayClientData = RelayUtilities.GetRelayData(joinTask.Result);

            // Setup client with Relay
            ConnectToRelayServer(relayClientData);
        }

        // Direct IP Hosting: Listens on IP/port
        public void StartHostDirect(string listenAddress = "0.0.0.0", ushort port = 7777)
        {
            var endpoint = NetworkEndpoint.Parse(listenAddress, port);
            SetupDirectHostAndConnect(endpoint);
        }

        // Direct IP Joining: Connects to IP/port
        public void JoinDirect(string ip, ushort port = 7777)
        {
            var endpoint = NetworkEndpoint.Parse(ip, port);
            SetupDirectClientAndConnect(endpoint);
        }

        private static IEnumerator InitializeServicesAndAuth()
        {
            var initTask = UnityServices.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);

            if (initTask.IsFaulted)
            {
                Debug.LogError("UnityServices.Initialize failed: " + initTask.Exception);
                yield break;
            }

            if (AuthenticationService.Instance.IsSignedIn) yield break;

            var signInTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
            yield return new WaitUntil(() => signInTask.IsCompleted);

            if (signInTask.IsFaulted)
            {
                Debug.LogError("SignInAnonymously failed: " + signInTask.Exception);
            }
        }

        private static void SetupRelayHostAndConnect(RelayServerData relayServerData)
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                Debug.LogError("Host mode requires PlayType.ClientAndServer");
                return;
            }

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(relayServerData, relayServerData);  // Host uses same data for server/thin client

            var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            WorldUtilities.DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld = serverWorld;  // Set server as default for injection

            // Server listen entity
            var em = serverWorld.EntityManager;
            var listenEntity = em.CreateEntity(typeof(NetworkStreamRequestListen));
            em.SetComponentData(listenEntity, new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4 });

            // Client connect entity (thin client for host)
            em = clientWorld.EntityManager;
            var connectEntity = em.CreateEntity(typeof(NetworkStreamRequestConnect));
            em.SetComponentData(connectEntity, new NetworkStreamRequestConnect { Endpoint = relayServerData.Endpoint });

            WorldUtilities.RegisterServerWorld(serverWorld);
            WorldUtilities.RegisterClientWorld(clientWorld);

            OnConnectionComplete?.Invoke();
        }

        private static void ConnectToRelayServer(RelayServerData relayClientData)
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.Client)
            {
                Debug.LogError("Client mode requires PlayType.Client");
                return;
            }

            var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(new RelayServerData(), relayClientData);  // Empty server data for pure client

            var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;

            WorldUtilities.DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld = clientWorld;

            // Client connect entity
            var em = clientWorld.EntityManager;
            var connectEntity = em.CreateEntity(typeof(NetworkStreamRequestConnect));
            em.SetComponentData(connectEntity, new NetworkStreamRequestConnect { Endpoint = relayClientData.Endpoint });

            WorldUtilities.RegisterClientWorld(clientWorld);

            OnConnectionComplete?.Invoke();
        }

        private static void SetupDirectHostAndConnect(NetworkEndpoint listenEndpoint)
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                Debug.LogError("Host mode requires PlayType.ClientAndServer");
                return;
            }

            var serverWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            WorldUtilities.DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld = serverWorld;

            // Server listen
            var em = serverWorld.EntityManager;
            var listenEntity = em.CreateEntity(typeof(NetworkStreamRequestListen));
            em.SetComponentData(listenEntity, new NetworkStreamRequestListen { Endpoint = listenEndpoint });

            // Thin client connect to localhost
            em = clientWorld.EntityManager;
            var connectEntity = em.CreateEntity(typeof(NetworkStreamRequestConnect));
            em.SetComponentData(connectEntity, new NetworkStreamRequestConnect { Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(listenEndpoint.Port) });

            WorldUtilities.RegisterServerWorld(serverWorld);
            WorldUtilities.RegisterClientWorld(clientWorld);

            OnConnectionComplete?.Invoke();
        }

        private static void SetupDirectClientAndConnect(NetworkEndpoint connectEndpoint)
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.Client)
            {
                Debug.LogError("Client mode requires PlayType.Client");
                return;
            }

            var clientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            WorldUtilities.DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld = clientWorld;

            // Client connect
            var em = clientWorld.EntityManager;
            var connectEntity = em.CreateEntity(typeof(NetworkStreamRequestConnect));
            em.SetComponentData(connectEntity, new NetworkStreamRequestConnect { Endpoint = connectEndpoint });

            WorldUtilities.RegisterClientWorld(clientWorld);

            OnConnectionComplete?.Invoke();
        }
    }
}
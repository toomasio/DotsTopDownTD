using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DotsTopDownTD.Network
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct GoInGameServerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GoInGameRequest>();
            state.RequireForUpdate<ReceiveRpcCommandRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var networkIdLookup = SystemAPI.GetComponentLookup<NetworkId>(true);

            var job = new ProcessGoInGameServerJob
            {
                ECB = ecb,
                NetworkIdLookup = networkIdLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ProcessGoInGameServerJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<NetworkId> NetworkIdLookup;

            public void Execute(
                Entity rpcEntity,
                in GoInGameRequest request,
                in ReceiveRpcCommandRequest receiveRpc,
                [EntityIndexInQuery] int sortKey)
            {
                var connection = receiveRpc.SourceConnection;

                // Mark connection as InGame
                ECB.AddComponent<NetworkStreamInGame>(sortKey, connection);

                // Optional: Log
                if (NetworkIdLookup.HasComponent(connection))
                {
                    var netId = NetworkIdLookup[connection].Value;
                    Debug.Log($"Server: Connection {netId} is now InGame");
                }

                // Destroy RPC
                ECB.DestroyEntity(sortKey, rpcEntity);
            }
        }
    }
}

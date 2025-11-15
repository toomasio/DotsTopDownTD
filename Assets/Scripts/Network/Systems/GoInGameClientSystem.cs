// File: GoInGameSystems.cs
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace DotsTopDownTD.Network
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct GoInGameClientSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var job = new SendGoInGameJob
            {
                ECB = ecb.AsParallelWriter(),
                NetworkStreamInGameLookup = SystemAPI.GetComponentLookup<NetworkStreamInGame>(true)
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(NetworkId))]
        [WithNone(typeof(NetworkStreamInGame))]
        public partial struct SendGoInGameJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<NetworkStreamInGame> NetworkStreamInGameLookup;

            public void Execute(Entity connectionEntity, in NetworkId networkId, [EntityIndexInQuery] int entityIndexInQuery)
            {
                // Mark connection as InGame
                ECB.AddComponent<NetworkStreamInGame>(entityIndexInQuery, connectionEntity);

                // Create and send RPC
                var rpcEntity = ECB.CreateEntity(entityIndexInQuery);
                ECB.AddComponent<GoInGameRequest>(entityIndexInQuery, rpcEntity);
                ECB.AddComponent(entityIndexInQuery, rpcEntity, new SendRpcCommandRequest
                {
                    TargetConnection = connectionEntity
                });
            }
        }
    }
}

using System.Collections.Generic;
using Unity.Entities;

namespace DotsTopDownTD.Network
{
    public static class WorldUtilities
    {
        private static World _clientWorld;
        private static World _serverWorld;

        public static void DestroyLocalSimulationWorld()
        {
            foreach (var world in new List<World>(World.All))
            {
                if (world.Flags == WorldFlags.Game)
                {
                    world.Dispose();
                }
            }
        }

        public static void RegisterServerWorld(World world) => _serverWorld = world;
        public static void RegisterClientWorld(World world) => _clientWorld = world;

        public static World GetServerWorld() => _serverWorld;
        public static World GetClientWorld() => _clientWorld;
    }
}
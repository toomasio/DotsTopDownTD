using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;

namespace DotsTopDownTD.Network
{
    public static class WorldUtilities
    {
        private static World _clientWorld;
        private static World _serverWorld;

        public static void DestroyAllWorlds()
        {
            World.DisposeAllWorlds();
        }

        public static void RegisterServerWorld(World world) => _serverWorld = world;
        public static void RegisterClientWorld(World world) => _clientWorld = world;

        public static World GetServerWorld() => _serverWorld;
        public static World GetClientWorld() => _clientWorld;
    }
}
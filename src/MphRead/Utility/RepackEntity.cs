using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Utility
{
    public static partial class Repack
    {
        public static void TestEntities()
        {
            foreach (RoomMetadata meta in Metadata.RoomMetadata.Values)
            {
                if (meta.FirstHunt) // hybrid uses MPH entities
                {
                    RepackFhEntities();
                }
                else
                {
                    RepackEntities();
                } 
            }
        }

        private static void RepackEntities()
        {

        }

        private static void RepackFhEntities()
        {

        }
    }
}

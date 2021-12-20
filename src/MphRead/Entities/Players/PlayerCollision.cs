using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        // todo: visualize everything
        private void CheckPlayerCollision()
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var other = (PlayerEntity)entity;
                if (!other.LoadFlags.TestFlag(LoadFlags.Active) || other._health == 0)
                {
                    continue;
                }
                if (Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    // todo: halfturret collision
                }
                if (other == this)
                {
                    continue;
                }
                // sktodo
            }
        }

        private void CheckCollision()
        {
        }
    }
}

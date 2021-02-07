using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Entities
{
    public class PlatformEntity : VisibleEntityBase
    {
        private readonly PlatformEntityData _data;

        public PlatformEntity(PlatformEntityData data) : base(NewEntityType.Platform)
        {
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                // mtodo: entity placeholders
            }
            else
            {
                NewModel model = Read.GetNewModel(meta.Name);
                _models.Add(model);
                _anyLighting = model.Materials.Any(m => m.Lighting != 0);
                // temporary
                if (meta.Name == "SamusShip")
                {
                    model.Animations.NodeGroupId = 1;
                }
                else if (meta.Name == "SyluxTurret")
                {
                    model.Animations.NodeGroupId = -1;
                }
            }
        }
    }
}

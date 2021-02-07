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

        public PlatformEntity(int id, PlatformEntityData data) : base(NewEntityType.Platform)
        {
            Id = id;
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
                ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
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

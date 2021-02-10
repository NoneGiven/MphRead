using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerEntity : VisibleEntityBase
    {
        public Hunter Hunter { get; }
        private readonly NewModel _bipedModel = null!;
        private readonly NewModel _altModel = null!;
        private readonly NewModel _gunModel = null!;
        private readonly NewModel _dblDmgModel;
        // todo: does this affect collision and stuff?
        private readonly Matrix4 _scaleMtx;

        // todo: main player, player slots, etc.
        private readonly bool _mainPlayer = false;
        private bool _altForm = false;
        private bool _doubleDamage = false;

        public PlayerEntity(Hunter hunter) : base(NewEntityType.Player)
        {
            Hunter = hunter;
            // todo: lod1
            IReadOnlyList<string> models = Metadata.HunterModels[Hunter];
            Debug.Assert(models.Count == 3);
            for (int i = 0; i < models.Count; i++)
            {
                NewModel model = Read.GetNewModel(models[i]);
                _models.Add(model);
                if (i == 0)
                {
                    _bipedModel = model;
                }
                else if (i == 1)
                {
                    _altModel = model;
                }
                else if (i == 2)
                {
                    _gunModel = model;
                }
            }
            _dblDmgModel = Read.GetNewModel("doubleDamage_img");
            _scaleMtx = Matrix4.CreateScale(Metadata.HunterScales[Hunter]);
        }

        protected override bool GetModelActive(NewModel model, int index)
        {
            return (_altForm && model == _altModel) || (_mainPlayer && model == _gunModel) || (!_mainPlayer && model == _bipedModel);
        }

        protected override Material GetMaterial(NewModel model, int materialId)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(model == _gunModel && materialId == 0)))
            {
                return _dblDmgModel.Materials[0];
            }
            return base.GetMaterial(model, materialId);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            Matrix4 transform = base.GetModelTransform(model, index);
            if (model == _bipedModel)
            {
                transform = _scaleMtx * transform;
            }
            return transform;
        }
    }
}

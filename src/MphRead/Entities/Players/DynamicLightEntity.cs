using System;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class DynamicLightEntityBase : EntityBase
    {
        protected Vector3 _light1Vector;
        protected Vector3 _light1Color;
        protected Vector3 _light2Vector;
        protected Vector3 _light2Color;

        public Vector3 Light1Vector => _light1Vector;
        public Vector3 Light1Color => _light1Color;
        public Vector3 Light2Vector => _light2Vector;
        public Vector3 Light2Color => _light2Color;

        protected bool _useRoomLights = false;

        public DynamicLightEntityBase(EntityType type, Scene scene) : base(type, scene)
        {
        }

        private const float _colorStep = 8 / 255f;

        // todo?: FPS stuff
        protected void UpdateLightSources(Vector3 position)
        {
            static float UpdateChannel(float current, float source, float frames)
            {
                float diff = source - current;
                if (MathF.Abs(diff) < _colorStep)
                {
                    return source;
                }
                int factor;
                if (current > source)
                {
                    factor = (int)MathF.Truncate((diff + _colorStep) / (8 * _colorStep));
                    if (factor <= -1)
                    {
                        return current + (factor - 1) * _colorStep * frames;
                    }
                    return current - _colorStep * frames;
                }
                factor = (int)MathF.Truncate(diff / (8 * _colorStep));
                if (factor >= 1)
                {
                    return current + factor * _colorStep * frames;
                }
                return current + _colorStep * frames;
            }
            bool hasLight1 = false;
            bool hasLight2 = false;
            Vector3 light1Color = _light1Color;
            Vector3 light1Vector = _light1Vector;
            Vector3 light2Color = _light2Color;
            Vector3 light2Vector = _light2Vector;
            float frames = _scene.FrameTime * 30;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.LightSource)
                {
                    continue;
                }
                var lightSource = (LightSourceEntity)entity;
                if (lightSource.Volume.TestPoint(position))
                {
                    if (lightSource.Light1Enabled)
                    {
                        hasLight1 = true;
                        light1Vector.X += (lightSource.Light1Vector.X - light1Vector.X) / 8f * frames;
                        light1Vector.Y += (lightSource.Light1Vector.Y - light1Vector.Y) / 8f * frames;
                        light1Vector.Z += (lightSource.Light1Vector.Z - light1Vector.Z) / 8f * frames;
                        light1Color.X = UpdateChannel(light1Color.X, lightSource.Light1Color.X, frames);
                        light1Color.Y = UpdateChannel(light1Color.Y, lightSource.Light1Color.Y, frames);
                        light1Color.Z = UpdateChannel(light1Color.Z, lightSource.Light1Color.Z, frames);
                    }
                    if (lightSource.Light2Enabled)
                    {
                        hasLight2 = true;
                        light2Vector.X += (lightSource.Light2Vector.X - light2Vector.X) / 8f * frames;
                        light2Vector.Y += (lightSource.Light2Vector.Y - light2Vector.Y) / 8f * frames;
                        light2Vector.Z += (lightSource.Light2Vector.Z - light2Vector.Z) / 8f * frames;
                        light2Color.X = UpdateChannel(light2Color.X, lightSource.Light2Color.X, frames);
                        light2Color.Y = UpdateChannel(light2Color.Y, lightSource.Light2Color.Y, frames);
                        light2Color.Z = UpdateChannel(light2Color.Z, lightSource.Light2Color.Z, frames);
                    }
                }
            }
            if (!hasLight1)
            {
                light1Vector.X += (_scene.Light1Vector.X - light1Vector.X) / 8f * frames;
                light1Vector.Y += (_scene.Light1Vector.Y - light1Vector.Y) / 8f * frames;
                light1Vector.Z += (_scene.Light1Vector.Z - light1Vector.Z) / 8f * frames;
                light1Color.X = UpdateChannel(light1Color.X, _scene.Light1Color.X, frames);
                light1Color.Y = UpdateChannel(light1Color.Y, _scene.Light1Color.Y, frames);
                light1Color.Z = UpdateChannel(light1Color.Z, _scene.Light1Color.Z, frames);
            }
            if (!hasLight2)
            {
                light2Vector.X += (_scene.Light2Vector.X - light2Vector.X) / 8f * frames;
                light2Vector.Y += (_scene.Light2Vector.Y - light2Vector.Y) / 8f * frames;
                light2Vector.Z += (_scene.Light2Vector.Z - light2Vector.Z) / 8f * frames;
                light2Color.X = UpdateChannel(light2Color.X, _scene.Light2Color.X, frames);
                light2Color.Y = UpdateChannel(light2Color.Y, _scene.Light2Color.Y, frames);
                light2Color.Z = UpdateChannel(light2Color.Z, _scene.Light2Color.Z, frames);
            }
            _light1Color = light1Color;
            _light1Vector = light1Vector.Normalized();
            _light2Color = light2Color;
            _light2Vector = light2Vector.Normalized();
        }

        protected override LightInfo GetLightInfo()
        {
            if (_useRoomLights)
            {
                return base.GetLightInfo();
            }
            return new LightInfo(_light1Vector, _light1Color, _light2Vector, _light2Color);
        }
    }
}

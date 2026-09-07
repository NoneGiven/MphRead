using System;
using System.Collections.Generic;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Mods.Render
{
    /// <summary>
    /// The hunter you are about to come back as, turning on the spot, in the
    /// suit you picked -- the real model, not a picture of one.
    ///
    /// The results screen asked this question with the game's 32x32 portrait
    /// sprite first, and the portrait cannot answer half of it: there is one
    /// per hunter and it does not know what a suit is, so the four colours
    /// beside it were the only thing saying what you had chosen. A model does
    /// know: <c>Recolor</c> is the same palette index the player wears, so
    /// what is drawn here is exactly what everybody else is about to see.
    ///
    /// <para>
    /// **It is an entity that is never in the scene.** It is built like one --
    /// <see cref="EntityBase"/> is where the node walk, the material update
    /// and the render-item building live, and reimplementing those for one
    /// preview would be a second renderer -- but it is never inserted, so it
    /// takes no slot, runs no Process, holds no collision, is never asked for
    /// a NodeRef and cannot outlive a room change. That last one is not
    /// hypothetical: an entity alive across a rotation is exactly the shape of
    /// bug that produced the crash this screen was being fixed for.
    /// </para>
    /// <para>
    /// Its items are drawn in a pass of their own, into a scissored corner of
    /// the frame with its own projection and a depth buffer cleared to itself
    /// (see <c>Scene.ModDrawPreview</c>). That is what makes it a preview
    /// window rather than a model standing in the room: nothing in the level
    /// can occlude it, and it cannot be lit by whatever the level happens to
    /// be lit by.
    /// </para>
    /// </summary>
    public class HunterPreviewEntity : EntityBase
    {
        /// <summary>
        /// Fixed lighting, not the room's.
        ///
        /// The room's lights are what make a level look like itself, and half
        /// the multiplayer maps would leave this pitch dark or bright orange.
        /// A key from the front left and a cool fill from the right is the
        /// lighting every character select in every game has, for the reason
        /// that it shows the shape.
        /// </summary>
        private static readonly LightInfo _light = new LightInfo(
            new Vector3(-0.35f, -0.30f, -0.89f), new Vector3(1f, 0.97f, 0.92f),
            new Vector3(0.60f, 0.20f, 0.77f), new Vector3(0.32f, 0.36f, 0.48f));

        /// <summary>Half a turn: these models are authored facing away.</summary>
        private static readonly Matrix4 _facing =
            Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180));

        private Hunter _hunter = Hunter.Random;
        private int _recolor = -1;
        private ModelInstance? _model;

        public HunterPreviewEntity(Scene scene) : base(EntityType.Model, scene)
        {
        }

        public bool Ready => _model != null;

        /// <summary>
        /// Point it at a hunter and a suit. Cheap to call every frame: only a
        /// change loads anything, and <c>Read</c> caches models globally, so a
        /// hunter somebody is already playing costs nothing at all.
        /// </summary>
        public void SetUp(Hunter hunter, int recolor)
        {
            if (_model != null && hunter == _hunter && recolor == _recolor)
            {
                return;
            }
            if (hunter != _hunter || _model == null)
            {
                try
                {
                    if (!Metadata.HunterModels.TryGetValue(hunter, out IReadOnlyList<string>? models)
                        || models.Count == 0)
                    {
                        return;
                    }
                    // The first LOD, which is the one the game draws for a
                    // player you are standing next to.
                    ModelInstance inst = Read.GetModelInstance(models[0]);
                    _models.Clear();
                    _models.Add(inst);
                    _model = inst;
                    _hunter = hunter;
                    // Standing, not the bind pose.
                    //
                    // A model with no animation set draws the skeleton as it
                    // was authored, which for these is arms straight out --
                    // the T-pose, and not a picture of anybody. Idle is the
                    // animation the game plays for a hunter standing still,
                    // so it is both the right pose and the one everybody
                    // recognises the character in.
                    inst.SetAnimation((int)PlayerAnimation.Idle);
                }
                catch (Exception ex)
                {
                    // A preview is not worth a match. The panel falls back to
                    // the portrait sprite when this never becomes ready.
                    Console.WriteLine($"[endscreen] no model for {hunter}: {ex.Message}");
                    _model = null;
                    return;
                }
            }
            _recolor = recolor;
            Recolor = recolor;
        }

        /// <summary>
        /// Advance the idle animation, once a simulation step.
        ///
        /// Called from the step and never from a draw, for the reason every
        /// other timer in this program is: a picture with no step behind it
        /// must not advance anything, or the hunter breathes at the frame
        /// rate. The instance is this preview's own -- <c>Read</c> caches the
        /// <c>Model</c>, not the <c>ModelInstance</c> -- so nothing a player
        /// is doing is touched by this.
        /// </summary>
        public void Step()
        {
            _model?.UpdateAnimFrames();
        }

        public void Reset()
        {
        }

        protected override LightInfo GetLightInfo()
        {
            return _light;
        }

        /// <summary>
        /// Build this frame's render items. The caller has already told the
        /// scene to collect them into the preview list rather than the world's.
        ///
        /// The pose comes from the idle animation set in <see cref="SetUp"/>
        /// and advanced in <see cref="Step"/>; nothing here touches it, so a
        /// frame drawn twice draws the same pose twice.
        /// </summary>
        public override void GetDrawInfo()
        {
            if (_model == null)
            {
                return;
            }
            // Turned to face the camera, and still.
            //
            // A hunter model is authored facing -Z, which is the direction a
            // player walks in, and the preview camera stands at +Z looking
            // back at the origin -- so the identity transform shows the back
            // of their head. Half a turn puts the face, the arm cannon and the
            // chest, which is the half of a hunter anybody recognises, towards
            // whoever is choosing.
            //
            // Still, because it turned on the spot first and that is the wrong
            // thing for a picker: the answer to "what does this suit look
            // like" should be the same every time you glance at it, not
            // whichever side happens to be towards you.
            UpdateTransforms(_model, _facing, _recolor < 0 ? 0 : _recolor);
            GetDrawItems(_model, 0, _light);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using MphRead.Entities;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead
{
    public static class Selection
    {
        public static EntityBase? Entity { get; private set; }
        public static ModelInstance? Instance { get; private set; }
        public static Node? Node { get; private set; }
        public static Mesh? Mesh { get; private set; }

        private static bool Any => Mesh != null || Node != null || Instance != null || Entity != null;

        private static bool _showSelection = true;
        private static bool _hideUnselectedVolumes = false;

        public static bool CheckVolume(EntityBase entity)
        {
            return !_hideUnselectedVolumes || Entity == null || entity == Entity;
        }

        public static void Clear()
        {
            Mesh = null;
            Node = null;
            Instance = null;
            Entity = null;
        }

        public static SelectionType CheckSelection(EntityBase entity, ModelInstance inst, Node node, Mesh mesh)
        {
            if (Mesh != null)
            {
                if (mesh == Mesh && node == Node && inst == Instance && entity == Entity)
                {
                    return SelectionType.Selected;
                }
            }
            else if (Node != null)
            {
                if (node == Node && inst == Instance && entity == Entity)
                {
                    return SelectionType.Selected;
                }
            }
            else if (Instance != null)
            {
                if (inst == Instance && entity == Entity)
                {
                    return SelectionType.Selected;
                }
            }
            else if (Entity != null)
            {
                if (entity == Entity)
                {
                    return SelectionType.Selected;
                }
            }
            if (Entity != null)
            {
                if (Entity.GetParent() == entity)
                {
                    return SelectionType.Parent;
                }
                if (Entity.GetChild() == entity)
                {
                    return SelectionType.Child;
                }
            }
            return SelectionType.None;
        }

        public static void ToggleShowSelection()
        {
            _showSelection = !_showSelection;
        }

        public static void ToggleUnselectedVolumes()
        {
            _hideUnselectedVolumes = !_hideUnselectedVolumes;
        }

        private static readonly Vector3 _entityColor = Vector3.One; // white
        private static readonly Vector3 _modelColor = new Vector3(255, 255, 200) / 255f; // yellow
        private static readonly Vector3 _nodeColor = new Vector3(200, 255, 200) / 255f; // green
        private static readonly Vector3 _meshColor = new Vector3(255, 200, 255) / 255f; // pink
        private static readonly Vector3 _parentColor = Vector3.UnitX; // red
        private static readonly Vector3 _childColor = Vector3.UnitZ; // blue

        public static Vector4? GetSelectionColor(SelectionType type)
        {
            static float GetFactor()
            {
                long ms = Environment.TickCount64;
                float percentage = (ms % 1000) / 1000f;
                if (ms / 1000 % 10 % 2 == 0)
                {
                    percentage = 1 - percentage;
                }
                return percentage;
            }
            if (_showSelection)
            {
                if (type == SelectionType.Selected)
                {
                    float factor = GetFactor();
                    if (Mesh != null)
                    {
                        return new Vector4(_meshColor * factor, 1);
                    }
                    if (Node != null)
                    {
                        return new Vector4(_nodeColor * factor, 1);
                    }
                    if (Instance != null)
                    {
                        return new Vector4(_modelColor * factor, 1);
                    }
                    return new Vector4(_entityColor * factor, 1);
                }
                if (type == SelectionType.Parent)
                {
                    float factor = GetFactor();
                    return new Vector4(_parentColor * factor, 1);
                }
                if (type == SelectionType.Child)
                {
                    float factor = GetFactor();
                    return new Vector4(_childColor * factor, 1);
                }
            }
            return null;
        }

        public static bool OnKeyDown(KeyboardKeyEventArgs e, Scene scene)
        {
            if (e.Key == Keys.M)
            {
                UpdateSelection(e.Control, e.Shift, scene);
                return true;
            }
            if (!Any)
            {
                return false;
            }
            if (e.Key == Keys.Equal || e.Key == Keys.KeyPadEqual)
            {
                if (e.Alt)
                {
                    NextAnimation(e.Control);
                }
                else
                {
                    SelectNext(scene, e.Control);
                }
                return true;
            }
            if (e.Key == Keys.Minus || e.Key == Keys.KeyPadSubtract)
            {
                if (e.Alt)
                {
                    PrevAnimation(e.Control);
                }
                else
                {
                    SelectPrev(scene, e.Control);
                }
                return true;
            }
            if (e.Key == Keys.X && scene.AllowCameraMovement)
            {
                LookAtSelection(scene, e.Control, e.Shift);
                return true;
            }
            // ctodo: allow toggling collision entries and individual planes
            if (e.Key == Keys.D0 || e.Key == Keys.KeyPad0)
            {
                // note: toggling meshes and nodes will affect all model instances that use them
                if (Mesh != null)
                {
                    Mesh.Visible = !Mesh.Visible;
                }
                else if (Node != null)
                {
                    Node.Enabled = !Node.Enabled;
                }
                else if (Instance != null)
                {
                    Instance.Active = !Instance.Active;
                }
                else if (Entity != null)
                {
                    if (e.Control)
                    {
                        Entity.SetActive(!e.Shift);
                    }
                    else
                    {
                        Entity.Hidden = !Entity.Hidden;
                    }
                }
                return true;
            }
            if (e.Key == Keys.D1 || e.Key == Keys.KeyPad1)
            {
                PrevRecolor();
                return true;
            }
            if (e.Key == Keys.D2 || e.Key == Keys.KeyPad2)
            {
                NextRecolor();
                return true;
            }
            return false;
        }

        private const float _twoPi = MathF.PI * 2;

        public static void OnKeyHeld(KeyboardState keyboardState)
        {
            if (Entity != null && Entity.Type != EntityType.Room)
            {
                float step = 0.1f;
                Vector3 position = Entity.Position;
                if (keyboardState.IsKeyDown(Keys.W)) // move Z-
                {
                    position = position.AddZ(-step);
                }
                else if (keyboardState.IsKeyDown(Keys.S)) // move Z+
                {
                    position = position.AddZ(step);
                }
                if (keyboardState.IsKeyDown(Keys.Space)) // move Y+
                {
                    position = position.AddY(step);
                }
                else if (keyboardState.IsKeyDown(Keys.V)) // move Y-
                {
                    position = position.AddY(-step);
                }
                if (keyboardState.IsKeyDown(Keys.A)) // move X-
                {
                    position = position.AddX(-step);
                }
                else if (keyboardState.IsKeyDown(Keys.D)) // move X+
                {
                    position = position.AddX(step);
                }
                // todo: some transforms (sniper targets in UNIT4_RM2) aren't consistent when first changing the rotation
                step = 0.0436332f; // 2.5 degrees
                Vector3 rotation = Entity.Rotation;
                if (keyboardState.IsKeyDown(Keys.Up)) // rotate up
                {
                    rotation.X += step;
                }
                else if (keyboardState.IsKeyDown(Keys.Down)) // rotate down
                {
                    rotation.X -= step;
                }
                if (keyboardState.IsKeyDown(Keys.Left)) // rotate left
                {
                    rotation.Y += step;
                }
                else if (keyboardState.IsKeyDown(Keys.Right)) // rotate right
                {
                    rotation.Y -= step;
                }
                while (rotation.X < 0)
                {
                    rotation.X += _twoPi;
                }
                while (rotation.X > _twoPi)
                {
                    rotation.X -= _twoPi;
                }
                while (rotation.Y < 0)
                {
                    rotation.Y += _twoPi;
                }
                while (rotation.Y > _twoPi)
                {
                    rotation.Y -= _twoPi;
                }
                while (rotation.Z < 0)
                {
                    rotation.Z += _twoPi;
                }
                while (rotation.Z > _twoPi)
                {
                    rotation.Z -= _twoPi;
                }
                Entity.Position = position;
                if (Entity.Type == EntityType.Player)
                {
                    var player = (PlayerEntity)Entity;
                    player.DebugInput(keyboardState);
                }
                else
                {
                    Entity.Rotation = rotation;
                }
            }
        }

        private static void UpdateSelection(bool control, bool shift, Scene scene)
        {
            if (control && shift)
            {
                Entity = null;
                Instance = null;
                Node = null;
                Mesh = null;
            }
            else if (Mesh != null)
            {
                if (shift)
                {
                    Mesh = null;
                }
                else
                {
                    Entity = null;
                    Instance = null;
                    Node = null;
                    Mesh = null;
                }
            }
            else if (Node != null)
            {
                if (shift)
                {
                    Node = null;
                }
                else if (Instance != null && Node.MeshCount > 0)
                {
                    Mesh = Instance.Model.Meshes[Node.MeshId / 2];
                }
            }
            else if (Instance != null)
            {
                if (shift)
                {
                    Instance = null;
                }
                else
                {
                    Node = Instance.Model.Nodes.FirstOrDefault();
                }
            }
            else if (Entity != null)
            {
                if (shift)
                {
                    Entity = null;
                }
                else if (Entity.GetModels().Any(m => !m.IsPlaceholder))
                {
                    Instance = Entity.GetModels().FirstOrDefault();
                }
            }
            else if (!shift)
            {
                Entity = scene.Entities.FirstOrDefault();
            }
        }

        // todo: select other animation types, and enable playing in reverse
        private static void NextAnimation(bool control)
        {
            ModelInstance? inst = null;
            if (Instance != null)
            {
                inst = Instance;
            }
            else if (Entity != null)
            {
                inst = Entity.GetModels().FirstOrDefault();
            }
            if (inst != null)
            {
                if (control)
                {
                    int index = inst.AnimInfo.MaterialIndex + 1;
                    do
                    {
                        inst.SetMaterialAnim(index);
                        index++;
                    }
                    while (inst.AnimInfo.MaterialIndex != -1 && inst.AnimInfo.Material.Group?.Count == 0);
                }
                else
                {
                    int index = inst.AnimInfo.NodeIndex + 1;
                    do
                    {
                        inst.SetNodeAnim(index);
                        index++;
                    }
                    while (inst.AnimInfo.NodeIndex != -1 && inst.AnimInfo.Node.Group?.Count == 0);
                }
            }
        }

        private static void PrevAnimation(bool control)
        {
            ModelInstance? inst = null;
            if (Instance != null)
            {
                inst = Instance;
            }
            else if (Entity != null)
            {
                inst = Entity.GetModels().FirstOrDefault();
            }
            if (inst != null)
            {
                if (control)
                {
                    int index = inst.AnimInfo.MaterialIndex - 1;
                    if (index < -1)
                    {
                        index = inst.Model.AnimationGroups.Material.Count - 1;
                    }
                    do
                    {
                        inst.SetMaterialAnim(index);
                        index--;
                    }
                    while (inst.AnimInfo.MaterialIndex != -1 && inst.AnimInfo.Material.Group?.Count == 0);
                }
                else
                {
                    int index = inst.AnimInfo.NodeIndex - 1;
                    if (index < -1)
                    {
                        index = inst.Model.AnimationGroups.Node.Count - 1;
                    }
                    do
                    {
                        inst.SetNodeAnim(index);
                        index--;
                    }
                    while (inst.AnimInfo.NodeIndex != -1 && inst.AnimInfo.Node.Group?.Count == 0);
                }
            }
        }

        private static void NextRecolor()
        {
            if (Entity != null)
            {
                ModelInstance? instance = Entity.GetModels().FirstOrDefault();
                if (instance != null)
                {
                    int recolor = Entity.Recolor + 1;
                    if (recolor >= instance.Model.Recolors.Count)
                    {
                        recolor = 0;
                    }
                    Entity.Recolor = recolor;
                }
            }
        }

        private static void PrevRecolor()
        {
            if (Entity != null)
            {
                ModelInstance? instance = Entity.GetModels().FirstOrDefault();
                if (instance != null)
                {
                    int recolor = Entity.Recolor - 1;
                    if (recolor < 0)
                    {
                        recolor = instance.Model.Recolors.Count - 1;
                    }
                    Entity.Recolor = recolor;
                }
            }
        }

        private static void SelectNext(Scene scene, bool control)
        {
            // todo: it would be nice to have node/mesh selection follow the hierarchy (with meshes "overflowing" to the next node)
            if (Mesh != null)
            {
                SelectMesh(1);
            }
            else if (Node != null)
            {
                SelectNode(1, control);
            }
            else if (Instance != null)
            {
                SelectInstance(1);
            }
            else if (Entity != null)
            {
                SelectEntity(1, scene);
            }
        }

        private static void SelectPrev(Scene scene, bool control)
        {
            if (Mesh != null)
            {
                SelectMesh(-1);
            }
            else if (Node != null)
            {
                SelectNode(-1, control);
            }
            else if (Instance != null)
            {
                SelectInstance(-1);
            }
            else if (Entity != null)
            {
                SelectEntity(-1, scene);
            }
        }

        private static readonly List<Mesh> _meshBuffer = new List<Mesh>();

        private static void SelectMesh(int direction)
        {
            if (Instance != null && Node != null)
            {
                Mesh? mesh = null;
                _meshBuffer.Clear();
                int start = Node.MeshId / 2;
                for (int i = 0; i < Node.MeshCount; i++)
                {
                    _meshBuffer.Add(Instance.Model.Meshes[start + i]);
                }
                int index = _meshBuffer.IndexOf(e => e == Mesh);
                while (mesh != Mesh)
                {
                    index += direction;
                    if (index < 0)
                    {
                        index = _meshBuffer.Count - 1;
                    }
                    else if (index >= _meshBuffer.Count)
                    {
                        index = 0;
                    }
                    mesh = _meshBuffer[index];
                    if (FilterMesh(/*mesh*/))
                    {
                        Mesh = mesh;
                        break;
                    }
                }
            }
        }

        private static bool FilterMesh(/*Mesh mesh*/)
        {
            return true;
        }

        private static void SelectNode(int direction, bool control)
        {
            if (Instance != null)
            {
                Node? node = null;
                IReadOnlyList<Node> nodes = Instance.Model.Nodes;
                int index = nodes.IndexOf(e => e == Node);
                while (node != Node)
                {
                    index += direction;
                    if (index < 0)
                    {
                        index = nodes.Count - 1;
                    }
                    else if (index >= nodes.Count)
                    {
                        index = 0;
                    }
                    node = nodes[index];
                    if (FilterNode(node, control))
                    {
                        Node = node;
                        break;
                    }
                }
            }
        }

        private static bool FilterNode(Node node, bool roomOnly)
        {
            return !roomOnly || node.IsRoomPartNode;
        }

        private static void SelectInstance(int direction)
        {
            if (Entity != null)
            {
                ModelInstance? inst = null;
                IReadOnlyList<ModelInstance> insts = Entity.GetModels();
                int index = insts.IndexOf(e => e == Instance);
                while (inst != Instance)
                {
                    index += direction;
                    if (index < 0)
                    {
                        index = insts.Count - 1;
                    }
                    else if (index >= insts.Count)
                    {
                        index = 0;
                    }
                    inst = insts[index];
                    if (FilterInstance(/*inst*/))
                    {
                        Instance = inst;
                        break;
                    }
                }
            }
        }

        private static bool FilterInstance(/*ModelInstance inst*/)
        {
            return true;
        }

        private static void SelectEntity(int direction, Scene scene)
        {
            EntityBase? entity = null;
            int index = scene.Entities.IndexOf(e => e == Entity);
            while (entity != Entity)
            {
                index += direction;
                if (index < 0)
                {
                    index = scene.Entities.Count - 1;
                }
                else if (index >= scene.Entities.Count)
                {
                    index = 0;
                }
                entity = scene.Entities[index];
                if (FilterEntity(entity, scene))
                {
                    Entity = entity;
                    break;
                }
            }
        }

        private static bool FilterEntity(EntityBase entity, Scene scene)
        {
            return entity.Type == EntityType.BeamEffect || entity.Type == EntityType.BeamProjectile ||
                entity.GetModels().Any(m => (scene.ShowAllEntities || m.Active)
                    && (scene.ShowAllEntities || scene.ShowInvisibleEntities || !m.IsPlaceholder));
        }

        private static void LookAtSelection(Scene scene, bool control, bool shift)
        {
            if (control)
            {
                EntityBase? target = shift ? Entity?.GetChild() : Entity?.GetParent();
                if (target != null)
                {
                    scene.LookAt(target.Position);
                }
            }
            else if (Node != null) // node or mesh
            {
                scene.LookAt(Node.Animation.Row3.Xyz);
            }
            else if (Entity != null) // entity or model instance
            {
                scene.LookAt(Entity.Position);
            }
        }
    }
}

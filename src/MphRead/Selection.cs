using System;
using System.Collections.Generic;
using System.Linq;
using MphRead.Entities;
using OpenTK.Graphics.OpenGL;
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

        public static bool IsSelected(EntityBase entity, ModelInstance inst, Node node, Mesh mesh)
        {
            if (Mesh != null)
            {
                return mesh == Mesh && node == Node && inst == Instance && entity == Entity;
            }
            if (Node != null)
            {
                return node == Node && inst == Instance && entity == Entity;
            }
            if (Instance != null)
            {
                return inst == Instance && entity == Entity;
            }
            if (Entity != null)
            {
                return entity == Entity;
            }
            return false;
        }

        public static void ToggleShowSelection()
        {
            _showSelection = !_showSelection;
        }

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
                    return new Vector4(factor, factor, factor, 1);
                }
                if (type == SelectionType.Parent)
                {
                    float factor = GetFactor();
                    return new Vector4(factor, 0, 0, 1);
                }
                if (type == SelectionType.Child)
                {
                    float factor = GetFactor();
                    return new Vector4(0, 0, factor, 1);
                }
            }
            return null;
        }

        public static bool OnKeyDown(KeyboardKeyEventArgs e, NewScene scene)
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
                // mtodo: cycle animations
                SelectNext(scene);
                return true;
            }
            if (e.Key == Keys.Minus || e.Key == Keys.KeyPadSubtract)
            {
                // mtodo: cycle animations
                SelectPrev(scene);
                return true;
            }
            if (e.Key == Keys.X)
            {
                LookAtSelection(scene);
                return true;
            }
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
                    Entity.SetActive(!Entity.Active);
                }
                return true;
            }
            if (e.Key == Keys.D1 || e.Key == Keys.KeyPad1)
            {
                // mtodo: cycle recolors
                return true;
            }
            if (e.Key == Keys.D2 || e.Key == Keys.KeyPad2)
            {
                // mtodo: cycle recolors
                return true;
            }
            return false;
        }

        public static void OnKeyHeld()
        {
            // mtodo: move selected entity
        }

        private static void UpdateSelection(bool control, bool shift, NewScene scene)
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
                else
                {
                    Instance = Entity.GetModels().FirstOrDefault();
                }
            }
            else if (!shift)
            {
                Entity = scene.Entities.FirstOrDefault();
            }
        }

        private static void SelectNext(NewScene scene)
        {
            if (Mesh != null)
            {
                SelectMesh(1);
            }
            else if (Node != null)
            {
                SelectNode(1);
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

        private static void SelectPrev(NewScene scene)
        {
            if (Mesh != null)
            {
                SelectMesh(-1);
            }
            else if (Node != null)
            {
                SelectNode(-1);
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

        private static void SelectNode(int direction)
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
                    if (FilterNode(/*node*/))
                    {
                        Node = node;
                        break;
                    }
                }
            }
        }

        private static bool FilterNode(/*Node node*/)
        {
            return true;
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

        private static void SelectEntity(int direction, NewScene scene)
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

        private static bool FilterEntity(EntityBase entity, NewScene scene)
        {
            // mtodo: filter options
            return scene.ShowInvisible || entity.GetModels().Any(m => !m.IsPlaceholder);
        }

        private static void LookAtSelection(NewScene scene)
        {
            if (Node != null) // node or mesh
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

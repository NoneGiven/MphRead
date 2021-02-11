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
        public static ModelInstance? Inst { get; private set; }
        public static Node? Node { get; private set; }
        public static Mesh? Mesh { get; private set; }

        private static bool Any => Mesh != null || Node != null || Inst != null || Entity != null;

        private static bool _showSelection = true;

        public static bool IsSelected(EntityBase entity, ModelInstance model, Node node, Mesh mesh)
        {
            if (Mesh != null)
            {
                return mesh == Mesh && node == Node && model == Inst && entity == Entity;
            }
            if (Node != null)
            {
                return node == Node && model == Inst && entity == Entity;
            }
            if (Inst != null)
            {
                return model == Inst && entity == Entity;
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
                else if (Inst != null)
                {
                    Inst.Active = !Inst.Active;
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
                Inst = null;
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
                    Inst = null;
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
                else if (Inst != null && Node.MeshCount > 0)
                {
                    Mesh = Inst.Model.Meshes[Node.MeshId / 2];
                }
            }
            else if (Inst != null)
            {
                if (shift)
                {
                    Inst = null;
                }
                else
                {
                    Node = Inst.Model.Nodes.FirstOrDefault();
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
                    Inst = Entity.GetModels().FirstOrDefault();
                }
            }
            else if (!shift)
            {
                Entity = scene.Entities.FirstOrDefault();
            }
        }

        private static List<EntityBase> _buffer = new List<EntityBase>();

        private static void SelectNext(NewScene scene)
        {
            if (Mesh != null)
            {

            }
            else if (Node != null)
            {

            }
            else if (Inst != null)
            {

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

            }
            else if (Node != null)
            {

            }
            else if (Inst != null)
            {

            }
            else if (Entity != null)
            {
                SelectEntity(-1, scene);
            }
        }

        private static void SelectEntity(int direction, NewScene scene)
        {
            _buffer.Clear();
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
            if (Node != null) // mesh or node
            {
                scene.LookAt(Node.Animation.Row3.Xyz);
            }
            else if (Entity != null) // model or entity
            {
                scene.LookAt(Entity.Position);
            }
        }
    }
}

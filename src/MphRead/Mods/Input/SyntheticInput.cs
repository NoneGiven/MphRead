using System;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// A keyboard and a mouse that no window owns.
    ///
    /// <see cref="Scene"/> is handed a <see cref="KeyboardState"/> and a
    /// <see cref="MouseState"/> in its constructor and reads both once a frame
    /// (<c>PlayerEntity.ProcessInput</c> snapshots them unconditionally,
    /// before it decides whether anybody is driving), so a scene cannot be
    /// built without a pair -- and OpenTK only ever builds them from a GLFW
    /// window.
    ///
    /// The dedicated server's simulation has no window and no player at the
    /// keyboard: every slot is a puppet driven by relayed intent, so these two
    /// are read, snapshotted and never pressed. They exist to satisfy the
    /// signature.
    ///
    /// OpenTK does not let anyone else build those types -- the constructors
    /// are internal -- so they are reached by reflection, once, at startup.
    ///
    /// <c>MphRead.Droid.AndroidInput</c> does the same trick from the other
    /// end, and for the opposite reason: it builds a pair in order to *press*
    /// them, so that a thumb on a touchscreen reaches the engine's input path
    /// as the key the player has bound. The two are deliberately not merged --
    /// that one is a whole input backend and this is four lines of
    /// construction -- but if OpenTK ever changes these constructors, both
    /// break, and this comment is how the second one gets found.
    /// </summary>
    public static class SyntheticInput
    {
        public static KeyboardState CreateKeyboard()
        {
            return Create<KeyboardState>();
        }

        public static MouseState CreateMouse()
        {
            return Create<MouseState>();
        }

        private static T Create<T>()
        {
            Type type = typeof(T);
            object? instance = Activator.CreateInstance(type, nonPublic: true);
            if (instance == null)
            {
                throw new ProgramException(
                    $"Could not construct {type.Name}: OpenTK's non-public constructor is gone. "
                    + "See Mods/Input/SyntheticInput.cs.");
            }
            return (T)instance;
        }

        /// <summary>
        /// Whether the pair can be built at all on this runtime, asked before
        /// a scene is attempted rather than discovered inside one.
        ///
        /// A reflection failure here is a broken OpenTK upgrade, not a broken
        /// machine, and it should be reported as one line at startup rather
        /// than as a null reference thrown from inside a room load.
        /// </summary>
        public static bool Available(out string reason)
        {
            try
            {
                _ = CreateKeyboard();
                _ = CreateMouse();
                reason = "";
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }
    }
}

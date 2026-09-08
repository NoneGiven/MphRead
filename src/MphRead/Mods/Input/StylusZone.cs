using System;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// Which part of the DS's bottom screen the pen is on.
    /// </summary>
    public enum StylusRegion
    {
        /// <summary>Outside the zone entirely. The pointer is a mouse again.</summary>
        None,
        /// <summary>The middle, which is the map on the DS and the aim here.</summary>
        Aim,
        PowerBeam,
        Missile,
        Weapons,
        WeaponSelect,
        AltForm
    }

    /// <summary>
    /// The DS's bottom screen, drawn faintly on the top one and mapped to a
    /// tablet.
    ///
    /// A pen tablet is an absolute device: a point on the tablet is a point on
    /// the screen, and it stays that point. That is what the DS's touch screen
    /// was, and it is why a tablet is the one desktop device that can play
    /// this game the way it was actually played -- the four weapon buttons
    /// along the top, the morph ball in the corner, and aiming by dragging in
    /// the middle. Reaching for a button is a movement of the hand to a place,
    /// not a search with a cursor.
    ///
    /// So the player marks out a rectangle on their screen that stands for the
    /// bottom screen, and maps their tablet to it. Inside it, the layout is
    /// the DS's; outside it, nothing here applies and the pointer is an
    /// ordinary mouse. The rectangle keeps the DS's 4:3, because the layout
    /// is a picture and stretching it would put the buttons somewhere the hand
    /// has not learned.
    ///
    /// It is drawn on the top screen at very low opacity: enough that the hand
    /// can find a button without looking away from the game, not enough to be
    /// part of the picture. The DS player could glance down; this is the
    /// nearest thing to that on one screen.
    ///
    /// Positions below are in the DS's own 256x192 units and are taken off the
    /// game's bottom screen: four buttons across the top -- the Power Beam,
    /// the missile with its ammo count, the weapon and the weapon select --
    /// the alt form in the bottom right corner, and the map filling the
    /// middle, which is where aiming happens.
    /// </summary>
    public static class StylusZone
    {
        public const float DsWidth = 256;
        public const float DsHeight = 192;

        /// <summary>
        /// A button on the bottom screen: where it is and how big, in DS
        /// units, and what it does.
        /// </summary>
        public readonly struct Button
        {
            public readonly StylusRegion Region;
            public readonly float X;
            public readonly float Y;
            public readonly float Radius;
            public readonly string Label;

            public Button(StylusRegion region, float x, float y, float radius, string label)
            {
                Region = region;
                X = x;
                Y = y;
                Radius = radius;
                Label = label;
            }
        }

        /// <summary>
        /// The layout, off the DS screen. Order matters only in that the
        /// first one containing the point wins, and they do not overlap.
        /// </summary>
        public static readonly Button[] Buttons =
        {
            new Button(StylusRegion.PowerBeam, 26, 26, 22, "BEAM"),
            new Button(StylusRegion.Missile, 80, 24, 20, "MSL"),
            new Button(StylusRegion.Weapons, 150, 28, 30, "WPN"),
            new Button(StylusRegion.WeaponSelect, 222, 28, 26, "SEL"),
            new Button(StylusRegion.AltForm, 228, 166, 22, "ALT")
        };

        /// <summary>Whether the zone is being used at all.</summary>
        public static bool Enabled { get; set; }

        // Where the zone is, as fractions of the window, so it survives a
        // resize and a change of monitor. Width alone: the height follows
        // from the DS's shape, which the layout depends on.
        public static float Left { get; private set; } = 0.62f;
        public static float Top { get; private set; } = 0.60f;
        public static float Width { get; private set; } = 0.34f;

        public static float Height => Width * (DsHeight / DsWidth) * AspectCorrection;

        /// <summary>
        /// Window shape, so a zone given as a fraction of the width is still
        /// the DS's shape on screen. Set once a frame by the renderer, which
        /// is the only thing that knows the window.
        /// </summary>
        public static float AspectCorrection { get; set; } = 16f / 9f;

        /// <summary>How solid the overlay is drawn. Barely there by design --
        /// see the class summary -- but a slider's worth of a question, since
        /// screens and eyes differ.</summary>
        public static float Opacity { get; set; } = 0.22f;

        public static void SetRect(float left, float top, float width)
        {
            Width = Math.Clamp(width, 0.10f, 1f);
            Left = Math.Clamp(left, 0, 1 - Width);
            Top = Math.Clamp(top, 0, Math.Max(0, 1 - Height));
        }

        /// <summary>
        /// The player is drawing the zone right now, so the overlay is drawn
        /// solidly and the game takes no input from the pointer.
        /// </summary>
        public static bool Placing { get; private set; }

        private static float _placeAnchorX;
        private static float _placeAnchorY;
        private static bool _placeAnchored;

        /// <summary>
        /// One button in the settings, and then the whole of it: the player
        /// drags a rectangle on the screen where the bottom screen should be,
        /// and that is both the position and the size. Nothing to type, and
        /// nothing that can be set to a shape the layout does not fit.
        /// </summary>
        public static void BeginPlacement()
        {
            Placing = true;
            _placeAnchored = false;
        }

        public static void CancelPlacement()
        {
            Placing = false;
            _placeAnchored = false;
        }

        /// <summary>A press while placing: the first corner.</summary>
        public static void PlacementDown(float x, float y)
        {
            if (!Placing)
            {
                return;
            }
            _placeAnchorX = Math.Clamp(x, 0, 1);
            _placeAnchorY = Math.Clamp(y, 0, 1);
            _placeAnchored = true;
        }

        /// <summary>A drag while placing: the far corner, so far.</summary>
        public static void PlacementDrag(float x, float y)
        {
            if (!Placing || !_placeAnchored)
            {
                return;
            }
            float width = Math.Abs(Math.Clamp(x, 0, 1) - _placeAnchorX);
            SetRect(Math.Min(_placeAnchorX, x), Math.Min(_placeAnchorY, y), width);
        }

        /// <summary>Release: that is the zone. Enabling it is the point of
        /// having drawn one.</summary>
        public static void PlacementUp()
        {
            if (!Placing)
            {
                return;
            }
            Placing = false;
            if (_placeAnchored)
            {
                Enabled = true;
            }
            _placeAnchored = false;
        }

        // ------------------------------------------------------------- input

        /// <summary>Where the pen is, this frame.</summary>
        public static StylusRegion Region { get; private set; }

        /// <summary>Whether the pen is in contact -- the left button, which
        /// is what a pen tip reports.</summary>
        public static bool Contact { get; private set; }

        /// <summary>
        /// A button that was touched this frame and not the frame before.
        /// <see cref="StylusRegion.None"/> when nothing was.
        /// </summary>
        public static StylusRegion Pressed { get; private set; }

        private static StylusRegion _lastRegion;
        private static bool _lastContact;

        /// <summary>
        /// One frame of pointer, in window fractions. Called by the renderer
        /// whether or not the zone is on, because it is also what drives the
        /// placement drag.
        /// </summary>
        public static void Update(float x, float y, bool contact)
        {
            Pressed = StylusRegion.None;
            if (Placing)
            {
                Region = StylusRegion.None;
                Contact = contact;
                _lastContact = contact;
                return;
            }
            if (!Enabled)
            {
                Region = StylusRegion.None;
                Contact = false;
                _lastRegion = StylusRegion.None;
                _lastContact = false;
                return;
            }
            Region = RegionAt(x, y);
            Contact = contact;
            // A press is a contact that has just begun, or one that has slid
            // onto a different button without lifting. The second half
            // matters on a tablet: the hand moves along the row of buttons
            // with the tip down far more often than it taps each one.
            if (contact && Region != StylusRegion.Aim && Region != StylusRegion.None
                && (!_lastContact || Region != _lastRegion))
            {
                Pressed = Region;
            }
            _lastRegion = Region;
            _lastContact = contact;
        }

        /// <summary>Which part of the zone a window position falls in.</summary>
        public static StylusRegion RegionAt(float x, float y)
        {
            float height = Height;
            if (height <= 0 || x < Left || x >= Left + Width || y < Top || y >= Top + height)
            {
                return StylusRegion.None;
            }
            // Into DS units, where the layout is written.
            float dsX = (x - Left) / Width * DsWidth;
            float dsY = (y - Top) / height * DsHeight;
            for (int i = 0; i < Buttons.Length; i++)
            {
                Button button = Buttons[i];
                float dx = dsX - button.X;
                float dy = dsY - button.Y;
                if (dx * dx + dy * dy <= button.Radius * button.Radius)
                {
                    return button.Region;
                }
            }
            return StylusRegion.Aim;
        }

        /// <summary>
        /// Whether the pointer is somewhere that must not also aim or fire.
        ///
        /// On the DS the stylus aims and the shoulder button fires, so a touch
        /// on a button is not a shot. Here the pen tip *is* the left mouse
        /// button, which is the fire bind -- so touching the weapon button
        /// would fire the weapon it just selected, and dragging the tip
        /// across the row would turn the view. Both are suppressed while the
        /// tip is on a button, and neither is touched anywhere else.
        /// </summary>
        public static bool OnButton => Enabled && !Placing
            && Region != StylusRegion.None && Region != StylusRegion.Aim;

        public static void Reset()
        {
            Region = StylusRegion.None;
            Pressed = StylusRegion.None;
            Contact = false;
            _lastRegion = StylusRegion.None;
            _lastContact = false;
            Placing = false;
            _placeAnchored = false;
        }
    }
}

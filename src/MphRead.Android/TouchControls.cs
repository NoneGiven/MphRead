using System;
using System.Collections.Generic;
using MphRead.Mods.Input;

namespace MphRead.Droid
{
    /// <summary>What a thumb can press. Each maps to one of the player's binds.</summary>
    internal enum TouchAction
    {
        /// <summary>
        /// FIRE, which is both attacks: the gun on foot and the alt form's
        /// attack in the ball. There is no separate ALT action, because there
        /// was never a separate button -- the DS had one, and the game's
        /// defaults still bind both to it.
        /// </summary>
        Shoot,
        Jump,
        Morph,
        /// <summary>Opens and closes the scan visor: the desktop's SCAN VISOR (E).</summary>
        ScanVisor,
        /// <summary>
        /// Scans whatever is targeted, held: the desktop's SCAN (Q). Its own
        /// button, because it is a second step rather than another way to
        /// press VISOR -- reading an entry is not leaving the visor, and one
        /// button trying to be both made a press mean either.
        /// </summary>
        Scan,
        /// <summary>
        /// Missile, and back again. The wheel is the six affinity weapons and
        /// nothing else -- PlayerHud's weapon select has six slots, none of
        /// them the Power Beam or the Missile -- so on a screen with no number
        /// keys there was no way to reach a missile at all, in adventure or in
        /// a match.
        /// </summary>
        Missile,
        WeaponMenu,
        Zoom,
        Pause,
        /// <summary>
        /// The DS's own pause button, which is the map and status screen on
        /// foot and the scoreboard while it is held in a match. MENU stopped
        /// being that when it became the way to the app's own menu, and it is
        /// still worth reaching.
        /// </summary>
        Scoreboard,
        /// <summary>
        /// Opens the chat line and asks for the soft keyboard. The desktop's
        /// T, which a phone has no way to press.
        ///
        /// Hidden unless a networked match is running: offline there is nobody
        /// to read it, and in the story it does not exist at all.
        /// </summary>
        Chat
    }

    internal sealed class TouchButton
    {
        public TouchAction Action { get; }

        /// <summary>
        /// What is written on it *now*. Not fixed: a spectator's screen is
        /// the same dozen circles doing different jobs, and a button that
        /// still says FIRE while it cycles players is a button nobody
        /// presses. See <c>TouchControls.ApplyLayoutLocked</c>.
        /// </summary>
        public string Label { get; private set; }

        private readonly string _defaultLabel;

        /// <summary>Rename it, or pass null to put its own name back.</summary>
        public void Relabel(string? label)
        {
            Label = label ?? _defaultLabel;
        }
        public float CentreX { get; set; }
        public float CentreY { get; set; }
        public float Radius { get; set; }
        /// <summary>
        /// Whether it is on screen at all. FIRE and SCAN share a place and
        /// take turns: the visor cannot shoot and the gun cannot scan.
        /// </summary>
        public bool Visible { get; set; } = true;

        public TouchButton(TouchAction action, string label)
        {
            Action = action;
            Label = label;
            _defaultLabel = label;
        }

        public bool Contains(float x, float y)
        {
            float dx = x - CentreX;
            float dy = y - CentreY;
            // a little forgiveness: thumbs are not precise and the gap between
            // these is bigger than the slop
            float reach = Radius * 1.15f;
            return dx * dx + dy * dy <= reach * reach;
        }
    }

    /// <summary>
    /// The on-screen controls: where they are, what is being held, and how far
    /// the aiming finger has moved since the last frame read it.
    ///
    /// Deliberately free of any Android type. The view draws what this
    /// describes and reports touches to it; the game loop reads it. That split
    /// is what lets the layout be reasoned about (and moved) without going
    /// through a device.
    ///
    /// Touches arrive on the UI thread and are read on the GL thread, so
    /// everything crossing that line is behind the lock.
    /// </summary>
    internal sealed class TouchControls
    {
        [Flags]
        public enum Dir
        {
            None = 0,
            Up = 1,
            Down = 2,
            Left = 4,
            Right = 8
        }

        private readonly object _lock = new object();

        public IReadOnlyList<TouchButton> Buttons => _buttons;
        private readonly List<TouchButton> _buttons = new List<TouchButton>
        {
            // SCAN before FIRE: they sit in the same place, and the one that
            // is visible is the one a thumb should find there.
            new TouchButton(TouchAction.Scan, "SCAN") { Visible = false },
            new TouchButton(TouchAction.Shoot, "FIRE"),
            new TouchButton(TouchAction.Jump, "JUMP"),
            new TouchButton(TouchAction.Morph, "MORPH"),
            // No ALT button. It shared MouseButton.Left with FIRE -- the
            // game's own default, the DS having had one attack button -- so
            // it was a second way to press the thing FIRE presses, and having
            // both is what stopped FIRE working at all. FIRE is both attacks
            // now; see GameView.CollectInput. VISOR now sits where ALT did.
            new TouchButton(TouchAction.ScanVisor, "VISOR"),
            new TouchButton(TouchAction.Missile, "MSSL"),
            new TouchButton(TouchAction.WeaponMenu, "WEAPON"),
            new TouchButton(TouchAction.Zoom, "ZOOM"),
            new TouchButton(TouchAction.Pause, "MENU"),
            new TouchButton(TouchAction.Scoreboard, "SCORE"),
            new TouchButton(TouchAction.Chat, "CHAT") { Visible = false }
        };

        public float Width { get; private set; }
        public float Height { get; private set; }
        /// <summary>Screen density, so a swipe turns the same amount on any phone.</summary>
        public float Density { get; private set; } = 1f;

        // the movement stick, which appears where the thumb lands
        public bool StickActive { get; private set; }
        public float StickX { get; private set; }
        public float StickY { get; private set; }
        public float StickKnobX { get; private set; }
        public float StickKnobY { get; private set; }
        public float StickRadius { get; private set; }
        public float StickKnobRadius { get; private set; }

        // The controls step aside for a pad and come back at the first
        // touch. See NotePadActivity.
        private bool _padDriving;
        private bool _forceVisible;

        private readonly HashSet<TouchAction> _held = new HashSet<TouchAction>();
        private readonly Dictionary<int, TouchAction> _buttonPointers = new Dictionary<int, TouchAction>();
        private int _stickPointer = -1;
        private int _aimPointer = -1;
        private float _aimLastX;
        private float _aimLastY;
        private float _aimDeltaX;
        private float _aimDeltaY;
        private float _aimAbsX;
        private float _aimAbsY;
        private bool _aimDown;
        private Dir _direction;

        // FIRE doubles as an aim drag: a thumb that presses FIRE and then
        // moves keeps firing and steers the reticle, rather than releasing
        // FIRE the instant it leaves the button's circle.
        private int _fireAimPointer = -1;
        private float _fireAimLastX;
        private float _fireAimLastY;

        // WEAPON is the same trick for a different reason: the finger that
        // opens the wheel is also the one that picks off it. Press, drag into
        // the weapon, let go -- one thumb, and the other one never leaves the
        // stick. Without this the button releases the moment the finger slides
        // out of its circle, which closes the wheel before anything can be
        // chosen, so picking a weapon needed a second finger and standing
        // still. Only the position is kept: the wheel reads where the finger
        // is, never how far it moved, so this contributes nothing to the aim.
        private int _wheelPointer = -1;
        private float _wheelX;
        private float _wheelY;

        // A quick flick on the right side of the screen -- the aim side,
        // where nothing else about movement is being controlled -- boosts
        // in morph ball, the way a stylus flick did on the DS. See
        // GameView.CollectInput and PlayerInput's boost handling for the
        // other half of this. Tracked on both the free-look aim pointer and
        // the FIRE-drag pointer, since either thumb might do the flick.
        private const float SwipeBoostDistanceDp = 50f;
        private const long SwipeBoostWindowMs = 120;
        private const long SwipeBoostCooldownMs = 350;
        private readonly SwipeTracker _aimSwipe = new SwipeTracker();
        private readonly SwipeTracker _fireAimSwipe = new SwipeTracker();
        // Zero, not long.MinValue: TickCount64 minus long.MinValue overflows
        // to a large negative number, and the cooldown below would then never
        // be satisfied -- which is exactly what stopped this firing at all.
        private long _lastSwipeBoostTime;
        private bool _swipeBoostPending;
        private bool _swipeBoostEnabled;
        private float _swipeBoostX;
        private float _swipeBoostY;

        // Two quick taps on the aiming side jump, the way two taps of the
        // stylus did. A tap is a finger that went down and came up again
        // without going anywhere, so this cannot be confused with the flick
        // above it, which is nothing but going somewhere.
        private const long TapMaxMs = 250;
        private const long DoubleTapGapMs = 300;
        private const float TapSlopDp = 16f;
        private const float DoubleTapSpreadDp = 70f;
        private long _tapDownTime;
        private float _tapDownX;
        private float _tapDownY;
        private bool _tapMoved;
        private long _lastTapTime;
        private float _lastTapX;
        private float _lastTapY;
        private bool _doubleTapJumpPending;

        /// <summary>
        /// Whether a flick means anything right now -- it is the morph ball's
        /// boost, so only in the ball. Off, the flick is left alone to be the
        /// aim it looks like.
        /// </summary>
        public bool SwipeBoostEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _swipeBoostEnabled;
                }
            }
            set
            {
                lock (_lock)
                {
                    _swipeBoostEnabled = value;
                }
            }
        }

        /// <summary>
        /// Whether the scan visor is open, which is what swaps FIRE for SCAN.
        /// Set from the game thread, so a change repaints through
        /// <see cref="Invalidated"/> rather than waiting for the next touch.
        /// </summary>
        public bool ScanVisorActive
        {
            get
            {
                lock (_lock)
                {
                    return _scanVisorActive;
                }
            }
            set
            {
                Change(() =>
                {
                    if (_scanVisorActive == value)
                    {
                        return false;
                    }
                    _scanVisorActive = value;
                    return true;
                });
            }
        }
        private bool _scanVisorActive;

        /// <summary>
        /// Whether the CHAT button is on screen. Set once a frame from the
        /// game loop, which is the only thing that knows whether a networked
        /// match is running.
        /// </summary>
        public bool ChatEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _chatEnabled;
                }
            }
            set
            {
                Change(() =>
                {
                    if (_chatEnabled == value)
                    {
                        return false;
                    }
                    _chatEnabled = value;
                    return true;
                });
            }
        }
        private bool _chatEnabled;

        /// <summary>
        /// The screen a spectator gets: the same dozen circles, most of them
        /// gone and the rest doing something else.
        ///
        /// Set once a frame from the game loop, which is the only thing that
        /// knows either of these. Both at once rather than two properties,
        /// because the layout depends on the pair and a screen laid out twice
        /// from two half-answers flickers between them.
        /// </summary>
        public void SetSpectator(bool spectating, bool freeCamera)
        {
            Change(() =>
            {
                if (_spectating == spectating && _spectatorFreeCam == freeCamera)
                {
                    return false;
                }
                _spectating = spectating;
                _spectatorFreeCam = freeCamera;
                return true;
            });
        }

        private bool _spectating;
        private bool _spectatorFreeCam;

        /// <summary>
        /// The results screen is up, so the glass is a picker rather than a
        /// pair of thumbsticks.
        ///
        /// Set once a frame from the game loop, like the spectator pair above
        /// and for the same reason. Everything that drives a player goes away
        /// while it is on: nothing a player presses reaches the world during
        /// the results, so FIRE, JUMP, MORPH and the rest are twelve circles
        /// sitting on top of the one thing on screen anybody wants to touch.
        /// Menu, scoreboard and chat stay -- they still do what they say.
        /// </summary>
        public void SetEndScreen(bool active)
        {
            Change(() =>
            {
                if (_endScreen == active)
                {
                    return false;
                }
                _endScreen = active;
                if (!active)
                {
                    _tapPending = false;
                }
                return true;
            });
        }

        private bool _endScreen;

        /// <summary>
        /// Rectangles on the HUD that take a tap instead of the world, as
        /// window fractions in groups of four. Pushed once a frame by the
        /// game loop from whatever it has just drawn -- currently the map
        /// vote's accept and deny buttons.
        ///
        /// Kept as a list of boxes rather than a mode, because during a
        /// running match the player still has to be able to aim: a tap
        /// outside these is a tap on the world, and only the two small
        /// rectangles in the corner are the vote's.
        /// </summary>
        public void SetTapTargets(float[] targets)
        {
            lock (_lock)
            {
                _tapTargets = targets;
            }
        }

        private float[] _tapTargets = Array.Empty<float>();
        private bool _tapPending;
        private float _tapX;
        private float _tapY;

        /// <summary>
        /// Remember a tap for the game loop, in window fractions. Called with
        /// the lock held.
        /// </summary>
        private void NoteTapLocked(float x, float y)
        {
            _tapPending = true;
            _tapX = x / Math.Max(Width, 1);
            _tapY = y / Math.Max(Height, 1);
        }

        /// <summary>
        /// The tap the glass captured for the HUD, in window fractions, or
        /// nothing. Taken by the game loop, which is the thread that may act
        /// on it -- touches arrive on the UI thread and the picker's state
        /// belongs to the simulation.
        /// </summary>
        public (bool Got, float X, float Y) TakeTap()
        {
            lock (_lock)
            {
                if (!_tapPending)
                {
                    return (false, 0, 0);
                }
                _tapPending = false;
                return (true, _tapX, _tapY);
            }
        }

        /// <summary>
        /// Whether a touch landed on one of the rectangles the HUD published
        /// this frame. Called with the lock held.
        ///
        /// Asked <i>before</i> the round buttons rather than after them, which
        /// is the whole of why voting did nothing on a phone: the vote's
        /// ACCEPT and DENY sat in the top-left corner of the HUD and MENU,
        /// SCORE and CHAT sit in the top-left corner of the glass, so every
        /// tap meant for a ballot was eaten by whichever circle was over it.
        /// These rectangles exist for a handful of seconds and only while
        /// something is asking a question with them; a permanent control
        /// underneath one has not been reached for.
        /// </summary>
        private bool TapHitsTargetLocked(float x, float y)
        {
            if (Width <= 0 || Height <= 0)
            {
                return false;
            }
            float fx = x / Width;
            float fy = y / Height;
            for (int i = 0; i + 3 < _tapTargets.Length; i += 4)
            {
                if (fx >= _tapTargets[i] && fx < _tapTargets[i + 2]
                    && fy >= _tapTargets[i + 1] && fy < _tapTargets[i + 3])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether a touch the buttons did not take belongs to the HUD rather
        /// than to the world. Called with the lock held.
        /// </summary>
        private bool TapIsForHudLocked(float x, float y)
        {
            if (Width <= 0 || Height <= 0)
            {
                return false;
            }
            // The results screen takes everything the buttons did not: there
            // is no world to aim at while it is up.
            return _endScreen;
        }

        /// <summary>
        /// Apply a change to what the screen shows, and repaint if it moved.
        ///
        /// The three callers used to each reach into <see cref="_buttons"/>
        /// and set the visibility of the ones they knew about, which only
        /// works while no two of them care about the same button. Spectating
        /// cares about nine of them, so the layout is worked out in one place
        /// now (<see cref="ApplyLayoutLocked"/>) from all of the state at
        /// once, and these only say what changed.
        ///
        /// The mutation reports whether it moved anything, and does so from
        /// inside the lock: these are set once a frame from the game thread
        /// and read on the UI thread, so a comparison made outside it is a
        /// comparison against a value that may already be stale.
        /// </summary>
        private void Change(Func<bool> mutate)
        {
            lock (_lock)
            {
                if (!mutate())
                {
                    return;
                }
                ApplyLayoutLocked();
            }
            Invalidated?.Invoke();
        }

        /// <summary>
        /// Which buttons are on the screen and what they say, from every
        /// piece of state that decides it. Called with the lock held.
        /// </summary>
        private void ApplyLayoutLocked()
        {
            foreach (TouchButton button in _buttons)
            {
                bool visible;
                string? label = null;
                if (_endScreen)
                {
                    // The three that still mean something between matches.
                    visible = button.Action == TouchAction.Pause
                        || button.Action == TouchAction.Scoreboard
                        || button.Action == TouchAction.Chat && _chatEnabled;
                }
                else if (_spectating)
                {
                    // Nothing a spectator presses does anything in the world:
                    // PlayerEntity.ProcessInput skips the local player while
                    // this is on. So the buttons that would shoot, scan, pick
                    // a weapon or zoom go away, and the ones that are left are
                    // renamed to what they now do.
                    switch (button.Action)
                    {
                    case TouchAction.Shoot:
                        // The desktop's left click.
                        visible = true;
                        label = "NEXT";
                        break;
                    case TouchAction.ScanVisor:
                        // The desktop's Space: the map, or the player you were
                        // watching.
                        visible = true;
                        label = "VIEW";
                        break;
                    case TouchAction.Jump:
                    case TouchAction.Morph:
                        // Up and down on the free camera, and nothing at all
                        // riding along behind somebody's eyes -- so they are
                        // only there on the camera that can use them.
                        visible = _spectatorFreeCam;
                        label = button.Action == TouchAction.Jump ? "UP" : "DOWN";
                        break;
                    case TouchAction.Scoreboard:
                    case TouchAction.Pause:
                        visible = true;
                        break;
                    case TouchAction.Chat:
                        visible = _chatEnabled;
                        break;
                    default:
                        visible = false;
                        break;
                    }
                }
                else
                {
                    visible = button.Action switch
                    {
                        // FIRE and SCAN share a place and take turns: the
                        // visor cannot shoot and the gun cannot scan.
                        TouchAction.Scan => _scanVisorActive,
                        TouchAction.Shoot => !_scanVisorActive,
                        TouchAction.Chat => _chatEnabled,
                        _ => true
                    };
                }
                // And the player's own answer on top of the situation's: a
                // button they have turned off is off wherever it would
                // otherwise appear, spectating included. Hidden buttons take
                // no touches either (see the hit test in Down), which is the
                // point -- an aim drag that starts where ZOOM used to be is
                // now an aim drag.
                button.Visible = visible && TouchSettings.Shown(SettingOf(button.Action));
                button.Relabel(label);
            }
        }

        /// <summary>
        /// Which switch on the settings screen answers for this button.
        /// </summary>
        private static TouchControl SettingOf(TouchAction action)
        {
            return action switch
            {
                TouchAction.Shoot => TouchControl.Shoot,
                TouchAction.Jump => TouchControl.Jump,
                TouchAction.Morph => TouchControl.Morph,
                TouchAction.ScanVisor => TouchControl.ScanVisor,
                TouchAction.Scan => TouchControl.Scan,
                TouchAction.Missile => TouchControl.Missile,
                TouchAction.WeaponMenu => TouchControl.WeaponMenu,
                TouchAction.Zoom => TouchControl.Zoom,
                TouchAction.Pause => TouchControl.Pause,
                TouchAction.Scoreboard => TouchControl.Scoreboard,
                _ => TouchControl.Chat
            };
        }

        /// <summary>
        /// The settings screen has been closed: re-read which buttons the
        /// player wants and repaint. Called from the game loop, which is what
        /// knows the pause menu has just been away.
        /// </summary>
        public void ReloadSettings()
        {
            lock (_lock)
            {
                ApplyLayoutLocked();
            }
            Invalidated?.Invoke();
        }

        /// <summary>
        /// Asked to repaint, for the changes that do not come from a touch.
        /// The view sets this; nothing here knows what a view is.
        /// </summary>
        public Action? Invalidated { get; set; }

        /// <summary>
        /// Whether the controls are off the screen because a pad is being
        /// held. Read by the view, which draws nothing while it is true.
        /// </summary>
        public bool PadDriving
        {
            get
            {
                lock (_lock)
                {
                    return HiddenLocked;
                }
            }
        }

        /// <summary>Called with the lock held.</summary>
        private bool HiddenLocked => _padDriving && !_forceVisible;

        /// <summary>
        /// Keep the controls on screen whatever the pad is doing.
        ///
        /// Set once a frame by the game loop, for the one thing a pad cannot
        /// do: a dialog box is dismissed by pressing its OK button, which is
        /// read as a *position* on what used to be a touch screen (see
        /// PlayerDialog.CheckButtonPressed), and GamepadInput deliberately
        /// drives no pointer. Hiding the controls through one of those would
        /// be a story that cannot be continued.
        ///
        /// A flag the loop keeps setting rather than a one-shot "show
        /// yourself", because a thumb still on the stick would otherwise put
        /// them away again on the very next motion event and the box would
        /// flicker for as long as the pad was held.
        /// </summary>
        public bool ForceVisible
        {
            get
            {
                lock (_lock)
                {
                    return _forceVisible;
                }
            }
            set
            {
                bool before;
                bool after;
                lock (_lock)
                {
                    before = HiddenLocked;
                    _forceVisible = value;
                    after = HiddenLocked;
                }
                Settle(before, after);
            }
        }

        /// <summary>
        /// A pad button or stick moved: put the controls away.
        ///
        /// There is no setting behind this and there was one -- "use a
        /// connected gamepad". Being *connected* was never the question a
        /// player was asking: a pad paired with the phone for something else
        /// is still paired while they play with their thumbs, so hiding on
        /// connection would take the controls away from someone who never
        /// picked the pad up. What is being watched here is the pad actually
        /// being used, and the answer is reversed by the next finger on the
        /// glass.
        /// </summary>
        public void NotePadActivity()
        {
            bool before;
            bool after;
            lock (_lock)
            {
                before = HiddenLocked;
                _padDriving = true;
                after = HiddenLocked;
            }
            Settle(before, after);
        }

        /// <summary>
        /// Repaint when the controls appeared or went away.
        ///
        /// It used to let go of everything held on the way out, and that was
        /// the right answer to the wrong question: a thumb resting on FIRE as
        /// the player picked the pad up would have been held down for ever,
        /// *because the finger's release landed on a control that had stopped
        /// listening*. It does not stop listening any more -- hiding the
        /// layout hides the layout -- so the release arrives and does its job,
        /// and cancelling here would instead cut off a player who is
        /// deliberately holding a button with one hand and a stick with the
        /// other.
        /// </summary>
        private void Settle(bool wasHidden, bool isHidden)
        {
            if (wasHidden == isHidden)
            {
                return;
            }
            Invalidated?.Invoke();
        }

        /// <summary>Lay the controls out for a viewport of this size.</summary>
        public void Layout(float width, float height, float density)
        {
            lock (_lock)
            {
                Width = width;
                Height = height;
                Density = density <= 0 ? 1f : density;
                float h = height;
                StickRadius = 0.15f * h;
                StickKnobRadius = 0.06f * h;
                Place(TouchAction.Shoot, width - 0.17f * h, h - 0.19f * h, 0.105f * h);
                // The same place and the same size: in the visor, that thumb
                // has nothing to shoot with and everything to scan with.
                Place(TouchAction.Scan, width - 0.17f * h, h - 0.19f * h, 0.105f * h);
                Place(TouchAction.Jump, width - 0.40f * h, h - 0.15f * h, 0.085f * h);
                Place(TouchAction.Morph, width - 0.15f * h, h - 0.47f * h, 0.080f * h);
                Place(TouchAction.ScanVisor, width - 0.38f * h, h - 0.42f * h, 0.075f * h);
                // Within the firing thumb's reach, since a missile is fired
                // rather than administered, and clear of JUMP above it and
                // VISOR beside it.
                Place(TouchAction.Missile, width - 0.62f * h, h - 0.28f * h, 0.075f * h);
                Place(TouchAction.WeaponMenu, width - 0.12f * h, 0.15f * h, 0.075f * h);
                Place(TouchAction.Zoom, width - 0.33f * h, 0.12f * h, 0.065f * h);
                Place(TouchAction.Pause, 0.11f * h, 0.12f * h, 0.060f * h);
                Place(TouchAction.Scoreboard, 0.28f * h, 0.12f * h, 0.060f * h);
                // Third along the top row, past MENU and SCORE. Low enough to
                // clear the chat log itself, which grows downward from the top
                // of the HUD's own space and is inset to miss MENU already.
                Place(TouchAction.Chat, 0.45f * h, 0.12f * h, 0.060f * h);
            }
        }

        private void Place(TouchAction action, float x, float y, float radius)
        {
            foreach (TouchButton button in _buttons)
            {
                if (button.Action == action)
                {
                    button.CentreX = x;
                    button.CentreY = y;
                    button.Radius = radius;
                    return;
                }
            }
        }

        public bool IsHeld(TouchAction action)
        {
            lock (_lock)
            {
                return _held.Contains(action);
            }
        }

        /// <summary>
        /// The aim movement since this was last called, in density-independent
        /// pixels, and cleared by the call -- so a frame that reads it twice
        /// does not turn twice.
        /// </summary>
        public (float X, float Y) TakeAimDelta()
        {
            lock (_lock)
            {
                float x = _aimDeltaX;
                float y = _aimDeltaY;
                _aimDeltaX = 0;
                _aimDeltaY = 0;
                return (x / Density, y / Density);
            }
        }

        /// <summary>
        /// Whether a swipe boost fired since this was last called, and which
        /// way the flick went -- a unit vector in screen terms, X to the
        /// right and Y downwards. Cleared by the call, so a frame that reads
        /// it twice does not boost twice.
        /// </summary>
        public (bool Fired, float X, float Y) TakeSwipeBoost()
        {
            lock (_lock)
            {
                bool pending = _swipeBoostPending;
                _swipeBoostPending = false;
                return (pending, _swipeBoostX, _swipeBoostY);
            }
        }

        /// <summary>
        /// Whether the aiming side was tapped twice quickly, cleared by the
        /// call. The DS jumped on a double tap and so does this.
        /// </summary>
        public bool TakeDoubleTapJump()
        {
            lock (_lock)
            {
                bool pending = _doubleTapJumpPending;
                _doubleTapJumpPending = false;
                return pending;
            }
        }

        /// <summary>
        /// Treat the whole screen as a place to point at, rather than the left
        /// half as a stick.
        ///
        /// The DS had a touch screen and parts of this game still read a
        /// position off it -- the dialog boxes and their OK button among them.
        /// Those parts are drawn across the middle of the screen, which is the
        /// seam between the stick and the aim, so half of an OK button lands on
        /// a thumbstick that is not being used: the game is paused behind the
        /// box. While one of those is up the split does more harm than good.
        /// </summary>
        public bool PointerIsAbsolute { get; set; }

        /// <summary>Where the aiming finger is, for the parts that read a position.</summary>
        public (bool Down, float X, float Y) AimPosition()
        {
            lock (_lock)
            {
                return (_aimDown, _aimAbsX, _aimAbsY);
            }
        }

        /// <summary>
        /// Where the weapon wheel should read its cursor from.
        ///
        /// The aiming finger when there is one, and otherwise the finger
        /// holding WEAPON. That order is what keeps both ways of using the
        /// wheel: press and drag with one thumb, which is what it is for, and
        /// the older hold-with-one-tap-with-another, which still works and is
        /// what somebody who learnt it will do. A second finger put down
        /// while the wheel is open is an explicit choice and wins.
        ///
        /// Separate from <see cref="AimPosition"/> rather than folded into it
        /// because that one also answers for the dialog boxes, and a WEAPON
        /// press has no business moving a cursor over an OK button.
        /// </summary>
        public (bool Down, float X, float Y) WeaponWheelPosition()
        {
            lock (_lock)
            {
                if (_aimDown)
                {
                    return (true, _aimAbsX, _aimAbsY);
                }
                if (_wheelPointer != -1)
                {
                    return (true, _wheelX, _wheelY);
                }
                return (false, _aimAbsX, _aimAbsY);
            }
        }

        public Dir Direction
        {
            get
            {
                lock (_lock)
                {
                    return _direction;
                }
            }
        }

        public void PointerDown(int pointerId, float x, float y)
        {
            bool revealed = false;
            lock (_lock)
            {
                revealed = HiddenLocked;
                // A finger on the glass is the player choosing the
                // touchscreen again, whether or not the controls had gone.
                _padDriving = false;
                // And it does what it landed on, drawn or not. The controls
                // used to swallow this one -- the reasoning being that a
                // thumb reaching for a screen it had stopped looking at
                // lands in FIRE's half -- but that is a rule about a player
                // who has *put the pad down*, and the pad and the glass are
                // routinely both in use at once: a stick in the left hand and
                // a thumb on FIRE, or a pad for movement and the screen for
                // the weapon wheel, which is the one thing a pad cannot reach
                // at all. Swallowing the touch made those cost a press every
                // time the pad had been moved since. Hiding the layout is
                // still right; disabling the surface under it was not.
                PointerDownLocked(pointerId, x, y);
            }
            if (revealed)
            {
                Invalidated?.Invoke();
            }
        }

        /// <summary>Called with the lock already held.</summary>
        private void PointerDownLocked(int pointerId, float x, float y)
        {
            if (TapHitsTargetLocked(x, y))
            {
                // Straight to the game loop as a point on the screen, and
                // before the buttons get a look at it: see
                // TapHitsTargetLocked. Not held, not dragged -- what is under
                // it is a button on the HUD, and a press and a release in the
                // same place is the whole gesture.
                NoteTapLocked(x, y);
                return;
            }
            foreach (TouchButton button in _buttons)
            {
                if (button.Visible && button.Contains(x, y))
                {
                    _buttonPointers[pointerId] = button.Action;
                    _held.Add(button.Action);
                    // Both of the buttons that live under the aiming
                    // thumb drag the aim as well: keeping a target
                    // centred matters as much while scanning it as it
                    // does while shooting at it.
                    if (button.Action == TouchAction.Shoot || button.Action == TouchAction.Scan)
                    {
                        _fireAimPointer = pointerId;
                        _fireAimLastX = x;
                        _fireAimLastY = y;
                        _fireAimSwipe.Reset();
                    }
                    else if (button.Action == TouchAction.WeaponMenu)
                    {
                        _wheelPointer = pointerId;
                        _wheelX = x;
                        _wheelY = y;
                    }
                    return;
                }
            }
            if (TapIsForHudLocked(x, y))
            {
                NoteTapLocked(x, y);
                return;
            }
            if (!PointerIsAbsolute && x < Width / 2 && _stickPointer == -1)
            {
                _stickPointer = pointerId;
                StickActive = true;
                StickX = x;
                StickY = y;
                StickKnobX = x;
                StickKnobY = y;
                _direction = Dir.None;
                return;
            }
            if (_aimPointer == -1)
            {
                _aimPointer = pointerId;
                _aimLastX = x;
                _aimLastY = y;
                _aimAbsX = x;
                _aimAbsY = y;
                _aimDown = true;
                _aimSwipe.Reset();
                _tapDownTime = Environment.TickCount64;
                _tapDownX = x;
                _tapDownY = y;
                _tapMoved = false;
            }
        }

        /// <summary>
        /// The last few positions of one finger, for telling a flick from a
        /// drag. Only the newest matter, so it is a small ring.
        /// </summary>
        private sealed class SwipeTracker
        {
            private const int Capacity = 12;
            private readonly float[] _x = new float[Capacity];
            private readonly float[] _y = new float[Capacity];
            private readonly long[] _time = new long[Capacity];
            private int _count;
            private int _newest = -1;

            public void Reset()
            {
                _count = 0;
                _newest = -1;
            }

            public void Add(float x, float y, long now)
            {
                _newest = (_newest + 1) % Capacity;
                _x[_newest] = x;
                _y[_newest] = y;
                _time[_newest] = now;
                if (_count < Capacity)
                {
                    _count++;
                }
            }

            /// <summary>
            /// The furthest the finger has come from any sample still inside
            /// the window -- and always from at least the one before this,
            /// however old it is.
            ///
            /// That last part is the whole trick. A finger holding still
            /// sends no MOVE events at all, so a flick that follows one can
            /// arrive as a single jump whose predecessor is seconds old, and
            /// a window that only trusted its own age would throw away
            /// exactly the sample the flick lives in.
            /// </summary>
            public (float Distance, float X, float Y) Displacement(long now, long windowMs)
            {
                if (_count < 2)
                {
                    return (0, 0, 0);
                }
                float newestX = _x[_newest];
                float newestY = _y[_newest];
                float best = 0;
                float bestX = 0;
                float bestY = 0;
                for (int i = 1; i < _count; i++)
                {
                    int index = (_newest - i + Capacity) % Capacity;
                    if (i > 1 && now - _time[index] > windowMs)
                    {
                        break;
                    }
                    float dx = newestX - _x[index];
                    float dy = newestY - _y[index];
                    float distance = dx * dx + dy * dy;
                    if (distance > best)
                    {
                        best = distance;
                        bestX = dx;
                        bestY = dy;
                    }
                }
                return (MathF.Sqrt(best), bestX, bestY);
            }
        }

        /// <summary>Called with the lock already held.</summary>
        private void CheckSwipeBoost(SwipeTracker tracker, float x, float y)
        {
            long now = Environment.TickCount64;
            tracker.Add(x, y, now);
            if (!_swipeBoostEnabled || now - _lastSwipeBoostTime < SwipeBoostCooldownMs)
            {
                return;
            }
            float threshold = SwipeBoostDistanceDp * Density;
            (float distance, float dx, float dy) = tracker.Displacement(now, SwipeBoostWindowMs);
            if (distance > threshold)
            {
                _swipeBoostPending = true;
                // Kept as a direction rather than a length: how hard the flick
                // was does not set how hard the boost is (it is always a full
                // charge), only which way it goes.
                _swipeBoostX = dx / distance;
                _swipeBoostY = dy / distance;
                _lastSwipeBoostTime = now;
                tracker.Reset();
                // The flick was the boost, not a look. Letting it through as
                // aim as well would swing the camera through the whole of it.
                _aimDeltaX = 0;
                _aimDeltaY = 0;
            }
        }

        public void PointerMove(int pointerId, float x, float y)
        {
            lock (_lock)
            {
                if (pointerId == _stickPointer)
                {
                    float dx = x - StickX;
                    float dy = y - StickY;
                    float length = MathF.Sqrt(dx * dx + dy * dy);
                    if (length > StickRadius)
                    {
                        // the stick follows a thumb that has slid past its edge,
                        // rather than sticking at the rim and losing the input
                        StickX += dx * (1 - StickRadius / length);
                        StickY += dy * (1 - StickRadius / length);
                        dx = x - StickX;
                        dy = y - StickY;
                        length = StickRadius;
                    }
                    StickKnobX = x;
                    StickKnobY = y;
                    _direction = Dir.None;
                    float deadzone = StickRadius * 0.28f;
                    if (length > deadzone)
                    {
                        // eight-way, like the d-pad this stands in for: the
                        // engine's movement is a set of held keys, not an axis
                        float angle = MathF.Atan2(-dy, dx) * (180f / MathF.PI);
                        if (angle < 0)
                        {
                            angle += 360f;
                        }
                        if (angle > 22.5f && angle < 157.5f)
                        {
                            _direction |= Dir.Up;
                        }
                        if (angle > 202.5f && angle < 337.5f)
                        {
                            _direction |= Dir.Down;
                        }
                        if (angle > 112.5f && angle < 247.5f)
                        {
                            _direction |= Dir.Left;
                        }
                        if (angle < 67.5f || angle > 292.5f)
                        {
                            _direction |= Dir.Right;
                        }
                    }
                    return;
                }
                if (pointerId == _aimPointer)
                {
                    CheckSwipeBoost(_aimSwipe, x, y);
                    if (!_tapMoved)
                    {
                        float tapDx = x - _tapDownX;
                        float tapDy = y - _tapDownY;
                        float slop = TapSlopDp * Density;
                        _tapMoved = tapDx * tapDx + tapDy * tapDy > slop * slop;
                    }
                    _aimDeltaX += x - _aimLastX;
                    _aimDeltaY += y - _aimLastY;
                    _aimLastX = x;
                    _aimLastY = y;
                    _aimAbsX = x;
                    _aimAbsY = y;
                    return;
                }
                if (pointerId == _fireAimPointer)
                {
                    // FIRE stays held here regardless of how far the thumb
                    // drags: this pointer skips the "slides off a button
                    // releases it" rule below on purpose.
                    CheckSwipeBoost(_fireAimSwipe, x, y);
                    _aimDeltaX += x - _fireAimLastX;
                    _aimDeltaY += y - _fireAimLastY;
                    _fireAimLastX = x;
                    _fireAimLastY = y;
                    return;
                }
                if (pointerId == _wheelPointer)
                {
                    // Same exemption, and for the same reason: the drag off
                    // WEAPON *is* the gesture, so it must not be read as
                    // letting go of the button. No aim delta -- see the field.
                    _wheelX = x;
                    _wheelY = y;
                    return;
                }
                // a thumb that slides off a button releases it, and one that
                // slides onto another does not press it: a button press is
                // where the finger landed
                if (_buttonPointers.TryGetValue(pointerId, out TouchAction action))
                {
                    foreach (TouchButton button in _buttons)
                    {
                        if (button.Action == action)
                        {
                            if (!button.Contains(x, y))
                            {
                                _buttonPointers.Remove(pointerId);
                                ReleaseAction(action);
                            }
                            return;
                        }
                    }
                }
            }
        }

        public void PointerUp(int pointerId)
        {
            lock (_lock)
            {
                if (pointerId == _stickPointer)
                {
                    _stickPointer = -1;
                    StickActive = false;
                    _direction = Dir.None;
                    return;
                }
                if (pointerId == _aimPointer)
                {
                    _aimPointer = -1;
                    _aimDown = false;
                    _aimSwipe.Reset();
                    long up = Environment.TickCount64;
                    if (!_tapMoved && up - _tapDownTime <= TapMaxMs)
                    {
                        float spread = DoubleTapSpreadDp * Density;
                        float sinceX = _tapDownX - _lastTapX;
                        float sinceY = _tapDownY - _lastTapY;
                        if (up - _lastTapTime <= DoubleTapGapMs
                            && sinceX * sinceX + sinceY * sinceY <= spread * spread)
                        {
                            _doubleTapJumpPending = true;
                            // Spent: a third tap starts a new pair rather than
                            // jumping again off the second one.
                            _lastTapTime = 0;
                        }
                        else
                        {
                            _lastTapTime = up;
                            _lastTapX = _tapDownX;
                            _lastTapY = _tapDownY;
                        }
                    }
                    return;
                }
                if (pointerId == _fireAimPointer)
                {
                    _fireAimPointer = -1;
                    _fireAimSwipe.Reset();
                }
                if (pointerId == _wheelPointer)
                {
                    // Cleared before the button is released, so the frame that
                    // sees the wheel close no longer reads a stale position.
                    _wheelPointer = -1;
                }
                if (_buttonPointers.Remove(pointerId, out TouchAction action))
                {
                    ReleaseAction(action);
                }
            }
        }

        public void ReleaseEverything()
        {
            lock (_lock)
            {
                _buttonPointers.Clear();
                _held.Clear();
                _stickPointer = -1;
                _aimPointer = -1;
                _aimDown = false;
                _fireAimPointer = -1;
                _wheelPointer = -1;
                _swipeBoostPending = false;
                _doubleTapJumpPending = false;
                _lastTapTime = 0;
                _aimSwipe.Reset();
                _fireAimSwipe.Reset();
                StickActive = false;
                _direction = Dir.None;
                _aimDeltaX = 0;
                _aimDeltaY = 0;
            }
        }

        private void ReleaseAction(TouchAction action)
        {
            // two fingers can hold the same button; it is up when the last one is
            foreach (KeyValuePair<int, TouchAction> pair in _buttonPointers)
            {
                if (pair.Value == action)
                {
                    return;
                }
            }
            _held.Remove(action);
        }
    }
}

using System;
using System.Collections.Generic;
using MphRead.Hud;
using MphRead.Mods.Chat;
using MphRead.Mods.Network;

namespace MphRead.Entities
{
    /// <summary>
    /// The chat log, and the line you type into.
    ///
    /// A partial of PlayerEntity for the same reason the ping column and the
    /// Pro HUD readouts are: the HUD's drawing is private, and reaching it
    /// from outside would mean opening the whole of it up rather than adding
    /// one call site.
    ///
    /// Quake 3's shape, because it is the one every player already knows how
    /// to read: small text, three lines deep, each one aging out on its own,
    /// with the line you are typing under them. Green, and on nothing -- no
    /// plate behind it.
    ///
    /// In the **bottom** left, which is where Quake, Counter-Strike, Team
    /// Fortress and every game that took the shape from them put it. It was
    /// in the top left first, and that is a corner nobody looks at in a
    /// shooter: messages went by unread, which is the whole failure of a chat
    /// log, and it was reported as one. Moving it down also gave two things
    /// back. The mode score is drawn in that same corner on most hunters and
    /// used to be pushed out of the way by a log that might have nothing in
    /// it (there was a <c>ModChatClearance</c> for exactly that, now gone),
    /// and Android's MENU, SCOREBOARD and SAY buttons sit along the top edge,
    /// which is why the log used to start 30 units in on that platform and
    /// no longer needs to.
    ///
    /// It does not use <c>DrawText2D</c>, and that is the whole reason
    /// <see cref="ChatFont"/> exists: the game's font is capitals-only, so
    /// every line anybody typed came out shouted, and half as wide again as
    /// it needed to be. Everything below is that routine's Align.Left branch
    /// with a different font in it and the Kanji handling dropped -- chat is
    /// ASCII by the time it reaches here (see <see cref="ChatPacket"/>).
    /// </summary>
    public partial class PlayerEntity
    {
        /// <summary>
        /// Glyph size, 1 being the font's own 8x8 cell. Caps are six of those
        /// rows, so this draws a 2.7-unit capital in a 192-unit screen --
        /// about 15 pixels at 1080p, and small enough that three lines of it
        /// sit above the energy bar without being in the way.
        /// </summary>
        private const float ChatScale = 0.45f;
        private const float ChatLineHeight = 7 * ChatScale + 1.2f;

        /// <summary>
        /// The bottom of the block, which is the bottom of the line being
        /// typed -- everything else is measured up from here.
        ///
        /// Not the bottom of the screen. Pro mode's energy panel is drawn at
        /// 170 and runs to the edge, and it is the one readout a player looks
        /// at more often than the chat; the stock HUD's own bottom-left is
        /// helmet moulding. 168 clears both, and is still far enough down to
        /// read as the corner rather than as the middle of the screen.
        /// </summary>
        private const float ChatBottom = 168;

        /// <summary>
        /// Where the line being typed sits. The log stacks upwards from just
        /// above it, so the newest message is always the one nearest the
        /// prompt and the one your eye is already on.
        ///
        /// Its row is reserved whether or not anybody is typing, so the log
        /// does not jump up by a line the moment the prompt opens.
        /// </summary>
        private const float ChatPromptY = ChatBottom - ChatLineHeight;

        /// <summary>
        /// The margin from the left edge. The same on every platform now that
        /// the log is at the bottom: Android's fixed buttons -- MENU,
        /// SCOREBOARD, SAY -- are all along the top, and the movement stick
        /// below them is drawn wherever the thumb lands rather than in a
        /// corner this could be kept out of.
        /// </summary>
        private const float ChatMargin = 3;

        // All green, with a hierarchy inside it: who said it stands out from
        // what they said, and the game's own notices are dimmer than either.
        private static readonly ColorRgba ChatName = new ColorRgba(110, 255, 130, 255);
        private static readonly ColorRgba ChatInk = new ColorRgba(170, 255, 175, 255);
        private static readonly ColorRgba ChatSystemInk = new ColorRgba(110, 205, 125, 255);
        private static readonly ColorRgba ChatPromptInk = new ColorRgba(110, 205, 125, 255);

        /// <summary>
        /// Two entries, and neither is ever read: <c>DoTexture</c> refuses to
        /// run without palette data, and every glyph here is drawn with an
        /// explicit colour, which takes the palette out of the path. This is
        /// what makes the assertion true.
        /// </summary>
        private static readonly ColorRgba[] _chatPalette =
        {
            new ColorRgba(), new ColorRgba(255, 255, 255, 255)
        };

        private HudObjectInstance? _chatInst;
        private readonly List<(ChatLine Line, float Alpha)> _chatVisible = new();

        /// <summary>
        /// What the prompt says. Real lowercase, at last -- the game's font
        /// would have made this SAYS.
        /// </summary>
        private const string ChatPrompt = "Says: ";

        internal void ModDrawChat()
        {
            if (!ChatBox.Visible)
            {
                return;
            }
            if (_chatInst == null)
            {
                // Built on first use rather than with the rest of the HUD:
                // this costs a texture binding, and most matches never draw a
                // chat line at all. The palette goes on first, because
                // SetCharacterData only builds the texture once both are
                // there.
                _chatInst = new HudObjectInstance(width: ChatFont.Cell, height: ChatFont.Cell);
                _chatInst.SetPaletteData(_chatPalette, _scene);
                _chatInst.SetCharacterData(ChatFont.Pixels, _scene);
                _chatInst.Enabled = true;
            }
            ChatBox.CollectVisible(_chatVisible);
            float aspect = HudAspectFix;
            float x = ChatMargin * aspect;
            // Up from the prompt by however many lines there actually are, so
            // a single message sits just above the prompt and a full log
            // grows towards the middle of the screen. The oldest is drawn
            // first and each one after it a row lower, which is the order
            // CollectVisible hands them over in.
            float y = ChatPromptY - _chatVisible.Count * ChatLineHeight;
            for (int i = 0; i < _chatVisible.Count; i++)
            {
                (ChatLine line, float alpha) = _chatVisible[i];
                bool system = line.Kind == ChatPacket.KindSystem;
                // The trailing space is part of the prefix rather than the
                // message: it is measured with the name, and it survives a
                // message the sender began with one of their own.
                string name = system || line.Name.Length == 0 ? "" : line.Name + ": ";
                float at = ChatDraw(x, y, aspect, alpha, name,
                    system ? ChatSystemInk : ChatName);
                ChatDraw(at, y, aspect, alpha,
                    ChatFit(line.Text, aspect, at - x), system ? ChatSystemInk : ChatInk);
                y += ChatLineHeight;
            }
            if (ChatBox.Composing)
            {
                y = ChatPromptY;
                float at = ChatDraw(x, y, aspect, alpha: 1, ChatPrompt, ChatPromptInk);
                // The tail rather than the head once the line is long: what
                // somebody is typing is what they have just typed, and a
                // prompt that stops moving at the eightieth character looks
                // exactly like a prompt that has stopped taking input.
                //
                // The caret is a plain underscore and does not blink. It comes
                // out of the font like everything else, and something flashing
                // in the corner of a shooter reads as the game trying to tell
                // you about damage.
                ChatDraw(at, y, aspect, alpha: 1,
                    ChatTail(ChatBox.ComposeText + "_", aspect, at - x), ChatInk);
            }
        }

        /// <summary>
        /// One run, left to right from <paramref name="x"/>. Returns where the
        /// pen ended up, so the next run starts there.
        /// </summary>
        private float ChatDraw(float x, float y, float aspect, float alpha,
            ReadOnlySpan<char> text, ColorRgba color)
        {
            HudObjectInstance inst = _chatInst!;
            inst.Alpha = alpha;
            for (int i = 0; i < text.Length; i++)
            {
                int index = ChatFont.Index(text[i]);
                if (index < 0)
                {
                    continue;
                }
                if (text[i] != ' ')
                {
                    inst.PositionX = x / 256f;
                    inst.PositionY = y / 192f;
                    inst.SetData(index, color, _scene);
                    _scene.DrawHudObject(inst, mode: 1, scale: ChatScale);
                }
                x += ChatFont.Widths[index] * ChatScale * aspect;
            }
            return x;
        }

        /// <summary>
        /// How wide a run comes out, in the HUD's own 256-unit x space,
        /// including the aspect correction every horizontal HUD measurement
        /// carries -- a width measured without it is right on a 4:3 window and
        /// wrong on every other one.
        /// </summary>
        private static float ChatWidth(ReadOnlySpan<char> text, float aspect)
        {
            return ChatFont.Measure(text) * ChatScale * aspect;
        }

        /// <summary>The screen's width, less what is already spoken for.</summary>
        private static float ChatRoom(float aspect, float used)
        {
            return 256 - ChatMargin * aspect * 2 - used;
        }

        /// <summary>As much of a received line as fits, from the start.</summary>
        private static string ChatFit(string text, float aspect, float used)
        {
            float room = ChatRoom(aspect, used);
            if (ChatWidth(text, aspect) <= room)
            {
                return text;
            }
            int count = text.Length;
            while (count > 0 && ChatWidth(text.AsSpan(0, count), aspect) > room)
            {
                count--;
            }
            return text[..count];
        }

        /// <summary>As much of the line being typed as fits, from the end.</summary>
        private static string ChatTail(string text, float aspect, float used)
        {
            float room = ChatRoom(aspect, used);
            if (ChatWidth(text, aspect) <= room)
            {
                return text;
            }
            int start = 0;
            while (start < text.Length && ChatWidth(text.AsSpan(start), aspect) > room)
            {
                start++;
            }
            return text[start..];
        }

        /// <summary>
        /// Throw away the difference between where the mouse was when the
        /// prompt opened and where it is now.
        ///
        /// <c>ProcessInput</c> skips the local player entirely while chat has
        /// the keyboard, so their stored mouse position stops advancing --
        /// and aim is a difference between two of those. Without this, the
        /// frame the prompt closes on carries however far the mouse drifted
        /// across the whole message and snaps the view round by all of it.
        /// Both states rather than only the previous one, because the next
        /// frame's delta is taken against whichever of them survives.
        /// </summary>
        internal void ModForgetInputDeltas()
        {
            Input.MouseState = null;
            Input.PrevMouseState = null;
            Input.KeyboardState = null;
            Input.PrevKeyboardState = null;
        }
    }
}

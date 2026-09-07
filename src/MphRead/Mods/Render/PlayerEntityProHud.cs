using System;
using MphRead.Formats;
using MphRead.Hud;
using MphRead.Text;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    /// <summary>
    /// The readouts Pro mode HUD draws in place of the game's own.
    ///
    /// A partial of PlayerEntity for the reason the net HUD and the aim
    /// injection are: the HUD's text and box drawing are private to it, and
    /// reaching them from here costs upstream a handful of call sites in
    /// DrawHudObjects instead of opening the whole HUD up.
    ///
    /// What the stock HUD does with energy and ammo is draw them into the
    /// helmet -- a tank meter along a moulded panel, a number in the visor's
    /// green, both swaying with the idle animation. Pro mode has no helmet to
    /// draw them into, so they are drawn the way a shooter with no helmet
    /// draws them: flat, still, in the corners, in colours that say something
    /// on their own. No sprite assets are involved, only boxes and the game's
    /// own font, so this costs nothing to load and works in any room.
    /// </summary>
    public partial class PlayerEntity
    {
        /// <summary>
        /// Below these fractions of a full tank the readout goes amber, then
        /// red -- 60 and 33 of 99, which is where GetCrosshairColor turns.
        /// </summary>
        private const float ProHudWarn = 60 / 99f;
        private const float ProHudDanger = 33 / 99f;

        /// <summary>
        /// The three states every readout here shares: plenty, getting low,
        /// nearly out. One set of colours for energy and ammo both, so a
        /// glance at either corner means the same thing, and so the two
        /// cannot drift apart into two vocabularies.
        ///
        /// Three, not four: carrying more than a hunter's own tank used to get
        /// a fourth colour of its own, which made the one state you are never
        /// in trouble in the one state the bar changed colour for. Plenty is
        /// plenty, and the number beside the bar says how much.
        /// </summary>
        private static readonly Vector4 ProGood = new Vector4(0.24f, 0.85f, 0.32f, 1);
        private static readonly Vector4 ProWarn = new Vector4(1f, 0.68f, 0.1f, 1);
        private static readonly Vector4 ProDanger = new Vector4(0.95f, 0.18f, 0.18f, 1);

        private static readonly Vector4 ProHudPanel = new Vector4(0, 0, 0, 0.5f);
        private static readonly Vector4 ProHudShade = new Vector4(0, 0, 0, 0.55f);
        private static readonly Vector4 ProHudTrack = new Vector4(1, 1, 1, 0.16f);
        private static readonly ColorRgba ProHudInk = new ColorRgba(235, 238, 245, 255);
        private static readonly ColorRgba ProHudDim = new ColorRgba(178, 186, 200, 255);
        private static readonly ColorRgba ProHudShadow = new ColorRgba(0, 0, 0, 255);

        /// <summary>Energy down the left under the weapon list, ammo down the right, the score up in the corner.</summary>
        private void DrawProHud()
        {
            float aspect = HudAspectFix;
            Vector4 health = ProHealthColor();
            // The left foot of the screen, under the weapon list and the same
            // width as it: score, weapons and energy then read as one column
            // top to bottom, which is one place to look instead of three
            // corners.
            _scene.DrawHudFlatBox(2 * aspect, 170, 46 * aspect, 190, ProHudPanel);
            ProNumber(6 * aspect, 172, Align.Left, _health.ToString(), ProInk(health), 1.5f);
            ProBar(4 * aspect, 186, 40, 3, ProHealthFraction(), health);
            DrawProAmmo();
            ProScore(4 * aspect, 12, Align.Left, 1.1f);
        }

        /// <summary>
        /// The equipped weapon's shots, mirrored into the right foot of the
        /// screen -- the same panel, the same size, the far side.
        ///
        /// The weapon list already carries a number for every weapon, but not
        /// at a size you can read without looking at it, and the one that
        /// matters is the one in your hands. Coloured by how much is left, not
        /// by which weapon it is: drawn in the weapon's own colour first, a
        /// full ten missiles read red, which is the colour every other
        /// readout here uses for "you are nearly out".
        /// </summary>
        /// <summary>
        /// Panel geometry for the ammo corner, in HUD units off the screen's
        /// height. Wider than the energy panel opposite it because it carries
        /// an icon as well as a number, and the number can be three digits:
        /// the Battlehammer costs 4 a shot, so a full pool is 149 of them --
        /// which is 36 units of digits beside a 12-unit icon.
        /// </summary>
        private const float ProAmmoPanelWidth = 58;
        private const float ProAmmoNumberScale = 1.5f;

        private void DrawProAmmo()
        {
            string? ammo = ProAmmoText();
            if (ammo == null)
            {
                return;
            }
            float aspect = HudAspectFix;
            Vector4 color = ProAmmoColor();
            float right = 256 - 2 * aspect;
            float left = right - ProAmmoPanelWidth * aspect;
            _scene.DrawHudFlatBox(left, 170, right, 190, ProHudPanel);
            DrawProAmmoIcon(left + 2 * aspect, 172);
            ProNumber(right - 4 * aspect, 172, Align.Right, ammo, ProInk(color), ProAmmoNumberScale);
            ProBar(left + 2 * aspect, 186, ProAmmoPanelWidth - 4, 3, ProAmmoFraction(), color);
        }

        /// <summary>
        /// The equipped weapon's own icon, beside its number.
        ///
        /// The same instance and the same colour the weapon list draws down
        /// the left -- one icon per weapon, tinted the colour that weapon is
        /// known by -- so the two are obviously the same thing said twice, and
        /// so the texture is not rebuilt: SetData only redraws when the frame
        /// or the colour has actually changed, and asking for the pair the
        /// list has just asked for changes neither.
        ///
        /// Sized to the digits beside it rather than to the panel: at the
        /// number's own height it reads as a label on the number, which is
        /// what it is for. Placed on its ink like the list's are (see
        /// ModIconBounds), because these frames are drawn for the touchscreen
        /// weapon wheel and each sits somewhere different in its own frame.
        /// </summary>
        private void DrawProAmmoIcon(float x, float y)
        {
            int index = (int)CurrentWeapon;
            if (index < 0 || index >= _weaponListIcons.Length)
            {
                return;
            }
            HudObjectInstance icon = _weaponListIcons[index];
            if (icon == null)
            {
                return;
            }
            // A glyph is 8 units tall before scaling, so this is exactly the
            // height of the number it stands next to.
            float side = 8 * ProAmmoNumberScale;
            float aspect = HudAspectFix;
            IconBounds bounds = _weaponListIconBounds[index];
            float scale = side / Math.Max(bounds.Width, bounds.Height);
            icon.SetData(index, _weaponListColors[index], _scene);
            icon.Alpha = Features.HudOpacity;
            // The ink's centre put in the centre of a box `side` across and
            // `side` down -- across being measured off the height too, hence
            // the aspect on every horizontal term.
            icon.PositionX = (x + side * aspect / 2 - bounds.CentreX * scale * aspect) / 256f;
            icon.PositionY = (y + side / 2 - bounds.CentreY * scale) / 192f;
            _scene.DrawHudObject(icon, mode: 1, scale: scale);
        }

        // ------------------------------------------------------------ pieces

        /// <summary>
        /// How full the bar is, where full is a hunter's own energy and not
        /// the most they could possibly be carrying.
        ///
        /// A multiplayer hunter spawns with 99 and can pick up to 199 -- the
        /// stock HUD draws that as two meters, one stacked on the other, for
        /// exactly this reason. Measuring against 199 instead would have a
        /// player who has taken no damage at all looking half dead, which is
        /// what it did first: 99 of 199 is amber.
        /// </summary>
        private float ProHealthFraction()
        {
            return Math.Clamp(_health / (float)ProHealthSpan(), 0, 1);
        }

        private int ProHealthSpan()
        {
            if (GameState.Multiplayer)
            {
                return Math.Max(Values.EnergyTank - 1, 1);
            }
            return Math.Max(_healthMax, 1);
        }

        /// <summary>
        /// Green, amber, red -- the same three, at the same thresholds, the
        /// custom crosshair uses, so the two never disagree about how much
        /// trouble you are in. Above a full tank the bar is simply full and
        /// green: <see cref="ProHealthFraction"/> clamps, so 199 reads as the
        /// best state there is rather than as a state of its own.
        /// </summary>
        private Vector4 ProHealthColor()
        {
            float fraction = ProHealthFraction();
            if (fraction > ProHudWarn)
            {
                return ProGood;
            }
            if (fraction > ProHudDanger)
            {
                return ProWarn;
            }
            return ProDanger;
        }

        private static ColorRgba ProInk(Vector4 color)
        {
            return new ColorRgba((byte)(color.X * 255), (byte)(color.Y * 255), (byte)(color.Z * 255), 255);
        }

        /// <summary>
        /// The equipped weapon's shots left, or null where there is no such
        /// number -- the Power Beam, which costs nothing, and alt form, which
        /// has no gun. Shots, not the ammo pool they are bought from: one
        /// missile costs ten, so the raw figure is wrong by each weapon's own
        /// factor. -1 is the unlimited-ammo marker single-player bots carry.
        /// </summary>
        private string? ProAmmoText()
        {
            if (IsAltForm || IsMorphing || IsUnmorphing)
            {
                return null;
            }
            WeaponInfo info = EquipInfo.Weapon;
            if (info.AmmoCost == 0)
            {
                return null;
            }
            int amount = _ammo[info.AmmoType];
            return amount < 0 ? "--" : (amount / info.AmmoCost).ToString();
        }

        /// <summary>
        /// What counts as a full load, in pool units: the big ammo pickup.
        ///
        /// Not <c>_ammoMax</c>, which is the *cap* -- 599 in multiplayer, six
        /// times what anything hands out at once. Measured against that, a
        /// hunter carrying a perfectly ordinary loadout reads as nearly empty,
        /// and the bar never moves off its left end. What the game actually
        /// gives you is 100 units from a big pickup and 50 from a small one
        /// (250 and 100 in the story), a weapon pickup tops you up to 60, and
        /// multiplayer spawns you with exactly 100 units of missiles -- ten of
        /// them, at ten a shot. So one big pickup is what "full" means here,
        /// and the spawn loadout reads full, which is what it is.
        /// </summary>
        private static int ProAmmoFull => GameState.Multiplayer ? 100 : 250;

        /// <summary>How full the ammo pool is, against <see cref="ProAmmoFull"/>.</summary>
        private float ProAmmoFraction()
        {
            int amount = _ammo[EquipInfo.Weapon.AmmoType];
            if (amount < 0)
            {
                return 1;
            }
            return Math.Clamp(amount / (float)ProAmmoFull, 0, 1);
        }

        /// <summary>
        /// Green down to a small pickup's worth -- half a big one -- then
        /// amber, then red under a fifth of one. The same three colours, at
        /// the same meanings, as the energy beside it.
        ///
        /// In pool units rather than shots, so one rule covers weapons that
        /// cost 4 a shot and weapons that cost 20: a small pickup is the least
        /// the map can hand you, so having less than that is the first thing
        /// worth saying, and a fifth of a big one leaves no weapon in the game
        /// more than a couple of shots. Ten missiles -- what multiplayer spawns
        /// you with -- is a full 100 units and reads green, which is the whole
        /// point of not colouring this by weapon: in the weapon's own colour
        /// a full missile load was red, the one colour that means trouble.
        /// </summary>
        private Vector4 ProAmmoColor()
        {
            int amount = _ammo[EquipInfo.Weapon.AmmoType];
            if (amount < 0 || amount >= ProAmmoFull / 2)
            {
                return ProGood;
            }
            if (amount >= ProAmmoFull / 5)
            {
                return ProWarn;
            }
            return ProDanger;
        }

        /// <summary>
        /// A bar, on a track, in a dark surround. Widths are in units of the
        /// screen's height, like everything else here -- see HudAspectFix --
        /// so the caller states one number and it is the same size on any
        /// window.
        ///
        /// Three layers rather than two because a HUD is drawn over whatever
        /// the room happens to be: a dark track is invisible in a dark
        /// corridor and a pale one is invisible in a lit hall, so the track is
        /// pale, the surround behind it is dark, and the pair of them read
        /// against both.
        /// </summary>
        private void ProBar(float x, float y, float width, float height, float fill, Vector4 color)
        {
            float aspect = HudAspectFix;
            float pad = 1f;
            _scene.DrawHudFlatBox(x - pad * aspect, y - pad,
                x + (width + pad) * aspect, y + height + pad, ProHudShade);
            _scene.DrawHudFlatBox(x, y, x + width * aspect, y + height, ProHudTrack);
            if (fill > 0)
            {
                _scene.DrawHudFlatBox(x, y, x + width * fill * aspect, y + height, color);
            }
        }

        /// <summary>
        /// A readout, with its own shadow under it.
        ///
        /// The stock HUD never needed this: everything it draws sits on the
        /// helmet, which is an opaque backing of its own. With the helmet gone
        /// the numbers are over the room, and a red 15 over red rock is a
        /// number you cannot read at the moment you most need to.
        /// </summary>
        private void ProNumber(float x, float y, Align align, ReadOnlySpan<char> text,
            ColorRgba color, float scale)
        {
            float aspect = HudAspectFix;
            DrawText2D(x + 0.8f * aspect, y + 0.8f, align, palette: 0, text, ProHudShadow, scale: scale);
            DrawText2D(x, y, align, palette: 0, text, color, scale: scale);
        }

        /// <summary>
        /// The mode's own score line, in two parts: what it is called, small,
        /// and what it says, large. The stock HUD draws the same two strings
        /// (see DrawModeScore), which is suppressed while this is on.
        /// </summary>
        private void ProScore(float x, float y, Align align, float scale)
        {
            string label = Strings.GetHudMessage(ProScoreMessageId());
            ProNumber(x, y, align, label, ProHudDim, 0.55f);
            ProNumber(x, y + 8, align, FormatModeScore(MainPlayerIndex), ProHudInk, scale);
        }

        /// <summary>
        /// What this mode calls its score, from the game's own strings -- the
        /// same id each mode's own DrawHud* method passes to DrawModeScore, so
        /// the label does not quietly become "points" in a mode that counts
        /// octoliths.
        /// </summary>
        private static int ProScoreMessageId()
        {
            switch (GameState.Mode)
            {
                case GameMode.Survival:
                case GameMode.SurvivalTeams:
                    return 213; // lives left
                case GameMode.PrimeHunter:
                    return 214; // prime time
                case GameMode.Bounty:
                case GameMode.BountyTeams:
                    return 215; // octoliths
                case GameMode.Capture:
                    return 216; // octoliths
                case GameMode.Defender:
                case GameMode.DefenderTeams:
                    return 217; // ring time
                case GameMode.Nodes:
                case GameMode.NodesTeams:
                    return 218; // points
                default:
                    return 212; // points
            }
        }

    }
}

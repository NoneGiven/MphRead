using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Hud;
using MphRead.Text;

namespace MphRead.Entities
{
    public enum DialogType
    {
        None = -1,
        Overlay = 0,
        Hud = 1,
        Okay = 3,
        Event = 4,
        YesNo = 5,
        Scan = 6
    }

    public enum ConfirmState
    {
        No = 0,
        Yes = 1,
        Okay = 2
    }

    public enum PromptType
    {
        Any = 0,
        ShipHatch = 1,
        GameOver = 2
    }

    public enum EventType
    {
        EnergyTank = 0,
        VoltDriver = 1,
        MissileTank = 2,
        Battlehammer = 3,
        Imperialist = 4,
        Judicator = 5,
        Magmaul = 6,
        ShockCoil = 7,
        OmegaCannon = 8,
        Artifact = 15,
        Octolith = 16,
        UATank = 17
    }

    public partial class PlayerEntity
    {
        public DialogType DialogType { get; set; } = DialogType.None;
        private string? _overlayMessage1 = null;
        private string? _overlayMessage2 = null;
        private readonly char[] _overlayBuffer1 = new char[128];
        private readonly char[] _overlayBuffer2 = new char[512];
        private string? _dialogValue1 = null;
        private string? _dialogValue2 = null;
        private float _overlayTimer = 0;
        private float _dialogCharTimer = 0;
        private int _dialogPalette = 0;
        private float _overlayTextOffsetY = 0;
        private int _prevOverlayCharacters = 0;
        private bool _showDialogConfirm = false;
        private float _dialogConfirmTimer = 0;
        private bool _lastDialogPageSeen = false;
        private int _dialogPageCount = 0;
        private int _dialogPageIndex = 0;
        private readonly int[] _dialogPageLengths = new int[10];
        public ConfirmState DialogConfirmState { get; set; } = ConfirmState.Okay;
        public PromptType DialogPromptType { get; set; } = PromptType.Any;
        private EventType _eventType = EventType.EnergyTank;
        private bool _ignoreClick = false;

        public void ShowDialog(DialogType type, int messageId, int param1 = 0,
            int param2 = 0, string? value1 = null, string? value2 = null)
        {
            // the game doesn't have this check; we need it to prevent crashing on uninitialized members
            // like _messageBoxInst when e.g. an automatic trigger entity tries to display a message
            if (!LoadFlags.TestFlag(LoadFlags.Initial) || _scene.GameMode != GameMode.SinglePlayer || !IsMainPlayer)
            {
                return;
            }

            DialogType = type;
            _silentVisorSwitch = false;
            _dialogValue1 = value1;
            _dialogValue2 = value2;

            bool CheckPrompt()
            {
                if (!IsMainPlayer)
                {
                    CloseDialogs();
                    return false;
                }
                if (ScanVisor && DialogType != DialogType.Scan)
                {
                    _silentVisorSwitch = true;
                    SwitchVisors(reset: false);
                }
                return true;
            }

            if (type == DialogType.Overlay)
            {
                ShowDialogOverlay(messageId, duration: param1, warning: param2 != 0);
            }
            else if (type == DialogType.Hud)
            {
                ShowDialogHud(messageId, duration: param1, unpause: param2 != 0);
            }
            else if (type == DialogType.Okay || type == DialogType.YesNo)
            {
                if (CheckPrompt())
                {
                    ShowDialogPrompt(messageId);
                }
            }
            else if (type == DialogType.Scan)
            {
                if (CheckPrompt())
                {
                    ShowDialogScan();
                }
            }
            else if (type == DialogType.Event)
            {
                if (CheckPrompt())
                {
                    ShowDialogEvent(messageId, eventType: (EventType)param1);
                }
            }
            else
            {
                CloseDialogs();
            }
        }

        private void BufferDialogPages()
        {
            Debug.Assert(_overlayMessage2 != null && _overlayMessage2.Length > 0);
            WrapText(_overlayMessage2, 200, _overlayBuffer2);
            int index = 0;
            int line = 1;
            int page = 0;
            int length = 0;
            char ch = _overlayBuffer2[index];
            while (ch != '\0')
            {
                if (ch == '\n')
                {
                    line++;
                    if (line == 4)
                    {
                        _dialogPageLengths[page++] = length;
                        length = 0;
                        line = 1;
                    }
                    else
                    {
                        length++;
                    }
                }
                else
                {
                    length++;
                }
                if (++index == _overlayBuffer2.Length)
                {
                    break;
                }
                ch = _overlayBuffer2[index];
            }
            _dialogPageLengths[page] = length;
            _dialogPageCount = page + 1;
        }

        private void ShowDialogOverlay(int messageId, int duration, bool warning)
        {
            if (ScanVisor)
            {
                CloseDialogs();
                return;
            }
            StringTableEntry? entry = Strings.GetEntry('M', messageId, StringTables.GameMessages);
            if (entry == null)
            {
                // the game does this after updating the current message
                CloseDialogs();
                return;
            }
            if (_overlayMessage1 != null)
            {
                if (_overlayMessage1 == entry.Value1)
                {
                    _overlayTimer = duration / 30f;
                }
                // the game doesn't return if there's an existing but non-same message
                return;
            }
            Debug.Assert(_overlayMessage2 == null);
            _overlayMessage1 = entry.Value1;
            _overlayMessage2 = null;
            _dialogValue1 = null;
            _dialogValue2 = null;
            Array.Fill(_overlayBuffer1, '\0');
            int lineCount = WrapText(_overlayMessage1, 142, _overlayBuffer1);
            _overlayTextOffsetY = lineCount * 5;
            _overlayTimer = duration / 30f;
            _dialogCharTimer = 0;
            _dialogPalette = warning ? 3 : 0;
            _messageBoxInst.SetPalette(_dialogPalette, _scene);
            _messageSpacerInst.SetPalette(_dialogPalette, _scene);
            _messageBoxInst.SetAnimation(start: 0, target: 65, frames: 66, afterAnim: 65);
        }

        private void ShowDialogHud(int messageId, int duration, bool unpause)
        {
            string message = Strings.GetHudMessage(messageId);
            if (message == null)
            {
                // the game does this after updating the current message
                CloseDialogs();
                return;
            }
            if (_overlayMessage1 != null)
            {
                if (_overlayMessage1 == message)
                {
                    _overlayTimer = duration / 30f;
                }
                // the game doesn't return if there's an existing but non-same message
                return;
            }
            _overlayMessage1 = message;
            _overlayMessage2 = null;
            _dialogValue1 = null;
            _dialogValue2 = null;
            if (unpause)
            {
                GameState.UnpauseDialog();
            }
            Array.Fill(_overlayBuffer1, '\0');
            int lineCount = WrapText(_overlayMessage1, 142, _overlayBuffer1);
            _overlayTextOffsetY = lineCount * 5;
            _overlayTimer = duration / 30f;
            _dialogCharTimer = 9999;
            _prevIntroChars = 9999;
            _dialogPalette = 2;
            _messageBoxInst.SetPalette(_dialogPalette, _scene);
            _messageSpacerInst.SetPalette(_dialogPalette, _scene);
            _messageBoxInst.SetAnimation(start: 0, target: 65, frames: 66, afterAnim: 65);
        }

        private void ShowDialogPrompt(int messageId)
        {
            StringTableEntry? entry = Strings.GetEntry('M', messageId, StringTables.GameMessages);
            if (entry == null)
            {
                CloseDialogs();
                return;
            }
            StopLongSfx();
            bool discard = false;
            EndWeaponMenu(ref discard);
            if (entry.Prefix == 'G') // gunship
            {
                _soundSource.PlayFreeSfx(SfxId.GUNSHIP_TRANSMISSION);
            }
            else if (entry.Prefix == 'H') // hint
            {
                _soundSource.PlayFreeSfx(SfxId.GAME_HINT);
            }
            else if (entry.Prefix == 'T') // telepathy
            {
                _soundSource.StopFreeSfxScripts();
                _soundSource.PlayFreeSfx(SfxId.TELEPATHIC_MESSAGE);
            }
            else if (entry.Prefix == 'B') // secret switches
            {
                _soundSource.PlayFreeSfx(SfxId.GUNSHIP_TRANSMISSION);
                _soundSource.StopFreeSfxScripts();
                _soundSource.PlayFreeSfx(SfxId.TELEPATHIC_MESSAGE);
            }
            GameState.PauseDialog();
            _overlayMessage1 = entry.Value1;
            _overlayMessage2 = entry.Value2;
            Array.Fill(_overlayBuffer1, '\0');
            Array.Fill(_overlayBuffer2, '\0');
            int lineCount = WrapText(_overlayMessage1, 142, _overlayBuffer1);
            _overlayTextOffsetY = lineCount * 5;
            BufferDialogPages();
            _dialogCharTimer = 0;
            _dialogPalette = 0;
            _messageBoxInst.SetPalette(_dialogPalette, _scene);
            _messageSpacerInst.SetPalette(_dialogPalette, _scene);
            _messageBoxInst.SetAnimation(start: 0, target: 65, frames: 66, afterAnim: 65);
        }

        private void ShowDialogScan()
        {
            StopLongSfx();
            bool discard = false;
            EndWeaponMenu(ref discard);
            GameState.PauseDialog();
            _dialogCharTimer = 0;
            _dialogPalette = 0;
            _messageBoxInst.SetPalette(_dialogPalette, _scene);
            _messageSpacerInst.SetPalette(_dialogPalette, _scene);
            _messageBoxInst.SetAnimation(start: 0, target: 65, frames: 66, afterAnim: 65);
        }

        private void ShowDialogEvent(int messageId, EventType eventType)
        {
            StringTableEntry? entry = Strings.GetEntry('M', messageId, StringTables.GameMessages);
            if (entry == null)
            {
                CloseDialogs();
                return;
            }
            StopLongSfx();
            bool discard = false;
            EndWeaponMenu(ref discard);
            GameState.PauseDialog();
            _eventType = eventType;
            _overlayMessage1 = entry.Value1;
            _overlayMessage2 = entry.Value2;
            // todo: avoid allocation
            if (_dialogValue1 != null)
            {
                _overlayMessage1 = _overlayMessage1.Replace("&tab0", _dialogValue1);
                _overlayMessage2 = _overlayMessage2.Replace("&tab0", _dialogValue1);
            }
            if (_dialogValue2 != null)
            {
                _overlayMessage1 = _overlayMessage1.Replace("&tab1", _dialogValue2);
                _overlayMessage2 = _overlayMessage2.Replace("&tab1", _dialogValue2);
            }
            Array.Fill(_overlayBuffer1, '\0');
            Array.Fill(_overlayBuffer2, '\0');
            int lineCount = WrapText(_overlayMessage1, 142, _overlayBuffer1);
            _overlayTextOffsetY = lineCount * 5;
            BufferDialogPages();
            _dialogCharTimer = 0;
            _dialogPalette = 0;
            _messageBoxInst.SetPalette(_dialogPalette, _scene);
            _messageSpacerInst.SetPalette(_dialogPalette, _scene);
            _messageBoxInst.SetAnimation(start: 0, target: 65, frames: 66, afterAnim: 65);
            if (_eventType == EventType.EnergyTank || _eventType == EventType.MissileTank || _eventType == EventType.UATank)
            {
                _soundSource.PlayFreeSfx(SfxId.GET_ITEM);
                _dialogConfirmTimer = 60 / 30f;
                // todo?: it's lame that UA tank is the only pickup without a frame/icon
                if (_eventType != EventType.UATank)
                {
                    _dialogPickupInst.SetIndex((int)_eventType, _scene);
                }
            }
            else if (_eventType >= EventType.VoltDriver && _eventType <= EventType.ShockCoil)
            {
                // mustodo: stop music, play seq
                _soundSource.PlayFreeSfx(SfxId.WEAPON_POWER_UP);
                _dialogConfirmTimer = 150 / 30f;
                _dialogPickupInst.SetIndex((int)_eventType, _scene);
            }
            else if (_eventType == EventType.OmegaCannon || _eventType == EventType.Artifact)
            {
                _soundSource.PlayFreeSfx(SfxId.GET_ITEM2);
                _dialogConfirmTimer = 60 / 30f;
                if (_eventType == EventType.OmegaCannon)
                {
                    // the game doesn't do this because it shares the Omega Cannon event type with artifacts
                    _dialogPickupInst.SetIndex((int)_eventType, _scene);
                }
            }
            else if (_eventType == EventType.Octolith)
            {
                // mustodo: stop music, play seq
                _dialogCrystalInst.SetAnimation(start: 0, target: 35, frames: 36, loop: true);
                _dialogConfirmTimer = 150 / 30f;
            }
        }

        public void CloseDialogs()
        {
            // the game doesn't have this check; we need it to prevent crashing on uninitialized members
            // like _messageBoxInst when e.g. this is called on spawn
            if (!LoadFlags.TestFlag(LoadFlags.Initial) || _scene.GameMode != GameMode.SinglePlayer || !IsMainPlayer)
            {
                return;
            }

            DialogType = DialogType.None;
            _overlayTimer = 0;
            _dialogCharTimer = 0;
            _dialogValue1 = null;
            _dialogValue2 = null;
            _overlayMessage1 = null;
            _overlayMessage2 = null;
            _overlayTextOffsetY = 0;
            _prevOverlayCharacters = 0;
            _showDialogConfirm = false;
            _dialogConfirmTimer = 0;
            _lastDialogPageSeen = false;
            _dialogPageCount = 0;
            _dialogPageIndex = 0;
            for (int i = 0; i < _dialogPageLengths.Length; i++)
            {
                _dialogPageLengths[i] = 0;
            }
            if (_silentVisorSwitch)
            {
                SwitchVisors(reset: false);
            }
            _silentVisorSwitch = false;
            _messageBoxInst.SetIndex(0, _scene);
            _dialogButtonInst.SetIndex(0, _scene);
            _dialogArrowInst.SetIndex(0, _scene);
            _dialogCrystalInst.SetIndex(0, _scene);
            _scene.Layer5Info.BindingId = -1;
        }

        public void UpdateDialogs()
        {
            if (!ScanVisor || DialogType == DialogType.Scan)
            {
                if (DialogType == DialogType.Overlay || DialogType == DialogType.Hud)
                {
                    _overlayTimer -= _scene.FrameTime;
                    if (_overlayTimer <= 0)
                    {
                        CloseDialogs();
                        return;
                    }
                }
                _messageBoxInst.ProcessAnimation(_scene);
                if (_messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f)
                {
                    _dialogCharTimer += _scene.FrameTime;
                }
                if (_messageBoxInst.CurrentFrame >= 5)
                {
                    int spacerIndex = (_messageBoxInst.CurrentFrame & 1) != 0 ? 0 : 1;
                    _messageSpacerInst.SetIndex(spacerIndex, _scene);
                }
            }
            if (GameState.DialogPause && (DialogType == DialogType.Scan
                || _messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f))
            {
                bool closed = false;
                if (_showDialogConfirm)
                {
                    if (DialogType == DialogType.YesNo)
                    {
                        if (CheckButtonPressed(DialogButton.Yes))
                        {
                            closed = true;
                            DialogConfirmState = ConfirmState.Yes;
                            if (DialogPromptType != PromptType.ShipHatch && DialogPromptType != PromptType.GameOver)
                            {
                                CloseDialogs();
                            }
                        }
                        else if (CheckButtonPressed(DialogButton.No))
                        {
                            closed = true;
                            DialogConfirmState = ConfirmState.No;
                            if (DialogPromptType != PromptType.GameOver)
                            {
                                CloseDialogs();
                            }
                        }
                    }
                    else if (CheckButtonPressed(DialogButton.Okay))
                    {
                        closed = true;
                        if (DialogType == DialogType.Event)
                        {
                            // mustodo: restart music, etc.
                            RestartLongSfx();
                        }
                        else if (GameState.DialogPause)
                        {
                            RestartLongSfx();
                        }
                        bool scan = DialogType == DialogType.Scan;
                        _soundSource.PlayFreeSfx(SfxId.SCAN_OK);
                        CloseDialogs();
                        DialogConfirmState = ConfirmState.Okay;
                        GameState.UnpauseDialog();
                        if (scan)
                        {
                            AfterScan();
                        }
                    }
                }
                if (closed)
                {
                    _ignoreClick = true;
                }
                else
                {
                    if (CheckButtonPressed(DialogButton.Right))
                    {
                        if (_dialogPageIndex != _dialogPageCount - 1)
                        {
                            _dialogPageIndex++;
                        }
                    }
                    else if (CheckButtonPressed(DialogButton.Left))
                    {
                        if (_dialogPageIndex != 0)
                        {
                            _dialogPageIndex--;
                        }
                    }
                    if (_dialogConfirmTimer > 0)
                    {
                        _dialogConfirmTimer -= _scene.FrameTime;
                    }
                    if (_dialogPageIndex == _dialogPageCount - 1)
                    {
                        _lastDialogPageSeen = true;
                    }
                    if (_dialogConfirmTimer <= 0 && _lastDialogPageSeen)
                    {
                        if (!_showDialogConfirm)
                        {
                            _dialogButtonInst.SetAnimation(start: 0, target: 2, frames: 3, afterAnim: 2);
                            _dialogArrowInst.SetIndex(0, _scene);
                        }
                        _showDialogConfirm = true;
                    }
                    if (!_showDialogConfirm && _dialogArrowInst.Timer <= 0)
                    {
                        _dialogArrowInst.SetAnimation(start: 0, target: 29, frames: 30, afterAnim: 29, loop: true);
                    }
                    _dialogButtonInst.ProcessAnimation(_scene);
                    _dialogArrowInst.ProcessAnimation(_scene);
                }
                if (DialogType == DialogType.Event && _messageBoxInst.Timer <= 0)
                {
                    _dialogCrystalInst.ProcessAnimation(_scene);
                }
            }
        }

        private void DrawDialogs()
        {
            float baseY = DialogType == DialogType.Event ? 27 : 47;
            if (!ScanVisor && _overlayMessage1 != null)
            {
                float posX = 64 / 256f;
                float posY = baseY / 192f;
                float width = _messageBoxInst.Width / 256f;
                float height = _messageBoxInst.Height / 192f;
                float spacerOffset = 0;
                if (_messageBoxInst.CurrentFrame >= 5)
                {
                    spacerOffset = 16 / 256f;
                    _messageSpacerInst.Alpha = 0.5f;
                    _messageSpacerInst.PositionX = posX + width - spacerOffset;
                    _messageSpacerInst.PositionY = posY;
                    _messageSpacerInst.FlipVertical = false;
                    _scene.DrawHudObject(_messageSpacerInst, mode: 1);
                    _messageSpacerInst.PositionY = posY + height;
                    _messageSpacerInst.FlipVertical = true;
                    _scene.DrawHudObject(_messageSpacerInst, mode: 1);
                }
                float leftPos = posX - spacerOffset;
                float rightPos = posX + spacerOffset + width;
                float topPos = posY;
                float bottomPos = posY + height;
                _messageBoxInst.Alpha = 0.5f;
                _messageBoxInst.PositionX = leftPos;
                _messageBoxInst.PositionY = topPos;
                _messageBoxInst.FlipHorizontal = false;
                _messageBoxInst.FlipVertical = false;
                _scene.DrawHudObject(_messageBoxInst, mode: 1);
                _messageBoxInst.PositionX = rightPos;
                _messageBoxInst.PositionY = topPos;
                _messageBoxInst.FlipHorizontal = true;
                _messageBoxInst.FlipVertical = false;
                _scene.DrawHudObject(_messageBoxInst, mode: 1);
                _messageBoxInst.PositionX = leftPos;
                _messageBoxInst.PositionY = bottomPos;
                _messageBoxInst.FlipHorizontal = false;
                _messageBoxInst.FlipVertical = true;
                _scene.DrawHudObject(_messageBoxInst, mode: 1);
                _messageBoxInst.PositionX = rightPos;
                _messageBoxInst.PositionY = bottomPos;
                _messageBoxInst.FlipHorizontal = true;
                _messageBoxInst.FlipVertical = true;
                _scene.DrawHudObject(_messageBoxInst, mode: 1);
                if (_messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f)
                {
                    _textSpacingY = 10;
                    _textInst.SetPaletteData(_dialogPaletteData, _scene);
                    int characters = (int)(_dialogCharTimer / (1 / 30f));
                    DrawText2D(128, baseY + 34 - _overlayTextOffsetY, Align.PadCenter, _dialogPalette,
                        _overlayBuffer1, maxLength: characters);
                    _textInst.SetPaletteData(_textPaletteData, _scene);
                    _textSpacingY = 0;
                    if (characters > _prevOverlayCharacters
                        && characters <= _overlayMessage1.Length)
                    {
                        _soundSource.PlayFreeSfx(SfxId.LETTER_BLIP);
                        _prevOverlayCharacters = characters;
                    }
                }
            }
            if (DialogType == DialogType.Event && _messageBoxInst.Timer <= 0
                && _messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f)
            {
                if (_eventType <= EventType.OmegaCannon || _eventType == EventType.Octolith || _eventType == EventType.UATank)
                {
                    float posX = 64 + _messageBoxInst.Width - _dialogFrameInst.Width / 2;
                    float posY = baseY + 2 * _messageBoxInst.Height - 12;
                    _dialogFrameInst.Alpha = 0.5f;
                    _dialogFrameInst.PositionX = posX / 256f;
                    _dialogFrameInst.PositionY = posY / 192f;
                    _scene.DrawHudObject(_dialogFrameInst);
                    if (_eventType == EventType.Octolith)
                    {
                        _dialogCrystalInst.PositionX = (posX + 16) / 256f;
                        _dialogCrystalInst.PositionY = posY / 192f;
                        _scene.DrawHudObject(_dialogCrystalInst);
                    }
                    else if (_eventType != EventType.UATank)
                    {
                        _dialogPickupInst.PositionX = (posX + 16) / 256f;
                        _dialogPickupInst.PositionY = (posY + 16) / 192f;
                        _scene.DrawHudObject(_dialogPickupInst);
                    }
                }
            }
            int layerIndex = 4;
            int scanYOffset = 0;
            // langtodo: check all the places text 2d/3d are called from for other offsets
            // langtodo: will also need to do this for AMHK, which is different
            if (DialogType == DialogType.Scan && (Paths.IsMphJapan || Paths.IsMphKorea))
            {
                scanYOffset = -4;
            }
            if (DialogType == DialogType.Scan)
            {
                Debug.Assert(_overlayMessage1 != null);
                // todo?: JP/KR have an empty message here; add a bugfix?
                string text = Strings.GetHudMessage(102); // SCAN COMPLETE
                // the game doesn't apply the X shift, only Y
                DrawText2D(128 + _objShiftX, 58 + _objShiftY, Align.Center, palette: 0, text);
                HudObjectInstance iconInst = _scanIconInsts[_scanCategoryIndex * 2];
                iconInst.PositionX = 20 / 256f;
                iconInst.PositionY = 96 / 192f;
                iconInst.Center = false;
                iconInst.Alpha = 1;
                iconInst.UseMask = false;
                _scene.DrawHudObject(iconInst);
                Debug.Assert(iconInst.PaletteData != null);
                _textInst.SetPaletteData(iconInst.PaletteData, _scene);
                DrawText2D(58, 116 + scanYOffset, Align.Left, palette: 0, _overlayMessage1);
                _textInst.SetPaletteData(_textPaletteData, _scene);
                layerIndex = _scanCategoryLayers[_scanCategoryIndex];
            }
            if (((DialogType == DialogType.Okay || DialogType == DialogType.Event || DialogType == DialogType.YesNo)
                && _messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f) || DialogType == DialogType.Scan)
            {
                int start = 0;
                for (int i = 1; i <= _dialogPageIndex; i++)
                {
                    start += _dialogPageLengths[i - 1];
                }
                start += _dialogPageIndex; // account for trailing newline on each previous page
                var text = new ReadOnlySpan<char>(_overlayBuffer2, start, _dialogPageLengths[_dialogPageIndex]);
                _textInst.SetPaletteData(_dialogPaletteData, _scene);
                DrawText2D(128, 134 + scanYOffset, Align.Center, palette: 0, text);
                _textInst.SetPaletteData(_textPaletteData, _scene);
                _scene.Layer5Info.BindingId = _dialogBindingIds[layerIndex];
                _scene.Layer5Info.Alpha = 1;
                _scene.Layer5Info.ScaleX = 1;
                _scene.Layer5Info.ScaleY = 1;
                if (_dialogPageIndex != _dialogPageCount - 1)
                {
                    _dialogArrowInst.PositionX = 169 / 256f;
                    _dialogArrowInst.PositionY = 173 / 192f;
                    _dialogArrowInst.FlipHorizontal = false;
                    _scene.DrawHudObject(_dialogArrowInst);
                }
                if (_dialogPageIndex != 0)
                {
                    _dialogArrowInst.PositionX = 55 / 256f;
                    _dialogArrowInst.PositionY = 173 / 192f;
                    _dialogArrowInst.FlipHorizontal = true;
                    _scene.DrawHudObject(_dialogArrowInst);
                }
                if (_showDialogConfirm)
                {
                    float posX = 112;
                    float posY = 174;
                    if (DialogType == DialogType.YesNo)
                    {
                        _dialogButtonInst.PositionX = (posX - _dialogButtonInst.Width) / 256f;
                        _dialogButtonInst.PositionY = posY / 192f;
                        _scene.DrawHudObject(_dialogButtonInst);
                        _dialogButtonInst.PositionX = (posX + _dialogButtonInst.Width) / 256f;
                        _dialogButtonInst.PositionY = posY / 192f;
                        _scene.DrawHudObject(_dialogButtonInst);
                        text = Strings.GetHudMessage(105); // yes
                        float textPosX = posX - _dialogButtonInst.Width / 2;
                        DrawText2D(textPosX, posY + 5, Align.Center, palette: 0, text);
                        text = Strings.GetHudMessage(106); // no
                        textPosX = posX + _dialogButtonInst.Width * 1.5f + 1;
                        DrawText2D(textPosX, posY + 5, Align.Center, palette: 0, text);
                    }
                    else
                    {
                        _dialogButtonInst.PositionX = posX / 256f;
                        _dialogButtonInst.PositionY = posY / 192f;
                        _scene.DrawHudObject(_dialogButtonInst);
                        text = Strings.GetHudMessage(104); // ok
                        DrawText2D(posX + _dialogButtonInst.Width / 2 + 1, posY + 5, Align.Center, palette: 0, text);
                    }
                }
            }
        }

        private bool CheckButtonPressed(DialogButton type)
        {
            if (Input.ClickX >= 0 && Input.ClickY >= 0)
            {
                float clickX = Input.ClickX / _scene.Size.X;
                float clickY = Input.ClickY / _scene.Size.Y;
                ButtonInfo info = _buttonInfo[(int)type];
                if (clickX >= info.Left && clickX < info.Right && clickY >= info.Top && clickY < info.Bottom)
                {
                    return true;
                }
            }
            return false;
        }

        private readonly struct ButtonInfo
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Top;
            public readonly float Bottom;

            public ButtonInfo(float left, float right, float top, float bottom)
            {
                Left = left / 256f;
                Right = right / 256f;
                Top = top / 192f;
                Bottom = bottom / 192f;
            }
        }

        private enum DialogButton
        {
            Okay = 0,
            Yes = 1,
            No = 2,
            Left = 3,
            Right = 4
        }

        private static readonly IReadOnlyList<ButtonInfo> _buttonInfo = new ButtonInfo[5]
        {
            new ButtonInfo(left: 111, right: 145, top: 173, bottom: 191),
            new ButtonInfo(left: 79, right: 113, top: 173, bottom: 191),
            new ButtonInfo(left: 143, right: 177, top: 173, bottom: 191),
            new ButtonInfo(left: 55, right: 88, top: 172, bottom: 190),
            new ButtonInfo(left: 168, right: 201, top: 172, bottom: 190)
        };
    }
}

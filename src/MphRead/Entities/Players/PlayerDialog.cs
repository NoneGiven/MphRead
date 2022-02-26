using System;
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
        YesNo = 5
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
        private bool _showDialogClose = false;
        private int _dialogPageCount = 0;
        private int _dialogPageIndex = 0;
        private readonly int[] _dialogPageLengths = new int[10];

        public void ShowDialog(DialogType type, int messageId, int param1 = 0,
            int param2 = 0, string? value1 = null, string? value2 = null)
        {
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
                if (ScanVisor)
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
            else if (type == DialogType.Event)
            {
                if (CheckPrompt())
                {
                    ShowDialogEvent(messageId, eventType: param1);
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
                GameState.DialogPause = false;
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
            GameState.DialogPause = true;
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
        }

        private void ShowDialogEvent(int messageId, int eventType)
        {

        }

        public void CloseDialogs()
        {
            DialogType = DialogType.None;
            _overlayTimer = 0;
            _dialogCharTimer = 0;
            _dialogValue1 = null;
            _dialogValue2 = null;
            _overlayMessage1 = null;
            _overlayMessage2 = null;
            _overlayTextOffsetY = 0;
            _prevOverlayCharacters = 0;
            _showDialogClose = false;
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
        }

        public void UpdateDialogs()
        {
            if (!ScanVisor)
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
            // diagtodo: lots more stuff
        }

        private void DrawDialogs()
        {
            if (!ScanVisor && _overlayMessage1 != null)
            {
                float posX = 64 / 256f;
                float posY = 47 / 192f;
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
                if (DialogType == DialogType.Event && _messageBoxInst.Timer <= 0)
                {
                    // diagtodo: draw frame and icon
                }
                if (_messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f)
                {
                    _textSpacingY = 10;
                    _textInst.SetPaletteData(_dialogPaletteData, _scene);
                    int characters = (int)(_dialogCharTimer / (1 / 30f));
                    DrawText2D(128, 81 - _overlayTextOffsetY, Align.PadCenter, _dialogPalette,
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
            if (DialogType == DialogType.Okay || DialogType == DialogType.Event || DialogType == DialogType.YesNo)
            {
                // diagtodo: use this to draw scan stuff too
                // diagtodo: move the dialog up one tile for scan, and some number of tiles for events w/ icon
                if (_messageBoxInst.Time - _messageBoxInst.Timer >= 16 / 30f)
                {
                    int start = 0;
                    for (int i = 1; i <= _dialogPageIndex; i++)
                    {
                        start += _dialogPageLengths[i - 1];
                    }
                    var text = new ReadOnlySpan<char>(_overlayBuffer2, start, _dialogPageLengths[_dialogPageIndex]);
                    _textInst.SetPaletteData(_dialogPaletteData, _scene);
                    DrawText2D(128, 134, Align.Center, palette: 0, text);
                    _textInst.SetPaletteData(_textPaletteData, _scene);
                    _scene.Layer5Info.BindingId = _dialogBindingIds[1]; // diagtodo: palette selection for scan
                    _scene.Layer5Info.Alpha = 1;
                    _scene.Layer5Info.ScaleX = 1;
                    _scene.Layer5Info.ScaleY = 1;
                }
                // sktodo: draw buttons
            }
        }
    }
}

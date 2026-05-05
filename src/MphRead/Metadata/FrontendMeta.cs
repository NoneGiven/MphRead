using System.Collections.Frozen;

namespace MphRead
{
    public static partial class Metadata
    {
        // only in A76E
        public static readonly ModelMetadata Ad2Dm2
            = new ModelMetadata("ad2_dm2", dir: MetaDir.Stage);

        public static readonly FrozenDictionary<string, ModelMetadata> HudModels
            = Frozen.Create<string, ModelMetadata>(
            [
                // cockpit
                new(
                    "unit1_land_cockpit",
                    new ModelMetadata("unit1_land_cockpit", @"hud\unit1_land_cockpit_model.bin", null, null)
                ),
                new(
                    "unit2_land_cockpit",
                    new ModelMetadata("unit2_land_cockpit", @"hud\unit2_land_cockpit_model.bin", null, null)
                ),
                new (
                    "unit3_land_cockpit",
                    new ModelMetadata("unit3_land_cockpit", @"hud\unit3_land_cockpit_model.bin", null, null)
                ),
                new(
                    "unit4_land_cockpit",
                    new ModelMetadata("unit4_land_cockpit", @"hud\unit4_land_cockpit_model.bin", null, null)
                ),
                new(
                    "gorea_land_cockpit",
                    new ModelMetadata("gorea_land_cockpit", @"hud\gorea_land_cockpit_model.bin", null, null)
                ),
                // nav rooms
                new(
                    "unit1_1nav",
                    new ModelMetadata("unit1_1nav", @"hud\unit1_1nav_model.bin", null, null)
                ),
                new(
                    "unit1_2nav",
                    new ModelMetadata("unit1_2nav", @"hud\unit1_2nav_model.bin", null, null)
                ),
                new(
                    "unit2_1nav",
                    new ModelMetadata("unit2_1nav", @"hud\unit2_1NAV_Model.bin", null, null)
                ),
                new(
                    "unit2_2nav",
                    new ModelMetadata("unit2_2nav", @"hud\unit2_2nav_model.bin", null, null)
                ),
                new(
                    "unit3_1nav",
                    new ModelMetadata("unit3_1nav", @"hud\unit3_1nav_model.bin", null, null)
                ),
                new(
                    "unit3_2nav",
                    new ModelMetadata("unit3_2nav", @"hud\unit3_2nav_model.bin", null, null)
                ),
                new(
                    "unit4_1nav",
                    new ModelMetadata("unit4_1nav", @"hud\unit4_1nav_model.bin", null, null)
                ),
                new(
                    "Door_NAV",
                    new ModelMetadata("Door_NAV", dir: MetaDir.Hud)
                ),
                new(
                    "PlayerPos_NAV",
                    new ModelMetadata("PlayerPos_NAV", dir: MetaDir.Hud, anim: "PlayerPos")
                ),
                new(
                    "damage",
                    new ModelMetadata("damage", dir: MetaDir.Hud)
                ),
                new(
                    "icons",
                    new ModelMetadata("icons", texturePath: @"models\icons_Tex.bin", dir: MetaDir.Hud)
                ),
                new(
                    "hud_icon_arrow",
                    new ModelMetadata("hud_icon_arrow", dir: MetaDir.Hud)
                ),
                new(
                    "hud_icon_nodes",
                    new ModelMetadata("hud_icon_nodes", dir: MetaDir.Hud)
                ),
                new(
                    "hud_icon_octolith",
                    new ModelMetadata("hud_icon_octolith", dir: MetaDir.Hud)
                ),
                new(
                    "hud_icon_player",
                    new ModelMetadata("hud_icon_player", dir: MetaDir.Hud)
                )
            ]);

        public static readonly FrozenDictionary<string, ModelMetadata> TouchToStartModels
            = Frozen.Create<string, ModelMetadata>(
            [
                // touchtostart
                new(
                    "touch_bg",
                    new ModelMetadata("touch_bg", dir: MetaDir.TouchToStart)
                )
            ]);

        public static readonly FrozenDictionary<string, ModelMetadata> MultiplayerModels
            = Frozen.Create<string, ModelMetadata>(
            [
                // multiplayer
                new(
                    "bigdeathmatch",
                    new ModelMetadata("bigdeathmatch", dir: MetaDir.Multiplayer)
                ),
                new(
                    "bounty",
                    new ModelMetadata("bounty", dir: MetaDir.Multiplayer)
                ),
                new(
                    "capture",
                    new ModelMetadata("capture", dir: MetaDir.Multiplayer)
                ),
                new(
                    "challenge",
                    new ModelMetadata("challenge", dir: MetaDir.Multiplayer)
                ),
                new(
                    "friendconfig",
                    new ModelMetadata("friendconfig", dir: MetaDir.Multiplayer)
                ),
                new(
                    "med_highlight",
                    new ModelMetadata("med_highlight", dir: MetaDir.Multiplayer)
                ),
                new(
                    "multiplayer",
                    new ModelMetadata("multiplayer", dir: MetaDir.Multiplayer)
                ),
                new(
                    "nodes",
                    new ModelMetadata("nodes", dir: MetaDir.Multiplayer)
                ),
                new(
                    "primehunter",
                    new ModelMetadata("primehunter", dir: MetaDir.Multiplayer)
                ),
                new(
                    "rivalradar",
                    new ModelMetadata("rivalradar", dir: MetaDir.Multiplayer)
                ),
                new(
                    "singlecart",
                    new ModelMetadata("singlecart", dir: MetaDir.Multiplayer)
                ),
                new(
                    "survival",
                    new ModelMetadata("survival", dir: MetaDir.Multiplayer)
                ),
                new(
                    "wificonfig",
                    new ModelMetadata("wificonfig", dir: MetaDir.Multiplayer)
                ),
                new(
                    "wifi",
                    new ModelMetadata("wifi", dir: MetaDir.Multiplayer)
                )
            ]);

        public static readonly FrozenDictionary<string, ModelMetadata> LogoModels
            = Frozen.Create<string, ModelMetadata>(
            [
                // logos
                new(
                    "flare",
                    new ModelMetadata("flare", dir: MetaDir.Logo)
                ),
                new(
                    "flare2",
                    new ModelMetadata("flare2", dir: MetaDir.Logo)
                ),
                new(
                    "flare3",
                    new ModelMetadata("flare3", dir: MetaDir.Logo)
                ),
                new(
                    "logo1",
                    new ModelMetadata("logo1", dir: MetaDir.Logo)
                ),
                new(
                    "logo2",
                    new ModelMetadata("logo2", dir: MetaDir.Logo)
                ),
                new(
                    "logo3",
                    new ModelMetadata("logo3", dir: MetaDir.Logo)
                ),
                new(
                    "logos",
                    new ModelMetadata("logos", dir: MetaDir.Logo)
                ),
                new(
                    "name",
                    new ModelMetadata("name", dir: MetaDir.Logo)
                ),
                new(
                    "nameflare",
                    new ModelMetadata("nameflare", dir: MetaDir.Logo)
                ),
                new(
                    "nst",
                    new ModelMetadata("nst", dir: MetaDir.Logo)
                ),
                new(
                    "whitelogo",
                    new ModelMetadata("whitelogo", dir: MetaDir.Logo)
                )
            ]);

        // sktodo: multiple animation files
        public static readonly FrozenDictionary<string, ModelMetadata> FrontendModels
            = Frozen.Create<string, ModelMetadata>(
            [
                // characterselect
                new(
                    "big_kanden",
                    new ModelMetadata("big_kanden", dir: MetaDir.CharSelect)
                ),
                new(
                    "big_noxus",
                    new ModelMetadata("big_noxus", dir: MetaDir.CharSelect)
                ),
                new(
                    "big_samus",
                    new ModelMetadata("big_samus", dir: MetaDir.CharSelect)
                ),
                new(
                    "big_spire",
                    new ModelMetadata("big_spire", dir: MetaDir.CharSelect)
                ),
                new(
                    "big_sylux",
                    new ModelMetadata("big_sylux", dir: MetaDir.CharSelect)
                ),
                new(
                    "big_trace",
                    new ModelMetadata("big_trace", dir: MetaDir.CharSelect)
                ),
                new(
                    "big_weavel",
                    new ModelMetadata("big_weavel", dir: MetaDir.CharSelect)
                ),
                new(
                    "character_grid",
                    new ModelMetadata("character_grid", dir: MetaDir.CharSelect)
                ),
                new(
                    "kandenoff",
                    new ModelMetadata("kandenoff", dir: MetaDir.CharSelect)
                ),
                new(
                    "kanden",
                    new ModelMetadata("kanden", dir: MetaDir.CharSelect)
                ),
                new(
                    "noxusoff",
                    new ModelMetadata("noxusoff", dir: MetaDir.CharSelect)
                ),
                new(
                    "noxus",
                    new ModelMetadata("noxus", dir: MetaDir.CharSelect)
                ),
                new(
                    "samus",
                    new ModelMetadata("samus", dir: MetaDir.CharSelect)
                ),
                new(
                    "spireoff",
                    new ModelMetadata("spireoff", dir: MetaDir.CharSelect)
                ),
                new(
                    "spire",
                    new ModelMetadata("spire", dir: MetaDir.CharSelect)
                ),
                new(
                    "syluxoff",
                    new ModelMetadata("syluxoff", dir: MetaDir.CharSelect)
                ),
                new(
                    "sylux",
                    new ModelMetadata("sylux", dir: MetaDir.CharSelect)
                ),
                new(
                    "traceoff",
                    new ModelMetadata("traceoff", dir: MetaDir.CharSelect)
                ),
                new(
                    "trace",
                    new ModelMetadata("trace", dir: MetaDir.CharSelect)
                ),
                new(
                    "weaveloff",
                    new ModelMetadata("weaveloff", dir: MetaDir.CharSelect)
                ),
                new(
                    "weavel",
                    new ModelMetadata("weavel", dir: MetaDir.CharSelect)
                ),
                // createjoin
                new(
                    "create",
                    new ModelMetadata("create", dir: MetaDir.CreateJoin)
                ),
                new(
                    "join_highlight",
                    new ModelMetadata("join_highlight", dir: MetaDir.CreateJoin)
                ),
                new(
                    "join",
                    new ModelMetadata("join", dir: MetaDir.CreateJoin)
                ),
                new(
                    "redbar",
                    new ModelMetadata("redbar", dir: MetaDir.CreateJoin)
                ),
                // gameoptions
                new(
                    "arrows_option",
                    new ModelMetadata("arrows_option", dir: MetaDir.GameOption)
                ),
                new(
                    "arrows_type",
                    new ModelMetadata("arrows_type", dir: MetaDir.GameOption)
                ),
                new(
                    "box_arrowsdouble",
                    new ModelMetadata("box_arrowsdouble", dir: MetaDir.GameOption)
                ),
                new(
                    "box_arrows",
                    new ModelMetadata("box_arrows", dir: MetaDir.GameOption)
                ),
                new(
                    "box_type",
                    new ModelMetadata("box_type", dir: MetaDir.GameOption)
                ),
                new(
                    "cancel",
                    new ModelMetadata("cancel", dir: MetaDir.GameOption)
                ),
                new(
                    "chat",
                    new ModelMetadata("chat", dir: MetaDir.GameOption)
                ),
                new(
                    "connected",
                    new ModelMetadata("connected", dir: MetaDir.GameOption)
                ),
                new(
                    "empty",
                    new ModelMetadata("empty", dir: MetaDir.GameOption)
                ),
                new(
                    "headphones",
                    new ModelMetadata("headphones", dir: MetaDir.GameOption)
                ),
                new(
                    "highlight_arrowleft",
                    new ModelMetadata("highlight_arrowleft", dir: MetaDir.GameOption)
                ),
                new(
                    "highlight_arrowright",
                    new ModelMetadata("highlight_arrowright", dir: MetaDir.GameOption)
                ),
                new(
                    "ok",
                    new ModelMetadata("ok", dir: MetaDir.GameOption)
                ),
                new(
                    "playmask",
                    new ModelMetadata("playmask", dir: MetaDir.GameOption)
                ),
                new(
                    "play",
                    new ModelMetadata("play", dir: MetaDir.GameOption)
                ),
                new(
                    "stereo",
                    new ModelMetadata("stereo", dir: MetaDir.GameOption)
                ),
                new(
                    "surround",
                    new ModelMetadata("surround", dir: MetaDir.GameOption)
                ),
                new(
                    "wifiicon",
                    new ModelMetadata("wifiicon", dir: MetaDir.GameOption)
                ),
                new(
                    "wifionlineicon",
                    new ModelMetadata("wifionlineicon", dir: MetaDir.GameOption)
                ),
                // gamerscard
                new(
                    "alimbicswirl",
                    new ModelMetadata("alimbicswirl", dir: MetaDir.GamersCard)
                ),
                new(
                    "blackstar",
                    new ModelMetadata("blackstar", dir: MetaDir.GamersCard)
                ),
                new(
                    "bronzemedal",
                    new ModelMetadata("bronzemedal", dir: MetaDir.GamersCard)
                ),
                new(
                    "bronzestar",
                    new ModelMetadata("bronzestar", dir: MetaDir.GamersCard)
                ),
                new(
                    "cardbgEURO",
                    new ModelMetadata("cardbgEURO", dir: MetaDir.GamersCard)
                ),
                new(
                    "cardbgJAP",
                    new ModelMetadata("cardbgJAP", dir: MetaDir.GamersCard)
                ),
                new(
                    "cardbgUS",
                    new ModelMetadata("cardbgUS", dir: MetaDir.GamersCard)
                ),
                new(
                    "cardbg",
                    new ModelMetadata("cardbg", dir: MetaDir.GamersCard)
                ),
                new(
                    "cardheadergold",
                    new ModelMetadata("cardheadergold", dir: MetaDir.GamersCard)
                ),
                new(
                    "cardmessage",
                    new ModelMetadata("cardmessage", dir: MetaDir.GamersCard)
                ),
                new(
                    "frame200gold",
                    new ModelMetadata("frame200gold", dir: MetaDir.GamersCard)
                ),
                new(
                    "frame200",
                    new ModelMetadata("frame200", dir: MetaDir.GamersCard)
                ),
                new(
                    "goldmedal",
                    new ModelMetadata("goldmedal", dir: MetaDir.GamersCard)
                ),
                new(
                    "goldstar",
                    new ModelMetadata("goldstar", dir: MetaDir.GamersCard)
                ),
                new(
                    "kandenmost",
                    new ModelMetadata("kandenmost", dir: MetaDir.GamersCard)
                ),
                new(
                    "lricons",
                    new ModelMetadata("lricons", dir: MetaDir.GamersCard)
                ),
                new(
                    "noxmost",
                    new ModelMetadata("noxmost", dir: MetaDir.GamersCard)
                ),
                new(
                    "octolith",
                    new ModelMetadata("octolith", dir: MetaDir.GamersCard)
                ),
                new(
                    "redstar",
                    new ModelMetadata("redstar", dir: MetaDir.GamersCard)
                ),
                new(
                    "samusmost",
                    new ModelMetadata("samusmost", dir: MetaDir.GamersCard)
                ),
                new(
                    "silvermedal",
                    new ModelMetadata("silvermedal", dir: MetaDir.GamersCard)
                ),
                new(
                    "silverstar",
                    new ModelMetadata("silverstar", dir: MetaDir.GamersCard)
                ),
                new(
                    "spiremost",
                    new ModelMetadata("spiremost", dir: MetaDir.GamersCard)
                ),
                new(
                    "syluxmost",
                    new ModelMetadata("syluxmost", dir: MetaDir.GamersCard)
                ),
                new(
                    "tab1",
                    new ModelMetadata("tab1", dir: MetaDir.GamersCard)
                ),
                new(
                    "tab2",
                    new ModelMetadata("tab2", dir: MetaDir.GamersCard)
                ),
                new(
                    "tab3",
                    new ModelMetadata("tab3", dir: MetaDir.GamersCard)
                ),
                new(
                    "tab4",
                    new ModelMetadata("tab4", dir: MetaDir.GamersCard)
                ),
                new(
                    "tab5",
                    new ModelMetadata("tab5", dir: MetaDir.GamersCard)
                ),
                new(
                    "textarea",
                    new ModelMetadata("textarea", dir: MetaDir.GamersCard)
                ),
                new(
                    "tracemost",
                    new ModelMetadata("tracemost", dir: MetaDir.GamersCard)
                ),
                new(
                    "weavelmost",
                    new ModelMetadata("weavelmost", dir: MetaDir.GamersCard)
                ),
                // keyboard
                new(
                    "EURO",
                    new ModelMetadata("EURO", dir: MetaDir.Keyboard)
                ),
                new(
                    "hl_key",
                    new ModelMetadata("hl_key", dir: MetaDir.Keyboard)
                ),
                new(
                    "JAP_1",
                    new ModelMetadata("JAP_1", dir: MetaDir.Keyboard)
                ),
                new(
                    "JAP_2",
                    new ModelMetadata("JAP_2", dir: MetaDir.Keyboard)
                ),
                new(
                    "keyboardmask",
                    new ModelMetadata("keyboardmask", dir: MetaDir.Keyboard)
                ),
                new(
                    "keytoggle",
                    new ModelMetadata("keytoggle", dir: MetaDir.Keyboard)
                ),
                new(
                    "messagebox",
                    new ModelMetadata("messagebox", dir: MetaDir.Keyboard)
                ),
                new(
                    "US_caps",
                    new ModelMetadata("US_caps", dir: MetaDir.Keyboard)
                ),
                new(
                    "US_lower",
                    new ModelMetadata("US_lower", dir: MetaDir.Keyboard)
                ),
                new(
                    "US_upper",
                    new ModelMetadata("US_upper", dir: MetaDir.Keyboard)
                ),
                // keypad
                new(
                    "numback",
                    new ModelMetadata("numback", dir: MetaDir.Keypad)
                ),
                new(
                    "numkey",
                    new ModelMetadata("numkey", dir: MetaDir.Keypad)
                ),
                // movieplayer
                new(
                    "cover",
                    new ModelMetadata("cover", dir: MetaDir.MoviePlayer)
                ),
                new(
                    "hotspot",
                    new ModelMetadata("hotspot", dir: MetaDir.MoviePlayer)
                ),
                new(
                    "thumbnails1",
                    new ModelMetadata("thumbnails1", dir: MetaDir.MoviePlayer)
                ),
                new(
                    "thumbnails2",
                    new ModelMetadata("thumbnails2", dir: MetaDir.MoviePlayer)
                ),
                // multimaster
                new(
                    "bluedot",
                    new ModelMetadata("bluedot", dir: MetaDir.MultiMaster)
                ),
                new(
                    "tab",
                    new ModelMetadata("tab", dir: MetaDir.MultiMaster)
                ),
                new(
                    "topsettings",
                    new ModelMetadata("topsettings", dir: MetaDir.MultiMaster)
                ),
                new(
                    "topstatus",
                    new ModelMetadata("topstatus", dir: MetaDir.MultiMaster)
                ),
                // pax_controls
                new(
                    "dmleft",
                    new ModelMetadata("dmleft", dir: MetaDir.PaxControls)
                ),
                new(
                    "dmright",
                    new ModelMetadata("dmright", dir: MetaDir.PaxControls)
                ),
                new(
                    "selectAoff",
                    new ModelMetadata("selectAoff", dir: MetaDir.PaxControls)
                ),
                new(
                    "selectAon",
                    new ModelMetadata("selectAon", dir: MetaDir.PaxControls)
                ),
                new(
                    "stylusleft",
                    new ModelMetadata("stylusleft", dir: MetaDir.PaxControls)
                ),
                new(
                    "stylusright",
                    new ModelMetadata("stylusright", dir: MetaDir.PaxControls)
                ),
                // popup
                new(
                    "popup",
                    new ModelMetadata("popup", dir: MetaDir.Popup)
                ),
                // results
                new(
                    "blackarrowleft",
                    new ModelMetadata("blackarrowleft", dir: MetaDir.Results)
                ),
                new(
                    "blackarrowright",
                    new ModelMetadata("blackarrowright", dir: MetaDir.Results)
                ),
                new(
                    "crossbar",
                    new ModelMetadata("crossbar", dir: MetaDir.Results)
                ),
                new(
                    "lightning",
                    new ModelMetadata("lightning", dir: MetaDir.Results)
                ),
                new(
                    "medalbg",
                    new ModelMetadata("medalbg", dir: MetaDir.Results)
                ),
                new(
                    "medalbronze",
                    new ModelMetadata("medalbronze", dir: MetaDir.Results)
                ),
                new(
                    "medalgold",
                    new ModelMetadata("medalgold", dir: MetaDir.Results)
                ),
                new(
                    "medallast",
                    new ModelMetadata("medallast", dir: MetaDir.Results)
                ),
                new(
                    "medalsilver",
                    new ModelMetadata("medalsilver", dir: MetaDir.Results)
                ),
                new(
                    "playagain",
                    new ModelMetadata("playagain", dir: MetaDir.Results)
                ),
                new(
                    "quit",
                    new ModelMetadata("quit", dir: MetaDir.Results)
                ),
                new(
                    "results",
                    new ModelMetadata("results", dir: MetaDir.Results)
                ),
                new(
                    "rivalboxoff",
                    new ModelMetadata("rivalboxoff", dir: MetaDir.Results)
                ),
                new(
                    "rivalboxon",
                    new ModelMetadata("rivalboxon", dir: MetaDir.Results)
                ),
                new(
                    "teamdivide",
                    new ModelMetadata("teamdivide", dir: MetaDir.Results)
                ),
                new(
                    "topbar",
                    new ModelMetadata("topbar", dir: MetaDir.Results)
                ),
                new(
                    "wincondition",
                    new ModelMetadata("wincondition", dir: MetaDir.Results)
                ),
                // sc_startgame
                new(
                    "dssystem",
                    new ModelMetadata("dssystem", dir: MetaDir.ScStartGame)
                ),
                new(
                    "readybox",
                    new ModelMetadata("readybox", dir: MetaDir.ScStartGame)
                ),
                // startgame
                new(
                    "adbot",
                    new ModelMetadata("adbot", dir: MetaDir.StartGame)
                ),
                new(
                    "botminus",
                    new ModelMetadata("botminus", dir: MetaDir.StartGame)
                ),
                new(
                    "chatout",
                    new ModelMetadata("chatout", dir: MetaDir.StartGame)
                ),
                new(
                    "choicekanden",
                    new ModelMetadata("choicekanden", dir: MetaDir.StartGame)
                ),
                new(
                    "choicenoxus",
                    new ModelMetadata("choicenoxus", dir: MetaDir.StartGame)
                ),
                new(
                    "choicesamus",
                    new ModelMetadata("choicesamus", dir: MetaDir.StartGame)
                ),
                new(
                    "choicespire",
                    new ModelMetadata("choicespire", dir: MetaDir.StartGame)
                ),
                new(
                    "choicesylux",
                    new ModelMetadata("choicesylux", dir: MetaDir.StartGame)
                ),
                new(
                    "choicetrace",
                    new ModelMetadata("choicetrace", dir: MetaDir.StartGame)
                ),
                new(
                    "choiceweavel",
                    new ModelMetadata("choiceweavel", dir: MetaDir.StartGame)
                ),
                new(
                    "playergrid",
                    new ModelMetadata("playergrid", dir: MetaDir.StartGame)
                ),
                new(
                    "player_highlight",
                    new ModelMetadata("player_highlight", dir: MetaDir.StartGame)
                ),
                new(
                    "spicy_1",
                    new ModelMetadata("spicy_1", dir: MetaDir.StartGame)
                ),
                new(
                    "spicy_2",
                    new ModelMetadata("spicy_2", dir: MetaDir.StartGame)
                ),
                new(
                    "spicy_3",
                    new ModelMetadata("spicy_3", dir: MetaDir.StartGame)
                ),
                new(
                    "startgame_highlight",
                    new ModelMetadata("startgame_highlight", dir: MetaDir.StartGame)
                ),
                new(
                    "startgame",
                    new ModelMetadata("startgame", dir: MetaDir.StartGame)
                ),
                new(
                    "team_blue",
                    new ModelMetadata("team_blue", dir: MetaDir.StartGame)
                ),
                new(
                    "team_red",
                    new ModelMetadata("team_red", dir: MetaDir.StartGame)
                ),
                // tostart
                new(
                    "logofinal",
                    new ModelMetadata("logofinal", dir: MetaDir.ToStart)
                ),
                // touchtostart_2
                new(
                    "splashers",
                    new ModelMetadata("splashers", dir: MetaDir.TouchToStart2)
                ),
                new(
                    "touch_bg",
                    new ModelMetadata("touch_bg", dir: MetaDir.TouchToStart2)
                ),
                // wifi_createjoin
                new(
                    "splitter",
                    new ModelMetadata("splitter", dir: MetaDir.WifiCreate)
                ),
                new(
                    "wififriend",
                    new ModelMetadata("wififriend", dir: MetaDir.WifiCreate)
                ),
                new(
                    "wifijoin",
                    new ModelMetadata("wifijoin", dir: MetaDir.WifiCreate)
                ),
                // wifi_games
                new(
                    "bigpanel",
                    new ModelMetadata("bigpanel", dir: MetaDir.WifiGames)
                ),
                new(
                    "creategame",
                    new ModelMetadata("creategame", dir: MetaDir.WifiGames)
                ),
                new(
                    "downarrow",
                    new ModelMetadata("downarrow", dir: MetaDir.WifiGames)
                ),
                new(
                    "friendbar_long",
                    new ModelMetadata("friendbar_long", dir: MetaDir.WifiGames)
                ),
                new(
                    "friendbar_short",
                    new ModelMetadata("friendbar_short", dir: MetaDir.WifiGames)
                ),
                new(
                    "gamepanel",
                    new ModelMetadata("gamepanel", dir: MetaDir.WifiGames)
                ),
                new(
                    "gotofriends",
                    new ModelMetadata("gotofriends", dir: MetaDir.WifiGames)
                ),
                new(
                    "gotogames",
                    new ModelMetadata("gotogames", dir: MetaDir.WifiGames)
                ),
                new(
                    "joingame",
                    new ModelMetadata("joingame", dir: MetaDir.WifiGames)
                ),
                new(
                    "locked",
                    new ModelMetadata("locked", dir: MetaDir.WifiGames)
                ),
                new(
                    "mainframe",
                    new ModelMetadata("mainframe", dir: MetaDir.WifiGames)
                ),
                new(
                    "namepanel",
                    new ModelMetadata("namepanel", dir: MetaDir.WifiGames)
                ),
                new(
                    "pending",
                    new ModelMetadata("pending", dir: MetaDir.WifiGames)
                ),
                new(
                    "rivalbar_long",
                    new ModelMetadata("rivalbar_long", dir: MetaDir.WifiGames)
                ),
                new(
                    "rivalbar_short",
                    new ModelMetadata("rivalbar_short", dir: MetaDir.WifiGames)
                ),
                new(
                    "secondframe",
                    new ModelMetadata("secondframe", dir: MetaDir.WifiGames)
                ),
                new(
                    "unlocked",
                    new ModelMetadata("unlocked", dir: MetaDir.WifiGames)
                ),
                new(
                    "uparrow",
                    new ModelMetadata("uparrow", dir: MetaDir.WifiGames)
                ),
                new(
                    "yourpanel",
                    new ModelMetadata("yourpanel", dir: MetaDir.WifiGames)
                ),
                // main menu
                new(
                    "audio",
                    new ModelMetadata("audio", dir: MetaDir.MainMenu)
                ),
                new(
                    "backhighlight",
                    new ModelMetadata("backhighlight", dir: MetaDir.MainMenu)
                ),
                new(
                    "backicon",
                    new ModelMetadata("backicon", dir: MetaDir.MainMenu)
                ),
                new(
                    "big_highlight",
                    new ModelMetadata("big_highlight", dir: MetaDir.MainMenu)
                ),
                new(
                    "blackdrop",
                    new ModelMetadata("blackdrop", dir: MetaDir.MainMenu)
                ),
                new(
                    "controls",
                    new ModelMetadata("controls", dir: MetaDir.MainMenu)
                ),
                new(
                    "copy",
                    new ModelMetadata("copy", dir: MetaDir.MainMenu)
                ),
                new(
                    "credits",
                    new ModelMetadata("credits", dir: MetaDir.MainMenu)
                ),
                new(
                    "delete",
                    new ModelMetadata("delete", dir: MetaDir.MainMenu)
                ),
                new(
                    "dialog_yesno",
                    new ModelMetadata("dialog_yesno", dir: MetaDir.MainMenu)
                ),
                new(
                    "disconnect",
                    new ModelMetadata("disconnect", dir: MetaDir.MainMenu)
                ),
                new(
                    "divider",
                    new ModelMetadata("divider", dir: MetaDir.MainMenu)
                ),
                new(
                    "edit",
                    new ModelMetadata("edit", dir: MetaDir.MainMenu)
                ),
                new(
                    "eraseall",
                    new ModelMetadata("eraseall", dir: MetaDir.MainMenu)
                ),
                new(
                    "esrb",
                    new ModelMetadata("esrb", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileAbrackets",
                    new ModelMetadata("fileAbrackets", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileAempty",
                    new ModelMetadata("fileAempty", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileA",
                    new ModelMetadata("fileA", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileBempty",
                    new ModelMetadata("fileBempty", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileB",
                    new ModelMetadata("fileB", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileCempty",
                    new ModelMetadata("fileCempty", dir: MetaDir.MainMenu)
                ),
                new(
                    "fileC",
                    new ModelMetadata("fileC", dir: MetaDir.MainMenu)
                ),
                new(
                    "gamebrackets",
                    new ModelMetadata("gamebrackets", dir: MetaDir.MainMenu)
                ),
                new(
                    "intogamefade",
                    new ModelMetadata("intogamefade", dir: MetaDir.MainMenu)
                ),
                new(
                    "microphone",
                    new ModelMetadata("microphone", dir: MetaDir.MainMenu)
                ),
                new(
                    "movies",
                    new ModelMetadata("movies", dir: MetaDir.MainMenu)
                ),
                new(
                    "multiplayer",
                    new ModelMetadata("multiplayer", dir: MetaDir.MainMenu)
                ),
                new(
                    "options",
                    new ModelMetadata("options", dir: MetaDir.MainMenu)
                ),
                new(
                    "orange",
                    new ModelMetadata("orange", dir: MetaDir.MainMenu)
                ),
                new(
                    "records",
                    new ModelMetadata("records", dir: MetaDir.MainMenu)
                ),
                new(
                    "singleplayer",
                    new ModelMetadata("singleplayer", dir: MetaDir.MainMenu)
                ),
                new(
                    "small_highlight",
                    new ModelMetadata("small_highlight", dir: MetaDir.MainMenu)
                ),
                new(
                    "topdrop",
                    new ModelMetadata("topdrop", dir: MetaDir.MainMenu)
                ),
                new(
                    "toplogoR",
                    new ModelMetadata("toplogoR", dir: MetaDir.MainMenu)
                ),
                new(
                    "toplogo",
                    new ModelMetadata("toplogo", dir: MetaDir.MainMenu)
                ),
                new(
                    "whitelogo",
                    new ModelMetadata("whitelogo", dir: MetaDir.MainMenu)
                ),
                new(
                    "wifi1",
                    new ModelMetadata("wifi1", dir: MetaDir.MainMenu)
                ),
                new(
                    "wifi2",
                    new ModelMetadata("wifi2", dir: MetaDir.MainMenu)
                ),
                new(
                    "wifi3",
                    new ModelMetadata("wifi3", dir: MetaDir.MainMenu)
                ),
                new(
                    "wifi4",
                    new ModelMetadata("wifi4", dir: MetaDir.MainMenu)
                ),
                new(
                    "wireless1",
                    new ModelMetadata("wireless1", dir: MetaDir.MainMenu)
                ),
                new(
                    "wireless2",
                    new ModelMetadata("wireless2", dir: MetaDir.MainMenu)
                ),
                new(
                    "wireless3",
                    new ModelMetadata("wireless3", dir: MetaDir.MainMenu)
                ),
                new(
                    "wireless4",
                    new ModelMetadata("wireless4", dir: MetaDir.MainMenu)
                ),
                // stage
                new(
                    "ad1",
                    new ModelMetadata("ad1", dir: MetaDir.Stage)
                ),
                new(
                    "ad1_dm1",
                    new ModelMetadata("ad1_dm1", dir: MetaDir.Stage)
                ),
                new(
                    "ad2",
                    new ModelMetadata("ad2", dir: MetaDir.Stage)
                ),
                new(
                    "ad2_dm1",
                    new ModelMetadata("ad2_dm1", dir: MetaDir.Stage)
                ),
                new(
                    "ctf1",
                    new ModelMetadata("ctf1", dir: MetaDir.Stage)
                ),
                new(
                    "ctf1_dm1",
                    new ModelMetadata("ctf1_dm1", dir: MetaDir.Stage)
                ),
                new(
                    "e3level",
                    new ModelMetadata("e3level", dir: MetaDir.Stage)
                ),
                new(
                    "goreab2",
                    new ModelMetadata("goreab2", dir: MetaDir.Stage)
                ),
                new(
                    "mp1",
                    new ModelMetadata("mp1", dir: MetaDir.Stage)
                ),
                new(
                    "mp2",
                    new ModelMetadata("mp2", dir: MetaDir.Stage)
                ),
                new(
                    "mp3",
                    new ModelMetadata("mp3", dir: MetaDir.Stage)
                ),
                new(
                    "mp4",
                    new ModelMetadata("mp4", dir: MetaDir.Stage)
                ),
                new(
                    "mp4_dm1",
                    new ModelMetadata("mp4_dm1", dir: MetaDir.Stage)
                ),
                new(
                    "mp5",
                    new ModelMetadata("mp5", dir: MetaDir.Stage)
                ),
                new(
                    "mp6",
                    new ModelMetadata("mp6", dir: MetaDir.Stage)
                ),
                new(
                    "mp7",
                    new ModelMetadata("mp7", dir: MetaDir.Stage)
                ),
                new(
                    "mp8",
                    new ModelMetadata("mp8", dir: MetaDir.Stage)
                ),
                new(
                    "mp9",
                    new ModelMetadata("mp9", dir: MetaDir.Stage)
                ),
                new(
                    "mp10",
                    new ModelMetadata("mp10", dir: MetaDir.Stage)
                ),
                new(
                    "mp11",
                    new ModelMetadata("mp11", dir: MetaDir.Stage)
                ),
                new(
                    "mp12",
                    new ModelMetadata("mp12", dir: MetaDir.Stage)
                ),
                new(
                    "mp13",
                    new ModelMetadata("mp13", dir: MetaDir.Stage)
                ),
                new(
                    "mp14",
                    new ModelMetadata("mp14", dir: MetaDir.Stage)
                ),
                new(
                    "random",
                    new ModelMetadata("random", dir: MetaDir.Stage)
                ),
                new(
                    "unit1land",
                    new ModelMetadata("unit1land", dir: MetaDir.Stage)
                ),
                new(
                    "unit2land",
                    new ModelMetadata("unit2land", dir: MetaDir.Stage)
                ),
                new(
                    "unit3land",
                    new ModelMetadata("unit3land", dir: MetaDir.Stage)
                ),
                new(
                    "unit4land",
                    new ModelMetadata("unit4land", dir: MetaDir.Stage)
                ),
                new(
                    "blackout",
                    new ModelMetadata("blackout", dir: MetaDir.Stage)
                ),
                new(
                    "screenshot",
                    new ModelMetadata("screenshot", dir: MetaDir.Stage)
                ),
                new(
                    "stage_left",
                    new ModelMetadata("stage_left", dir: MetaDir.Stage)
                ),
                new(
                    "stage_right",
                    new ModelMetadata("stage_right", dir: MetaDir.Stage)
                ),
                new(
                    "stageleft_highlight",
                    new ModelMetadata("stageleft_highlight", dir: MetaDir.Stage)
                ),
                new(
                    "stageright_highlight",
                    new ModelMetadata("stageright_highlight", dir: MetaDir.Stage)
                )
            ]);
    }
}

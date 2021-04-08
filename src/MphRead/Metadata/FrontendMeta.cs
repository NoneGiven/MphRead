using System.Collections.Generic;

namespace MphRead
{
    public static partial class Metadata
    {
        // only in A76E
        public static readonly ModelMetadata Ad2Dm2
            = new ModelMetadata("ad2_dm2", dir: MetaDir.Stage);

        public static readonly IReadOnlyDictionary<string, ModelMetadata> HudModels
            = new Dictionary<string, ModelMetadata>()
            {
                // cockpit
                {
                    "unit1_land_cockpit",
                    new ModelMetadata("unit1_land_cockpit", dir: MetaDir.Hud)
                },
                {
                    "unit2_land_cockpit",
                    new ModelMetadata("unit2_land_cockpit", dir: MetaDir.Hud)
                },
                {
                    "unit3_land_cockpit",
                    new ModelMetadata("unit3_land_cockpit", dir: MetaDir.Hud)
                },
                {
                    "unit4_land_cockpit",
                    new ModelMetadata("unit4_land_cockpit", dir: MetaDir.Hud)
                },
                {
                    "gorea_land_cockpit",
                    new ModelMetadata("gorea_land_cockpit", dir: MetaDir.Hud)
                },
                // nav rooms
                {
                    "unit1_1NAV",
                    new ModelMetadata("unit1_1NAV", dir: MetaDir.Hud)
                },
                {
                    "unit1_2NAV",
                    new ModelMetadata("unit1_2NAV", dir: MetaDir.Hud)
                },
                {
                    "unit2_1NAV",
                    new ModelMetadata("unit2_1NAV", dir: MetaDir.Hud)
                },
                {
                    "unit2_2NAV",
                    new ModelMetadata("unit2_2NAV", dir: MetaDir.Hud)
                },
                {
                    "unit3_1NAV",
                    new ModelMetadata("unit3_1NAV", dir: MetaDir.Hud)
                },
                {
                    "unit3_2NAV",
                    new ModelMetadata("unit3_2NAV", dir: MetaDir.Hud)
                },
                {
                    "unit4_1NAV",
                    new ModelMetadata("unit4_1NAV", dir: MetaDir.Hud)
                },
                {
                    "Door_NAV",
                    new ModelMetadata("Door_NAV", dir: MetaDir.Hud)
                },
                {
                    "PlayerPos_NAV",
                    new ModelMetadata("PlayerPos_NAV", dir: MetaDir.Hud, anim: "PlayerPos")
                },
                {
                    "damage",
                    new ModelMetadata("damage", dir: MetaDir.Hud)
                },
                // todo: can't parse some out of bounds texture/palette offsets from this
                //{
                //    "icons",
                //    new ModelMetadata("icons", dir: MetaDir.Hud)
                //},
                {
                    "hud_icon_arrow",
                    new ModelMetadata("hud_icon_arrow", dir: MetaDir.Hud)
                },
                {
                    "hud_icon_nodes",
                    new ModelMetadata("hud_icon_nodes", dir: MetaDir.Hud)
                },
                {
                    "hud_icon_octolith",
                    new ModelMetadata("hud_icon_octolith", dir: MetaDir.Hud)
                },
                {
                    "hud_icon_player",
                    new ModelMetadata("hud_icon_player", dir: MetaDir.Hud)
                }
            };

        public static readonly IReadOnlyDictionary<string, ModelMetadata> TouchToStartModels
            = new Dictionary<string, ModelMetadata>()
            {
                // touchtostart
                {
                    "touch_bg",
                    new ModelMetadata("touch_bg", dir: MetaDir.TouchToStart)
                },
            };

        public static readonly IReadOnlyDictionary<string, ModelMetadata> MultiplayerModels
            = new Dictionary<string, ModelMetadata>()
            {
                // multiplayer
                {
                    "bigdeathmatch",
                    new ModelMetadata("bigdeathmatch", dir: MetaDir.Multiplayer)
                },
                {
                    "bounty",
                    new ModelMetadata("bounty", dir: MetaDir.Multiplayer)
                },
                {
                    "capture",
                    new ModelMetadata("capture", dir: MetaDir.Multiplayer)
                },
                {
                    "challenge",
                    new ModelMetadata("challenge", dir: MetaDir.Multiplayer)
                },
                {
                    "friendconfig",
                    new ModelMetadata("friendconfig", dir: MetaDir.Multiplayer)
                },
                {
                    "med_highlight",
                    new ModelMetadata("med_highlight", dir: MetaDir.Multiplayer)
                },
                {
                    "multiplayer",
                    new ModelMetadata("multiplayer", dir: MetaDir.Multiplayer)
                },
                {
                    "nodes",
                    new ModelMetadata("nodes", dir: MetaDir.Multiplayer)
                },
                {
                    "primehunter",
                    new ModelMetadata("primehunter", dir: MetaDir.Multiplayer)
                },
                {
                    "rivalradar",
                    new ModelMetadata("rivalradar", dir: MetaDir.Multiplayer)
                },
                {
                    "singlecart",
                    new ModelMetadata("singlecart", dir: MetaDir.Multiplayer)
                },
                {
                    "survival",
                    new ModelMetadata("survival", dir: MetaDir.Multiplayer)
                },
                {
                    "wificonfig",
                    new ModelMetadata("wificonfig", dir: MetaDir.Multiplayer)
                },
                {
                    "wifi",
                    new ModelMetadata("wifi", dir: MetaDir.Multiplayer)
                }
            };

        public static readonly IReadOnlyDictionary<string, ModelMetadata> LogoModels
            = new Dictionary<string, ModelMetadata>()
            {
                // logos
                {
                    "flare",
                    new ModelMetadata("flare", dir: MetaDir.Logo)
                },
                {
                    "flare2",
                    new ModelMetadata("flare2", dir: MetaDir.Logo)
                },
                {
                    "flare3",
                    new ModelMetadata("flare3", dir: MetaDir.Logo)
                },
                {
                    "logo1",
                    new ModelMetadata("logo1", dir: MetaDir.Logo)
                },
                {
                    "logo2",
                    new ModelMetadata("logo2", dir: MetaDir.Logo)
                },
                {
                    "logo3",
                    new ModelMetadata("logo3", dir: MetaDir.Logo)
                },
                {
                    "logos",
                    new ModelMetadata("logos", dir: MetaDir.Logo)
                },
                {
                    "name",
                    new ModelMetadata("name", dir: MetaDir.Logo)
                },
                {
                    "nameflare",
                    new ModelMetadata("nameflare", dir: MetaDir.Logo)
                },
                {
                    "nst",
                    new ModelMetadata("nst", dir: MetaDir.Logo)
                },
                {
                    "whitelogo",
                    new ModelMetadata("whitelogo", dir: MetaDir.Logo)
                }
            };

        // sktodo: multiple animation files
        public static readonly IReadOnlyDictionary<string, ModelMetadata> FrontendModels
            = new Dictionary<string, ModelMetadata>()
            {
                // characterselect
                {
                    "big_kanden",
                    new ModelMetadata("big_kanden", dir: MetaDir.CharSelect)
                },
                {
                    "big_noxus",
                    new ModelMetadata("big_noxus", dir: MetaDir.CharSelect)
                },
                {
                    "big_samus",
                    new ModelMetadata("big_samus", dir: MetaDir.CharSelect)
                },
                {
                    "big_spire",
                    new ModelMetadata("big_spire", dir: MetaDir.CharSelect)
                },
                {
                    "big_sylux",
                    new ModelMetadata("big_sylux", dir: MetaDir.CharSelect)
                },
                {
                    "big_trace",
                    new ModelMetadata("big_trace", dir: MetaDir.CharSelect)
                },
                {
                    "big_weavel",
                    new ModelMetadata("big_weavel", dir: MetaDir.CharSelect)
                },
                {
                    "character_grid",
                    new ModelMetadata("character_grid", dir: MetaDir.CharSelect)
                },
                {
                    "kandenoff",
                    new ModelMetadata("kandenoff", dir: MetaDir.CharSelect)
                },
                {
                    "kanden",
                    new ModelMetadata("kanden", dir: MetaDir.CharSelect)
                },
                {
                    "noxusoff",
                    new ModelMetadata("noxusoff", dir: MetaDir.CharSelect)
                },
                {
                    "noxus",
                    new ModelMetadata("noxus", dir: MetaDir.CharSelect)
                },
                {
                    "samus",
                    new ModelMetadata("samus", dir: MetaDir.CharSelect)
                },
                {
                    "spireoff",
                    new ModelMetadata("spireoff", dir: MetaDir.CharSelect)
                },
                {
                    "spire",
                    new ModelMetadata("spire", dir: MetaDir.CharSelect)
                },
                {
                    "syluxoff",
                    new ModelMetadata("syluxoff", dir: MetaDir.CharSelect)
                },
                {
                    "sylux",
                    new ModelMetadata("sylux", dir: MetaDir.CharSelect)
                },
                {
                    "traceoff",
                    new ModelMetadata("traceoff", dir: MetaDir.CharSelect)
                },
                {
                    "trace",
                    new ModelMetadata("trace", dir: MetaDir.CharSelect)
                },
                {
                    "weaveloff",
                    new ModelMetadata("weaveloff", dir: MetaDir.CharSelect)
                },
                {
                    "weavel",
                    new ModelMetadata("weavel", dir: MetaDir.CharSelect)
                },
                // createjoin
                {
                    "create",
                    new ModelMetadata("create", dir: MetaDir.CreateJoin)
                },
                {
                    "join_highlight",
                    new ModelMetadata("join_highlight", dir: MetaDir.CreateJoin)
                },
                {
                    "join",
                    new ModelMetadata("join", dir: MetaDir.CreateJoin)
                },
                {
                    "redbar",
                    new ModelMetadata("redbar", dir: MetaDir.CreateJoin)
                },
                // gameoptions
                {
                    "arrows_option",
                    new ModelMetadata("arrows_option", dir: MetaDir.GameOption)
                },
                {
                    "arrows_type",
                    new ModelMetadata("arrows_type", dir: MetaDir.GameOption)
                },
                {
                    "box_arrowsdouble",
                    new ModelMetadata("box_arrowsdouble", dir: MetaDir.GameOption)
                },
                {
                    "box_arrows",
                    new ModelMetadata("box_arrows", dir: MetaDir.GameOption)
                },
                {
                    "box_type",
                    new ModelMetadata("box_type", dir: MetaDir.GameOption)
                },
                {
                    "cancel",
                    new ModelMetadata("cancel", dir: MetaDir.GameOption)
                },
                {
                    "chat",
                    new ModelMetadata("chat", dir: MetaDir.GameOption)
                },
                {
                    "connected",
                    new ModelMetadata("connected", dir: MetaDir.GameOption)
                },
                {
                    "empty",
                    new ModelMetadata("empty", dir: MetaDir.GameOption)
                },
                {
                    "headphones",
                    new ModelMetadata("headphones", dir: MetaDir.GameOption)
                },
                {
                    "highlight_arrowleft",
                    new ModelMetadata("highlight_arrowleft", dir: MetaDir.GameOption)
                },
                {
                    "highlight_arrowright",
                    new ModelMetadata("highlight_arrowright", dir: MetaDir.GameOption)
                },
                {
                    "ok",
                    new ModelMetadata("ok", dir: MetaDir.GameOption)
                },
                {
                    "playmask",
                    new ModelMetadata("playmask", dir: MetaDir.GameOption)
                },
                {
                    "play",
                    new ModelMetadata("play", dir: MetaDir.GameOption)
                },
                {
                    "stereo",
                    new ModelMetadata("stereo", dir: MetaDir.GameOption)
                },
                {
                    "surround",
                    new ModelMetadata("surround", dir: MetaDir.GameOption)
                },
                {
                    "wifiicon",
                    new ModelMetadata("wifiicon", dir: MetaDir.GameOption)
                },
                {
                    "wifionlineicon",
                    new ModelMetadata("wifionlineicon", dir: MetaDir.GameOption)
                },
                // gamerscard
                {
                    "alimbicswirl",
                    new ModelMetadata("alimbicswirl", dir: MetaDir.GamersCard)
                },
                {
                    "blackstar",
                    new ModelMetadata("blackstar", dir: MetaDir.GamersCard)
                },
                {
                    "bronzemedal",
                    new ModelMetadata("bronzemedal", dir: MetaDir.GamersCard)
                },
                {
                    "bronzestar",
                    new ModelMetadata("bronzestar", dir: MetaDir.GamersCard)
                },
                {
                    "cardbgEURO",
                    new ModelMetadata("cardbgEURO", dir: MetaDir.GamersCard)
                },
                {
                    "cardbgJAP",
                    new ModelMetadata("cardbgJAP", dir: MetaDir.GamersCard)
                },
                {
                    "cardbgUS",
                    new ModelMetadata("cardbgUS", dir: MetaDir.GamersCard)
                },
                {
                    "cardbg",
                    new ModelMetadata("cardbg", dir: MetaDir.GamersCard)
                },
                {
                    "cardheadergold",
                    new ModelMetadata("cardheadergold", dir: MetaDir.GamersCard)
                },
                {
                    "cardmessage",
                    new ModelMetadata("cardmessage", dir: MetaDir.GamersCard)
                },
                {
                    "frame200gold",
                    new ModelMetadata("frame200gold", dir: MetaDir.GamersCard)
                },
                {
                    "frame200",
                    new ModelMetadata("frame200", dir: MetaDir.GamersCard)
                },
                {
                    "goldmedal",
                    new ModelMetadata("goldmedal", dir: MetaDir.GamersCard)
                },
                {
                    "goldstar",
                    new ModelMetadata("goldstar", dir: MetaDir.GamersCard)
                },
                {
                    "kandenmost",
                    new ModelMetadata("kandenmost", dir: MetaDir.GamersCard)
                },
                {
                    "lricons",
                    new ModelMetadata("lricons", dir: MetaDir.GamersCard)
                },
                {
                    "noxmost",
                    new ModelMetadata("noxmost", dir: MetaDir.GamersCard)
                },
                {
                    "octolith",
                    new ModelMetadata("octolith", dir: MetaDir.GamersCard)
                },
                {
                    "redstar",
                    new ModelMetadata("redstar", dir: MetaDir.GamersCard)
                },
                {
                    "samusmost",
                    new ModelMetadata("samusmost", dir: MetaDir.GamersCard)
                },
                {
                    "silvermedal",
                    new ModelMetadata("silvermedal", dir: MetaDir.GamersCard)
                },
                {
                    "silverstar",
                    new ModelMetadata("silverstar", dir: MetaDir.GamersCard)
                },
                {
                    "spiremost",
                    new ModelMetadata("spiremost", dir: MetaDir.GamersCard)
                },
                {
                    "syluxmost",
                    new ModelMetadata("syluxmost", dir: MetaDir.GamersCard)
                },
                {
                    "tab1",
                    new ModelMetadata("tab1", dir: MetaDir.GamersCard)
                },
                {
                    "tab2",
                    new ModelMetadata("tab2", dir: MetaDir.GamersCard)
                },
                {
                    "tab3",
                    new ModelMetadata("tab3", dir: MetaDir.GamersCard)
                },
                {
                    "tab4",
                    new ModelMetadata("tab4", dir: MetaDir.GamersCard)
                },
                {
                    "tab5",
                    new ModelMetadata("tab5", dir: MetaDir.GamersCard)
                },
                {
                    "textarea",
                    new ModelMetadata("textarea", dir: MetaDir.GamersCard)
                },
                {
                    "tracemost",
                    new ModelMetadata("tracemost", dir: MetaDir.GamersCard)
                },
                {
                    "weavelmost",
                    new ModelMetadata("weavelmost", dir: MetaDir.GamersCard)
                },
                // keyboard
                {
                    "EURO",
                    new ModelMetadata("EURO", dir: MetaDir.Keyboard)
                },
                {
                    "hl_key",
                    new ModelMetadata("hl_key", dir: MetaDir.Keyboard)
                },
                {
                    "JAP_1",
                    new ModelMetadata("JAP_1", dir: MetaDir.Keyboard)
                },
                {
                    "JAP_2",
                    new ModelMetadata("JAP_2", dir: MetaDir.Keyboard)
                },
                {
                    "keyboardmask",
                    new ModelMetadata("keyboardmask", dir: MetaDir.Keyboard)
                },
                {
                    "keytoggle",
                    new ModelMetadata("keytoggle", dir: MetaDir.Keyboard)
                },
                {
                    "messagebox",
                    new ModelMetadata("messagebox", dir: MetaDir.Keyboard)
                },
                {
                    "US_caps",
                    new ModelMetadata("US_caps", dir: MetaDir.Keyboard)
                },
                {
                    "US_lower",
                    new ModelMetadata("US_lower", dir: MetaDir.Keyboard)
                },
                {
                    "US_upper",
                    new ModelMetadata("US_upper", dir: MetaDir.Keyboard)
                },
                // keypad
                {
                    "numback",
                    new ModelMetadata("numback", dir: MetaDir.Keypad)
                },
                {
                    "numkey",
                    new ModelMetadata("numkey", dir: MetaDir.Keypad)
                },
                // movieplayer
                {
                    "cover",
                    new ModelMetadata("cover", dir: MetaDir.MoviePlayer)
                },
                {
                    "hotspot",
                    new ModelMetadata("hotspot", dir: MetaDir.MoviePlayer)
                },
                {
                    "thumbnails1",
                    new ModelMetadata("thumbnails1", dir: MetaDir.MoviePlayer)
                },
                {
                    "thumbnails2",
                    new ModelMetadata("thumbnails2", dir: MetaDir.MoviePlayer)
                },
                // multimaster
                {
                    "bluedot",
                    new ModelMetadata("bluedot", dir: MetaDir.MultiMaster)
                },
                {
                    "tab",
                    new ModelMetadata("tab", dir: MetaDir.MultiMaster)
                },
                {
                    "topsettings",
                    new ModelMetadata("topsettings", dir: MetaDir.MultiMaster)
                },
                {
                    "topstatus",
                    new ModelMetadata("topstatus", dir: MetaDir.MultiMaster)
                },
                // pax_controls
                {
                    "dmleft",
                    new ModelMetadata("dmleft", dir: MetaDir.PaxControls)
                },
                {
                    "dmright",
                    new ModelMetadata("dmright", dir: MetaDir.PaxControls)
                },
                {
                    "selectAoff",
                    new ModelMetadata("selectAoff", dir: MetaDir.PaxControls)
                },
                {
                    "selectAon",
                    new ModelMetadata("selectAon", dir: MetaDir.PaxControls)
                },
                {
                    "stylusleft",
                    new ModelMetadata("stylusleft", dir: MetaDir.PaxControls)
                },
                {
                    "stylusright",
                    new ModelMetadata("stylusright", dir: MetaDir.PaxControls)
                },
                // popup
                {
                    "popup",
                    new ModelMetadata("popup", dir: MetaDir.Popup)
                },
                // results
                {
                    "blackarrowleft",
                    new ModelMetadata("blackarrowleft", dir: MetaDir.Results)
                },
                {
                    "blackarrowright",
                    new ModelMetadata("blackarrowright", dir: MetaDir.Results)
                },
                {
                    "crossbar",
                    new ModelMetadata("crossbar", dir: MetaDir.Results)
                },
                {
                    "lightning",
                    new ModelMetadata("lightning", dir: MetaDir.Results)
                },
                {
                    "medalbg",
                    new ModelMetadata("medalbg", dir: MetaDir.Results)
                },
                {
                    "medalbronze",
                    new ModelMetadata("medalbronze", dir: MetaDir.Results)
                },
                {
                    "medalgold",
                    new ModelMetadata("medalgold", dir: MetaDir.Results)
                },
                {
                    "medallast",
                    new ModelMetadata("medallast", dir: MetaDir.Results)
                },
                {
                    "medalsilver",
                    new ModelMetadata("medalsilver", dir: MetaDir.Results)
                },
                {
                    "playagain",
                    new ModelMetadata("playagain", dir: MetaDir.Results)
                },
                {
                    "quit",
                    new ModelMetadata("quit", dir: MetaDir.Results)
                },
                {
                    "results",
                    new ModelMetadata("results", dir: MetaDir.Results)
                },
                {
                    "rivalboxoff",
                    new ModelMetadata("rivalboxoff", dir: MetaDir.Results)
                },
                {
                    "rivalboxon",
                    new ModelMetadata("rivalboxon", dir: MetaDir.Results)
                },
                {
                    "teamdivide",
                    new ModelMetadata("teamdivide", dir: MetaDir.Results)
                },
                {
                    "topbar",
                    new ModelMetadata("topbar", dir: MetaDir.Results)
                },
                {
                    "wincondition",
                    new ModelMetadata("wincondition", dir: MetaDir.Results)
                },
                // sc_startgame
                {
                    "dssystem",
                    new ModelMetadata("dssystem", dir: MetaDir.ScStartGame)
                },
                {
                    "readybox",
                    new ModelMetadata("readybox", dir: MetaDir.ScStartGame)
                },
                // startgame
                {
                    "adbot",
                    new ModelMetadata("adbot", dir: MetaDir.StartGame)
                },
                {
                    "botminus",
                    new ModelMetadata("botminus", dir: MetaDir.StartGame)
                },
                {
                    "chatout",
                    new ModelMetadata("chatout", dir: MetaDir.StartGame)
                },
                {
                    "choicekanden",
                    new ModelMetadata("choicekanden", dir: MetaDir.StartGame)
                },
                {
                    "choicenoxus",
                    new ModelMetadata("choicenoxus", dir: MetaDir.StartGame)
                },
                {
                    "choicesamus",
                    new ModelMetadata("choicesamus", dir: MetaDir.StartGame)
                },
                {
                    "choicespire",
                    new ModelMetadata("choicespire", dir: MetaDir.StartGame)
                },
                {
                    "choicesylux",
                    new ModelMetadata("choicesylux", dir: MetaDir.StartGame)
                },
                {
                    "choicetrace",
                    new ModelMetadata("choicetrace", dir: MetaDir.StartGame)
                },
                {
                    "choiceweavel",
                    new ModelMetadata("choiceweavel", dir: MetaDir.StartGame)
                },
                {
                    "playergrid",
                    new ModelMetadata("playergrid", dir: MetaDir.StartGame)
                },
                {
                    "player_highlight",
                    new ModelMetadata("player_highlight", dir: MetaDir.StartGame)
                },
                {
                    "spicy_1",
                    new ModelMetadata("spicy_1", dir: MetaDir.StartGame)
                },
                {
                    "spicy_2",
                    new ModelMetadata("spicy_2", dir: MetaDir.StartGame)
                },
                {
                    "spicy_3",
                    new ModelMetadata("spicy_3", dir: MetaDir.StartGame)
                },
                {
                    "startgame_highlight",
                    new ModelMetadata("startgame_highlight", dir: MetaDir.StartGame)
                },
                {
                    "startgame",
                    new ModelMetadata("startgame", dir: MetaDir.StartGame)
                },
                {
                    "team_blue",
                    new ModelMetadata("team_blue", dir: MetaDir.StartGame)
                },
                {
                    "team_red",
                    new ModelMetadata("team_red", dir: MetaDir.StartGame)
                },
                // tostart
                {
                    "logofinal",
                    new ModelMetadata("logofinal", dir: MetaDir.ToStart)
                },
                // touchtostart_2
                {
                    "splashers",
                    new ModelMetadata("splashers", dir: MetaDir.TouchToStart2)
                },
                {
                    "touch_bg",
                    new ModelMetadata("touch_bg", dir: MetaDir.TouchToStart2)
                },
                // wifi_createjoin
                {
                    "splitter",
                    new ModelMetadata("splitter", dir: MetaDir.WifiCreate)
                },
                {
                    "wififriend",
                    new ModelMetadata("wififriend", dir: MetaDir.WifiCreate)
                },
                {
                    "wifijoin",
                    new ModelMetadata("wifijoin", dir: MetaDir.WifiCreate)
                },
                // wifi_games
                {
                    "bigpanel",
                    new ModelMetadata("bigpanel", dir: MetaDir.WifiGames)
                },
                {
                    "creategame",
                    new ModelMetadata("creategame", dir: MetaDir.WifiGames)
                },
                {
                    "downarrow",
                    new ModelMetadata("downarrow", dir: MetaDir.WifiGames)
                },
                {
                    "friendbar_long",
                    new ModelMetadata("friendbar_long", dir: MetaDir.WifiGames)
                },
                {
                    "friendbar_short",
                    new ModelMetadata("friendbar_short", dir: MetaDir.WifiGames)
                },
                {
                    "gamepanel",
                    new ModelMetadata("gamepanel", dir: MetaDir.WifiGames)
                },
                {
                    "gotofriends",
                    new ModelMetadata("gotofriends", dir: MetaDir.WifiGames)
                },
                {
                    "gotogames",
                    new ModelMetadata("gotogames", dir: MetaDir.WifiGames)
                },
                {
                    "joingame",
                    new ModelMetadata("joingame", dir: MetaDir.WifiGames)
                },
                {
                    "locked",
                    new ModelMetadata("locked", dir: MetaDir.WifiGames)
                },
                {
                    "mainframe",
                    new ModelMetadata("mainframe", dir: MetaDir.WifiGames)
                },
                {
                    "namepanel",
                    new ModelMetadata("namepanel", dir: MetaDir.WifiGames)
                },
                {
                    "pending",
                    new ModelMetadata("pending", dir: MetaDir.WifiGames)
                },
                {
                    "rivalbar_long",
                    new ModelMetadata("rivalbar_long", dir: MetaDir.WifiGames)
                },
                {
                    "rivalbar_short",
                    new ModelMetadata("rivalbar_short", dir: MetaDir.WifiGames)
                },
                {
                    "secondframe",
                    new ModelMetadata("secondframe", dir: MetaDir.WifiGames)
                },
                {
                    "unlocked",
                    new ModelMetadata("unlocked", dir: MetaDir.WifiGames)
                },
                {
                    "uparrow",
                    new ModelMetadata("uparrow", dir: MetaDir.WifiGames)
                },
                {
                    "yourpanel",
                    new ModelMetadata("yourpanel", dir: MetaDir.WifiGames)
                },
                // main menu
                {
                    "audio",
                    new ModelMetadata("audio", dir: MetaDir.MainMenu)
                },
                {
                    "backhighlight",
                    new ModelMetadata("backhighlight", dir: MetaDir.MainMenu)
                },
                {
                    "backicon",
                    new ModelMetadata("backicon", dir: MetaDir.MainMenu)
                },
                {
                    "big_highlight",
                    new ModelMetadata("big_highlight", dir: MetaDir.MainMenu)
                },
                {
                    "blackdrop",
                    new ModelMetadata("blackdrop", dir: MetaDir.MainMenu)
                },
                {
                    "controls",
                    new ModelMetadata("controls", dir: MetaDir.MainMenu)
                },
                {
                    "copy",
                    new ModelMetadata("copy", dir: MetaDir.MainMenu)
                },
                {
                    "credits",
                    new ModelMetadata("credits", dir: MetaDir.MainMenu)
                },
                {
                    "delete",
                    new ModelMetadata("delete", dir: MetaDir.MainMenu)
                },
                {
                    "dialog_yesno",
                    new ModelMetadata("dialog_yesno", dir: MetaDir.MainMenu)
                },
                {
                    "disconnect",
                    new ModelMetadata("disconnect", dir: MetaDir.MainMenu)
                },
                {
                    "divider",
                    new ModelMetadata("divider", dir: MetaDir.MainMenu)
                },
                {
                    "edit",
                    new ModelMetadata("edit", dir: MetaDir.MainMenu)
                },
                {
                    "eraseall",
                    new ModelMetadata("eraseall", dir: MetaDir.MainMenu)
                },
                {
                    "esrb",
                    new ModelMetadata("esrb", dir: MetaDir.MainMenu)
                },
                {
                    "fileAbrackets",
                    new ModelMetadata("fileAbrackets", dir: MetaDir.MainMenu)
                },
                {
                    "fileAempty",
                    new ModelMetadata("fileAempty", dir: MetaDir.MainMenu)
                },
                {
                    "fileA",
                    new ModelMetadata("fileA", dir: MetaDir.MainMenu)
                },
                {
                    "fileBempty",
                    new ModelMetadata("fileBempty", dir: MetaDir.MainMenu)
                },
                {
                    "fileB",
                    new ModelMetadata("fileB", dir: MetaDir.MainMenu)
                },
                {
                    "fileCempty",
                    new ModelMetadata("fileCempty", dir: MetaDir.MainMenu)
                },
                {
                    "fileC",
                    new ModelMetadata("fileC", dir: MetaDir.MainMenu)
                },
                {
                    "gamebrackets",
                    new ModelMetadata("gamebrackets", dir: MetaDir.MainMenu)
                },
                {
                    "intogamefade",
                    new ModelMetadata("intogamefade", dir: MetaDir.MainMenu)
                },
                {
                    "microphone",
                    new ModelMetadata("microphone", dir: MetaDir.MainMenu)
                },
                {
                    "movies",
                    new ModelMetadata("movies", dir: MetaDir.MainMenu)
                },
                {
                    "multiplayer",
                    new ModelMetadata("multiplayer", dir: MetaDir.MainMenu)
                },
                {
                    "options",
                    new ModelMetadata("options", dir: MetaDir.MainMenu)
                },
                {
                    "orange",
                    new ModelMetadata("orange", dir: MetaDir.MainMenu)
                },
                {
                    "records",
                    new ModelMetadata("records", dir: MetaDir.MainMenu)
                },
                {
                    "singleplayer",
                    new ModelMetadata("singleplayer", dir: MetaDir.MainMenu)
                },
                {
                    "small_highlight",
                    new ModelMetadata("small_highlight", dir: MetaDir.MainMenu)
                },
                {
                    "topdrop",
                    new ModelMetadata("topdrop", dir: MetaDir.MainMenu)
                },
                {
                    "toplogoR",
                    new ModelMetadata("toplogoR", dir: MetaDir.MainMenu)
                },
                {
                    "toplogo",
                    new ModelMetadata("toplogo", dir: MetaDir.MainMenu)
                },
                {
                    "whitelogo",
                    new ModelMetadata("whitelogo", dir: MetaDir.MainMenu)
                },
                {
                    "wifi1",
                    new ModelMetadata("wifi1", dir: MetaDir.MainMenu)
                },
                {
                    "wifi2",
                    new ModelMetadata("wifi2", dir: MetaDir.MainMenu)
                },
                {
                    "wifi3",
                    new ModelMetadata("wifi3", dir: MetaDir.MainMenu)
                },
                {
                    "wifi4",
                    new ModelMetadata("wifi4", dir: MetaDir.MainMenu)
                },
                {
                    "wireless1",
                    new ModelMetadata("wireless1", dir: MetaDir.MainMenu)
                },
                {
                    "wireless2",
                    new ModelMetadata("wireless2", dir: MetaDir.MainMenu)
                },
                {
                    "wireless3",
                    new ModelMetadata("wireless3", dir: MetaDir.MainMenu)
                },
                {
                    "wireless4",
                    new ModelMetadata("wireless4", dir: MetaDir.MainMenu)
                },
                // stage portrait
                {
                    "ad1",
                    new ModelMetadata("ad1", dir: MetaDir.Stage)
                },
                {
                    "ad1_dm1",
                    new ModelMetadata("ad1_dm1", dir: MetaDir.Stage)
                },
                {
                    "ad2",
                    new ModelMetadata("ad2", dir: MetaDir.Stage)
                },
                {
                    "ad2_dm1",
                    new ModelMetadata("ad2_dm1", dir: MetaDir.Stage)
                },
                {
                    "ctf1",
                    new ModelMetadata("ctf1", dir: MetaDir.Stage)
                },
                {
                    "ctf1_dm1",
                    new ModelMetadata("ctf1_dm1", dir: MetaDir.Stage)
                },
                {
                    "e3level",
                    new ModelMetadata("e3level", dir: MetaDir.Stage)
                },
                {
                    "goreab2",
                    new ModelMetadata("goreab2", dir: MetaDir.Stage)
                },
                {
                    "mp1",
                    new ModelMetadata("mp1", dir: MetaDir.Stage)
                },
                {
                    "mp2",
                    new ModelMetadata("mp2", dir: MetaDir.Stage)
                },
                {
                    "mp3",
                    new ModelMetadata("mp3", dir: MetaDir.Stage)
                },
                {
                    "mp4",
                    new ModelMetadata("mp4", dir: MetaDir.Stage)
                },
                {
                    "mp4_dm1",
                    new ModelMetadata("mp4_dm1", dir: MetaDir.Stage)
                },
                {
                    "mp5",
                    new ModelMetadata("mp5", dir: MetaDir.Stage)
                },
                {
                    "mp6",
                    new ModelMetadata("mp6", dir: MetaDir.Stage)
                },
                {
                    "mp7",
                    new ModelMetadata("mp7", dir: MetaDir.Stage)
                },
                {
                    "mp8",
                    new ModelMetadata("mp8", dir: MetaDir.Stage)
                },
                {
                    "mp9",
                    new ModelMetadata("mp9", dir: MetaDir.Stage)
                },
                {
                    "mp10",
                    new ModelMetadata("mp10", dir: MetaDir.Stage)
                },
                {
                    "mp11",
                    new ModelMetadata("mp11", dir: MetaDir.Stage)
                },
                {
                    "mp12",
                    new ModelMetadata("mp12", dir: MetaDir.Stage)
                },
                {
                    "mp13",
                    new ModelMetadata("mp13", dir: MetaDir.Stage)
                },
                {
                    "mp14",
                    new ModelMetadata("mp14", dir: MetaDir.Stage)
                },
                {
                    "random",
                    new ModelMetadata("random", dir: MetaDir.Stage)
                },
                {
                    "unit1land",
                    new ModelMetadata("unit1land", dir: MetaDir.Stage)
                },
                {
                    "unit2land",
                    new ModelMetadata("unit2land", dir: MetaDir.Stage)
                },
                {
                    "unit3land",
                    new ModelMetadata("unit3land", dir: MetaDir.Stage)
                },
                {
                    "unit4land",
                    new ModelMetadata("unit4land", dir: MetaDir.Stage)
                }
            };
    }
}

using System.Collections.Generic;

namespace MphRead
{
    public static partial class Metadata
    {
        // only in A76E
        public static readonly ModelMetadata Ad2Dm2
            = new ModelMetadata("ad2_dm2", dir: MetaDir.Stage);

        // sktodo: multiple animation files
        public static readonly IReadOnlyDictionary<string, ModelMetadata> FrontendModels
            = new Dictionary<string, ModelMetadata>()
            {
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

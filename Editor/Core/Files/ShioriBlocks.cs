using System.Collections.Generic;

namespace Shiori
{
    /// <summary>The managed-block contents Shiori writes into a Unity project. VRChat additions live in shiori-vrchat.</summary>
    public static class ShioriBlocks
    {
        public static readonly IReadOnlyList<string> GitIgnore = new[]
        {
            "[Ll]ibrary/",
            "[Tt]emp/",
            "[Oo]bj/",
            "[Bb]uild/",
            "[Bb]uilds/",
            "[Ll]ogs/",
            "[Uu]ser[Ss]ettings/",
            "*.csproj",
            "*.sln",
            "*.pidb",
            "*.booproj",
            "*.svd",
            "*.pdb",
            "*.mdb",
            "*.opendb",
            "*.VC.db",
            ".vs/",
            ".idea/",
            ".DS_Store",
        };

        public static readonly IReadOnlyList<string> GitAttributes = new[]
        {
            "*.cs text eol=lf diff=csharp",
            "*.unity text merge=unityyamlmerge",
            "*.prefab text merge=unityyamlmerge",
            "*.asset text merge=unityyamlmerge",
            "*.meta text",
            "*.mat text",
            "*.anim text",
            "*.controller text",
        };
    }
}

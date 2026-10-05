using System.Collections.Generic;

namespace Shiori.Editor
{
    /// <summary>Japanese UI strings. Simple-mode vocabulary is fixed: 保存 / 履歴 / 戻す / バリエーション / 同期.</summary>
    internal static class JapaneseStrings
    {
        public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>
        {
            ["window.title"] = "Shiori",

            ["lock.compiling"] = "スクリプトをコンパイル中です。終わるまで操作できません。",
            ["lock.updating"] = "Unity がアセットを更新中です。終わるまで操作できません。",
            ["lock.playmode"] = "再生モード中は操作できません。再生を停止してください。",
            ["lock.gitbusy"] = "処理を実行中です。しばらくお待ちください。",
        };
    }
}

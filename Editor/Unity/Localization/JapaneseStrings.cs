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

            ["error.git"] = "git の実行に失敗しました（終了コード {0}）。\n{1}",
            ["error.generic"] = "エラーが発生しました。\n{0}",
            ["error.settings"] = "設定ファイルを読めませんでした。\n{0}",
            ["status.checking"] = "確認中…",

            ["wizard.title"] = "はじめの設定",
            ["wizard.intro"] = "このプロジェクトの履歴を残す準備をします。上から順にボタンを押してください。",
            ["wizard.finish.message"] = "準備ができました。",
            ["wizard.finish.button"] = "完了",

            ["step.state.done"] = "完了",
            ["step.state.todo"] = "未完了",
            ["step.state.blocked"] = "前の手順を先に終えてください",

            ["step1.title"] = "git の確認",
            ["step1.found"] = "git {0} を見つけました。",
            ["step1.path"] = "場所: {0}",
            ["step1.notfound"] = "git が見つかりません。Shiori は git を使って履歴を保存します。下のボタンから git をダウンロードしてインストールし、Unity を再起動してから「再確認」を押してください。",
            ["step1.tooold"] = "git {0} は古すぎます。2.30 以上が必要です。最新版をインストールしてから「再確認」を押してください。",
            ["step1.lfs.found"] = "Git LFS: あり（{0}）",
            ["step1.lfs.missing"] = "Git LFS: なし（今は不要です）",
            ["step1.download"] = "ダウンロードページを開く",
            ["step1.recheck"] = "再確認",

            ["step2.title"] = "プロジェクト設定",
            ["step2.ok"] = "アセットをテキスト形式で保存し、.meta ファイルを表示する設定になっています。",
            ["step2.explain"] = "履歴を正しく扱うために、アセットをテキスト形式（Force Text）で保存し、.meta ファイルを表示（Visible Meta Files）する必要があります。",
            ["step2.current"] = "現在の設定: Asset Serialization = {0} / Version Control = {1}",
            ["step2.apply"] = "設定する",

            ["step3.title"] = "無視ファイル",
            ["step3.ok"] = ".gitignore と .gitattributes に Shiori の設定があります。",
            ["step3.explain"] = "Library や Temp など、履歴に残す必要のないフォルダを除外する設定を .gitignore と .gitattributes に追記します。既に書かれている内容はそのまま残します。",
            ["step3.apply"] = "追記する",

            ["step4.title"] = "最初の保存",
            ["step4.explain"] = "プロジェクトの今の状態を最初の履歴として保存します。",
            ["step4.done"] = "最初の保存が済んでいます。",
            ["step4.mismatch"] = "このプロジェクトは別のリポジトリ（{0}）の中にあります。この構成には対応していません。",
            ["step4.identity"] = "履歴に残す名前とメールアドレスを入力してください。この PC のこのプロジェクトにだけ保存されます。",
            ["step4.name"] = "名前",
            ["step4.email"] = "メールアドレス",
            ["step4.identity.required"] = "名前とメールアドレスの両方を入力してください。",
            ["step4.apply"] = "最初の保存をする",
            ["step4.progress"] = "Shiori: 最初の保存",
        };
    }
}

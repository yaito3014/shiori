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

            ["simple.save.title"] = "保存",
            ["simple.save.message.label"] = "メモ",
            ["simple.save.button"] = "保存",
            ["simple.save.nochanges"] = "変更はありません",
            ["simple.save.changes"] = "変更 {0} 件（追加 {1}、変更 {2}、削除 {3}）",
            ["simple.save.progress"] = "Shiori: 保存",
            ["simple.meta.title"] = ".meta ファイルの不整合が {0} 件あります（保存はできます）",
            ["simple.meta.missing"] = ".meta がありません: {0}",
            ["simple.meta.orphan"] = "対応するファイルがありません: {0}",

            ["simple.history.title"] = "履歴",
            ["simple.history.refresh"] = "更新",
            ["simple.history.empty"] = "まだ履歴がありません。",
            ["simple.history.more"] = "さらに読み込む",
            ["simple.history.current"] = "現在",
            ["simple.history.files"] = "{0} 件",
            ["simple.detail.empty"] = "履歴を選ぶと、変更されたファイルが表示されます。",
            ["simple.detail.nofiles"] = "変更されたファイルはありません。",
            ["simple.detail.by"] = "{0:yyyy/MM/dd HH:mm} ・ {1}",
            ["row.metaonly"] = "{0}（.meta のみ）",

            ["restore.button"] = "この時点に戻す",
            ["restore.dialog.title"] = "この時点に戻す",
            ["restore.dialog.dirty"] = "現在の未保存の変更は失われます。先に保存しますか？\n\n戻す先: {0}（{1}）",
            ["restore.dialog.clean"] = "この時点に戻します。よろしいですか？\n\n戻す先: {0}（{1}）",
            ["restore.dialog.savefirst"] = "保存してから戻す",
            ["restore.dialog.discard"] = "保存せずに戻す",
            ["restore.dialog.ok"] = "戻す",
            ["restore.dialog.cancel"] = "キャンセル",
            ["restore.progress"] = "Shiori: 戻す",
            ["restore.done"] = "「{0}」の時点に戻しました。",
            ["restore.nochange"] = "すでに「{0}」の時点と同じ状態です。",
            ["restore.stashed"] = "戻す前の変更は別に保管してあります。",
            ["restore.projectsettings"] = "プロジェクト設定も戻しました。反映には Unity の再起動が必要な設定があります。",

            ["mode.label"] = "モード",
            ["mode.simple"] = "かんたん",
            ["mode.detail"] = "詳細",

            ["detail.files.title"] = "変更ファイル",
            ["detail.files.empty"] = "変更はありません",
            ["detail.refresh"] = "更新",
            ["detail.diff.select"] = "ファイルを選ぶと差分が表示されます。",
            ["detail.diff.none"] = "表示できる差分はありません（バイナリ、または内容の変更なし）。",
            ["detail.diff.truncated"] = "長いため先頭 {0} 行までを表示しています。",

            ["time.now"] = "たった今",
            ["time.minutes"] = "{0} 分前",
            ["time.hours"] = "{0} 時間前",
            ["time.days"] = "{0} 日前",
            ["time.date"] = "{0:yyyy/MM/dd}",

            ["kind.added"] = "追加",
            ["kind.modified"] = "変更",
            ["kind.deleted"] = "削除",
            ["kind.renamed"] = "名前変更",
            ["kind.copied"] = "コピー",
            ["kind.typechanged"] = "種類変更",
            ["kind.unmerged"] = "競合",
            ["kind.ignored"] = "無視",
            ["kind.unknown"] = "不明",
        };
    }
}

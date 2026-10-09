using System.Collections.Generic;

namespace Shiori.Editor
{
    /// <summary>
    /// Japanese UI strings. Plain, short sentences for people who do not know git.
    /// Simple-mode vocabulary is fixed: 保存 / 履歴 / 戻す / バリエーション / 同期.
    /// </summary>
    internal static class JapaneseStrings
    {
        public static readonly IReadOnlyDictionary<string, string> Table = new Dictionary<string, string>
        {
            ["window.title"] = "Shiori",

            ["lock.compiling"] = "スクリプトをコンパイル中です。終わるまでお待ちください。",
            ["lock.updating"] = "Unity がアセットを更新中です。終わるまでお待ちください。",
            ["lock.playmode"] = "再生中は使えません。再生を止めてください。",
            ["lock.gitbusy"] = "処理中です。少しお待ちください。",

            ["error.git"] = "git でエラーが起きました（コード {0}）。\n{1}",
            ["error.generic"] = "エラーが起きました。\n{0}",
            ["error.settings"] = "設定ファイルを読めませんでした。\n{0}",
            ["status.checking"] = "確認中…",
            ["status.loading"] = "読み込み中…",

            ["wizard.title"] = "はじめの設定",
            ["wizard.intro"] = "このプロジェクトで履歴を使う準備をします。上から順に進めてください。",
            ["wizard.finish.message"] = "準備ができました。",
            ["wizard.finish.button"] = "完了",

            ["step.state.done"] = "完了",
            ["step.state.todo"] = "未完了",
            ["step.state.blocked"] = "先に上の手順を済ませてください",

            ["step1.title"] = "git の確認",
            ["step1.found"] = "git {0} が見つかりました。",
            ["step1.path"] = "場所: {0}",
            ["step1.notfound"] = "git が見つかりません。Shiori は git を使って履歴を保存します。\n下のボタンから git をインストールし、Unity を再起動してから「再確認」を押してください。",
            ["step1.tooold"] = "git {0} は古いため使えません（2.30 以上が必要です）。\n新しい git をインストールしてから「再確認」を押してください。",
            ["step1.lfs.found"] = "Git LFS: あり（{0}）",
            ["step1.lfs.missing"] = "Git LFS: なし（今は不要です）",
            ["step1.download"] = "ダウンロードページを開く",
            ["step1.recheck"] = "再確認",

            ["step2.title"] = "プロジェクト設定",
            ["step2.ok"] = "履歴に必要な設定になっています。",
            ["step2.explain"] = "履歴を正しく扱うために、アセットをテキスト形式で保存し、.meta ファイルを表示する設定にします。",
            ["step2.current"] = "今の設定: Asset Serialization = {0} / Version Control = {1}",
            ["step2.apply"] = "設定する",

            ["step3.title"] = "履歴に含めないもの",
            ["step3.ok"] = ".gitignore と .gitattributes に Shiori の設定が入っています。",
            ["step3.explain"] = "Library や Temp など、履歴に残す必要のないフォルダを除外する設定を .gitignore と .gitattributes に追記します。すでにある内容はそのまま残します。",
            ["step3.apply"] = "追記する",

            ["step.ext.error"] = "この手順を確認できませんでした。\n{0}",

            ["step4.title"] = "最初の保存",
            ["step4.explain"] = "プロジェクトの今の状態を、最初の履歴として保存します。",
            ["step4.done"] = "最初の保存は済んでいます。",
            ["step4.mismatch"] = "このプロジェクトは別のリポジトリ（{0}）の中にあります。この形には対応していません。",
            ["step4.identity"] = "履歴に残す名前とメールアドレスを入力してください。この PC のこのプロジェクトにだけ保存されます。",
            ["step4.name"] = "名前",
            ["step4.email"] = "メールアドレス",
            ["step4.identity.required"] = "名前とメールアドレスを両方入力してください。",
            ["step4.apply"] = "最初の保存をする",
            ["step4.progress"] = "Shiori: 最初の保存",

            ["simple.save.title"] = "保存",
            ["simple.save.message.label"] = "メモ",
            ["simple.save.button"] = "保存",
            ["simple.save.tooltip"] = "メモを書いて Enter でも保存できます。",
            ["simple.save.nochanges"] = "変更はありません",
            ["simple.save.changes"] = "{0} 件の変更（追加 {1} / 変更 {2} / 削除 {3}）",
            ["simple.save.progress"] = "Shiori: 保存",
            ["simple.meta.title"] = ".meta ファイルに問題が {0} 件あります（保存はできます）",
            ["simple.meta.missing"] = ".meta がありません: {0}",
            ["simple.meta.orphan"] = "元のファイルがありません: {0}",

            ["simple.history.title"] = "履歴",
            ["simple.history.refresh"] = "更新",
            ["simple.history.empty"] = "まだ履歴はありません。",
            ["simple.history.more"] = "もっと見る",
            ["simple.history.current"] = "現在",
            ["simple.history.files"] = "{0} 件",
            ["simple.detail.empty"] = "履歴を選ぶと、変わったファイルが表示されます。",
            ["simple.detail.nofiles"] = "変わったファイルはありません。",
            ["simple.detail.by"] = "{0:yyyy/MM/dd HH:mm} ・ {1}",
            ["row.metaonly"] = "{0}（.meta のみ）",

            ["restore.button"] = "この時点に戻す",
            ["restore.tooltip"] = "プロジェクト全体をこの時点の状態に戻します。",
            ["restore.nothing"] = "今がこの時点の状態なので、戻すものはありません。",
            ["restore.dialog.title"] = "この時点に戻す",
            ["restore.dialog.dirty"] = "現在の未保存の変更は失われます。先に保存しますか？\n\n戻す先: {0}（{1}）",
            ["restore.dialog.clean"] = "この時点に戻します。よろしいですか？\n\n戻す先: {0}（{1}）",
            ["restore.dialog.savefirst"] = "保存してから戻す",
            ["restore.dialog.discard"] = "保存せずに戻す",
            ["restore.dialog.ok"] = "戻す",
            ["restore.dialog.cancel"] = "キャンセル",
            ["restore.progress"] = "Shiori: 戻す",
            ["restore.done"] = "「{0}」の時点に戻しました。",
            ["restore.nochange"] = "すでに「{0}」の時点と同じです。",
            ["restore.stashed"] = "戻す前の変更は別に取ってあります。",
            ["restore.projectsettings"] = "プロジェクト設定も戻しました。反映には Unity の再起動が必要な場合があります。",

            ["settings.title"] = "Shiori",
            ["settings.git.title"] = "git の場所",
            ["settings.git.help"] = "空のままなら自動で探します。",
            ["settings.git.path"] = "git のパス",
            ["settings.git.browse"] = "参照…",
            ["settings.git.browse.title"] = "git.exe を選んでください",
            ["settings.git.auto"] = "自動で探す",
            ["settings.git.notfound"] = "git が見つかりません。",
            ["settings.mode.title"] = "表示",
            ["settings.modes.label"] = "使うモード",
            ["settings.modes.both"] = "かんたんと詳細",
            ["settings.modes.simple"] = "かんたんだけ",
            ["settings.modes.detail"] = "詳細だけ",
            ["settings.modes.help"] = "「かんたんと詳細」にすると、ウィンドウの上部で切り替えられます。",
            ["settings.user.file"] = "保存先: UserSettings/Shiori.json（この PC だけ）",
            ["settings.setup.title"] = "はじめの設定",
            ["settings.setup.completed"] = "はじめの設定は完了している",
            ["settings.setup.help"] = "チェックを外すと、次に開いたときに「はじめの設定」をもう一度表示します。",
            ["settings.ignore.title"] = "履歴に含めないもの",
            ["settings.ignore.missing"] = ".gitignore と .gitattributes に Shiori の設定がありません。",
            ["settings.ext.title"] = "追加の設定",
            ["settings.project.file"] = "保存先: ProjectSettings/Shiori.json（履歴に含まれます）",

            ["mode.label"] = "モード",
            ["mode.simple"] = "かんたん",
            ["mode.detail"] = "詳細",

            ["detail.files.title"] = "変更ファイル",
            ["detail.files.empty"] = "変更はありません",
            ["detail.refresh"] = "更新",
            ["detail.diff.select"] = "ファイルを選ぶと差分が表示されます。",
            ["detail.diff.none"] = "表示できる差分はありません。",
            ["detail.diff.truncated"] = "長いので先頭 {0} 行だけ表示しています。",

            ["time.now"] = "たった今",
            ["time.minutes"] = "{0} 分前",
            ["time.hours"] = "{0} 時間前",
            ["time.days"] = "{0} 日前",
            ["time.date"] = "{0:yyyy/MM/dd}",

            ["kind.added"] = "追加",
            ["kind.modified"] = "変更",
            ["kind.deleted"] = "削除",
            ["kind.renamed"] = "名前の変更",
            ["kind.copied"] = "コピー",
            ["kind.typechanged"] = "種類の変更",
            ["kind.unmerged"] = "競合",
            ["kind.ignored"] = "無視",
            ["kind.unknown"] = "不明",
        };
    }
}

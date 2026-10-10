# Shiori for Unity

Unity Editor の中で動く Git クライアントです。Git を知らない人向けの「かんたんモード」
（保存 / 履歴 / 戻す）と、通常の Git クライアント相当の「詳細モード」を、
同じデータモデルの上に載せています。

- 対応 Unity: 2022.3 以上（Unity 6 を含む）
- 必要なもの: git 2.30 以上（自動インストールはしません）
- VRChat 固有の機能は別パッケージ `shiori-vrchat`（予定）に置きます。本パッケージは VRChat SDK に依存しません。

## インストール

### VCC / ALCOM（VRChat のプロジェクト）

[yaito3014.github.io/vpm-listing](https://yaito3014.github.io/vpm-listing/) の「VCC / ALCOM に追加」を押すと、
リポジトリとして登録されます。あとはプロジェクトの「Manage Project」から Shiori を追加してください。
VRChat 向けの追加機能は同じ一覧にある [Shiori for VRChat](https://github.com/yaito3014/shiori-vrchat) を一緒に入れます。

### Package Manager（それ以外の Unity プロジェクト）

`Project Settings > Package Manager` の Scoped Registries に次を追加すると、Package Manager の「My Registries」から
インストールと更新ができます。Unity 6.3 以降では署名済み（発行元 KakeyamaY）と表示されます。

| 項目 | 値 |
| --- | --- |
| Name | `yaito3014` |
| URL | `https://yaito3014.github.io/vpm-listing` |
| Scope(s) | `com.yaito3014` |

登録したくない場合は tarball から入れられます。

1. [最新のリリース](https://github.com/yaito3014/shiori/releases/latest)から `com.yaito3014.shiori-<version>.tgz` をダウンロードします。
2. Package Manager の「+」から "Install package from tarball..." を選び、ダウンロードしたファイルを指定します。

この `.tgz` は Unity の署名付きです。Unity 6.3 以降では Package Manager に署名済み（発行元 KakeyamaY）と表示されます。
Unity 2022.3 でもそのまま入れられます。

`main` の最新を試したいときは "Add package from git URL" で `https://github.com/yaito3014/shiori.git` を指定します
（署名はありません）。

## 使い方

`Window > Shiori` でウィンドウを開きます。

### はじめの設定

初めて開いたときは、4 つの手順を上から順にボタンで進めます。

1. git の確認（見つからないときはダウンロードページを案内します）
2. プロジェクト設定（Asset Serialization = Force Text、Version Control = Visible Meta Files）
3. 履歴に含めないもの（`.gitignore` / `.gitattributes` に Shiori のブロックを追記。既存の内容は残します）
4. 最初の保存

### かんたんモード

- **保存**: メモを書いて「保存」を押す（またはメモ欄で Enter）と、今のプロジェクトの状態が履歴に残ります。
  メモは 60 文字までです。Unity でまだ保存していない変更（マテリアルの色など）も先に書き出します。
- **履歴**: 保存した時点が新しい順に並びます。選ぶと、その時に変わったファイルが見えます。
- **戻す**: 履歴の「この時点に戻す」で、プロジェクト全体をその時点の状態にします。
  未保存の変更があるときは「保存してから戻す / 保存せずに戻す / キャンセル」を選べます。
  「保存せずに戻す」でも変更は捨てず、別に取っておきます（`git stash`）。
- **送信**: 保存した履歴を、送信先（GitHub などに作った空の非公開リポジトリ）に送ります。
  送信先は、はじめの設定の最後（任意）か `Project Settings > Shiori` で URL を貼って設定します。
  「確認する」は何も書き換えずに、その URL が使えるかを調べます。サインインは初めて送信するときに出る画面で行い、
  Shiori はパスワードやトークンを持ちません。保存していない変更は送られません。送信先に、この PC にない保存が
  あるときは何も変えずに止まります。
- **受信**: ほかの PC から送信された保存を受け取り、プロジェクトを最新にします。保存していない変更があるときは、
  先に保存してから受信します。この PC とほかの PC で別々に保存していたときは何も変えずに止まります
  （合わせる機能は今後の版で入ります）。かんたんモードは数分ごとに送信先をそっと確かめ、
  「受信できる保存 N 件」と知らせます。この確認でサインイン画面が出ることはありません。
- **取ってある変更**: 「保存せずに戻す」で取っておいた変更は、かんたんモードの「取ってある変更」に並びます。
  「取り出す」で今のプロジェクトに戻せます。今の変更が保存済みで、取っておいたあとの履歴と同じファイルを
  変えていないときだけ取り出すので、ファイルが混ざることはありません。取り出しても一覧からは消えません。
  戻した結果は新しい履歴として記録されるので、履歴は一直線のままです。

### 詳細モード

ウィンドウ上部の「詳細」で切り替えます。git を知っている人向けに、同じ操作を git の言葉で並べます。
中身はかんたんモードと同じ処理なので、安全のための確認や制限も同じです。

- **上部のツールバー**: メッセージを書いて「ステージ済みをコミット」（何もステージしていなければ「すべてコミット」）、「プッシュ」「プル」
  （プルは fast-forward のみ）、origin との差（↑ 未プッシュ / ↓ 未取り込み）。
- **変更**: 「ステージ済み」と「未ステージ」の 2 つの一覧と、選んだファイルの差分（それぞれの側の差分）。
  各行の「+」「−」でファイルごとにステージ / ステージから外せます。アセットとその .meta、新しいフォルダーの .meta は
  一緒に動きます。ステージから外してもファイルの中身は変わりません。プレハブ（.prefab）とマテリアル（.mat）は
  「Unity 表示」で、GameObject・コンポーネント・プロパティごとに何が変わったかを見られます（「テキスト」で元の差分）。
- **ログ**: コミットの一覧と、選んだコミットで変わったファイル。「この状態に戻す」はかんたんモードの「戻す」と同じです。
- **スタッシュ**: すべての stash と「適用」。作業ツリーがきれいで、スタッシュのあとで同じファイルが変わっていない
  ときだけ適用し、スタッシュは残します。

行（hunk）単位のステージやブランチはまだありません（ブランチはバリエーションと一緒に入る予定です）。

### 設定

- `Preferences > Shiori`: git のパス、モード（`UserSettings/Shiori.json`、リポジトリには含まれません）
- `Project Settings > Shiori`: はじめの設定の完了フラグ、履歴に含めないものの再追記（`ProjectSettings/Shiori.json`）

### 安全のために

- コンパイル中、再生中、処理中はボタンが無効になり、理由が上部に表示されます。
- `reset --hard` や `checkout -- .` は使いません。作業ツリーを書き換える前後では
  `AssetDatabase` の自動更新を止め、終わってから同期的に更新します。
- 認証は git（credential manager）に任せます。Shiori はトークンやパスワードを保持しません。

## 拡張パッケージ

VRChat など環境ごとの機能は別パッケージとして追加できます（例: [shiori-vrchat](https://github.com/yaito3014/shiori-vrchat)）。
拡張パッケージは `Shiori.Core` の `ShioriExtension` を継承したクラスを 1 つ置くだけで、Shiori が自動で見つけます。

- はじめの設定に手順を追加できます（「履歴に含めないもの」の後、「最初の保存」の前）。
  追加した手順は `Project Settings > Shiori` からもう一度実行できます。
- `.gitignore` / `.gitattributes` に、パッケージごとのブロックを追記できます（Shiori のブロックや手書きの行は触りません）。
- ウィンドウ右上に短い状態表示（例: 「PC 向け」）、空のメモ欄に例文、「戻す」の確認に注意書きを足せます。
- はじめの設定のあとで追加の手順が「未完了」に戻ると（例: VCC でパッケージを足した）、
  かんたんモードの上部にその手順がボタン付きで表示されます。
- 保存 / 戻す の前後に処理を挟めます。
- `ProjectSettings/Shiori.json` の `extensions` にパッケージごとの設定を持てます。

拡張側のコードは UI を持たず、文言とボタンを返すだけなので、Unity なしの NUnit でテストできます。
設計の経緯は `docs~/adr/0002-extension-api-in-core.md` にあります。

## 開発

リポジトリのルートがパッケージ本体です。検証用の Unity プロジェクトはリポジトリの外に置きます
（中に置くと、ウィザードがリポジトリの入れ子を検出して止まるため）。

```
# 検証用プロジェクトを作る（既定: ../shiori-dev/<major.minor>）
pwsh Tools~/New-DevProject.ps1 -UnityVersion 2022.3.22f1
pwsh Tools~/New-DevProject.ps1 -UnityVersion 6000.6.0f1

# EditMode テストを回して結果を要約する
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 6000.6.0f1

# コンパイルだけ
pwsh Tools~/Test-DevProject.ps1 -UnityVersion 2022.3.22f1 -CompileOnly

# Unity 6 向けに署名した .tgz を ../dist/ に出す（Unity アカウントのパスワードを聞かれます）
pwsh Tools~/Sign-Package.ps1 -Username <email> -Organization <Unity Cloud の組織 ID>
```

CI（GitHub Actions）は同じスクリプトでプロジェクトを生成してから GameCI でテストを回します。
設計上の判断は `docs~/adr/` に残しています（配布物には含まれません）。

## ライセンス

MIT

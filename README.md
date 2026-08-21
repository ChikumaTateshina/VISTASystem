# VISTASystem

**V**RChat **I**ntegrated **S**tatus **T**racking **A**ssistant **System**

[![build](https://github.com/ChikumaTateshina/VISTASystem/actions/workflows/build.yml/badge.svg)](https://github.com/ChikumaTateshina/VISTASystem/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

デスクトップでアクティブになっているアプリケーションを検知し、VRChat のステータスを自動で切り替える Windows 常駐ツールです。

「ゲーム中は Do Not Disturb」「作業アプリを開いたら Ask Me」といったルールをあらかじめ登録しておくだけで、
ウィンドウを切り替えるたびにステータスとステータスメッセージが自動で更新されます。

> **ログイン情報の送信先について**
> 入力されたユーザー名・パスワード・二段階認証コードは、**VRChat 公式 API (`https://api.vrchat.cloud`) にのみ送信されます。**
> 作者を含む第三者のサーバーへ送信されることは一切ありません。詳細は [設定ファイルとプライバシー](#設定ファイルとプライバシー) を参照してください。

---

## 主な機能

- **アクティブウィンドウの監視** — 前面のプロセスを 5 秒間隔でチェックし、変化を検知します。
- **プロセス → ステータスのマッピング** — プロセス名ごとにステータス（Join Me / Online / Ask Me / Do Not Disturb）とステータスメッセージを設定できます。
- **アプリ選択ダイアログ** — 現在起動中のウィンドウ一覧から、絞り込み検索でプロセスを選べます。プロセス名を手で調べる必要はありません。
- **二段階認証に対応** — TOTP（認証アプリ）と Email OTP の両方に対応しています。
- **セッションの復元** — 一度ログインすればセッション Cookie を保存し、次回起動時は自動でログイン状態になります。
- **タスクトレイ常駐** — ウィンドウを閉じてもトレイに常駐し、監視を続けます。多重起動も防止します。
- **認証情報の暗号化保存** — パスワードと Cookie は Windows DPAPI でユーザー単位に暗号化して保存します。

## 動作環境

| 項目 | 内容 |
| --- | --- |
| OS | Windows 10 / 11 (x64) |
| ランタイム | 不要（自己完結型の単一 EXE として配布） |
| ビルド時 | [.NET 10 SDK](https://dotnet.microsoft.com/download) |

## インストール

### リリース版を使う

[Releases](https://github.com/ChikumaTateshina/VISTASystem/releases) から `VISTASystem.exe` をダウンロードし、任意のフォルダに置いて実行してください。
.NET ランタイムのインストールは不要です。

### ソースからビルドする

```sh
git clone https://github.com/ChikumaTateshina/VISTASystem.git
cd VISTASystem
dotnet build -c Release
```

配布用の単一 EXE を作る場合は、同梱の `publish.bat` を実行するか以下を実行します。

```sh
dotnet publish VISTASystem.csproj -p:PublishProfile=Release
```

`publish\VISTASystem.exe` が生成されます（自己完結型・単一ファイル・win-x64）。

## プロジェクト構成

責務ごとにディレクトリを分けています。

```
src/
├── Program.cs              エントリポイント（多重起動の防止）
├── Monitoring/             アクティブアプリの監視ロジック
│   ├── ActiveApplicationMonitor.cs  前面プロセスの変化検出
│   └── StatusMapping.cs             ステータス更新ルール
├── Interop/                Win32 API 呼び出し
│   ├── NativeMethods.cs        P/Invoke 宣言
│   ├── ActiveWindowDetector.cs 前面ウィンドウの検出・実行中アプリの列挙
│   └── AppEntry.cs             実行中アプリ 1 件の表示名とプロセス名
├── VRChat/                 VRChat API との通信
│   ├── VRChatApiClient.cs      ログイン・二段階認証・ステータス更新
│   ├── LoginResult.cs          ログイン試行の結果
│   └── VrcStatus.cs            表示名と API 値の対応表
├── Settings/               設定の永続化
│   ├── AppSettings.cs          settings.json の読み書き
│   └── DataProtector.cs        DPAPI による暗号化
└── Ui/                     画面
    ├── MainForm.cs             ログイン・監視・設定の制御
    ├── MainForm.Layout.cs      コントロールの生成とレイアウト
    ├── ProcessPickerDialog.cs  アプリ選択ダイアログ
    ├── TrayIcon.cs             トレイアイコンの生成
    └── UI.cs                   配色とコントロールのファクトリ
```

## 使い方

1. **ログイン**
   VRChat のユーザー名（またはメールアドレス）とパスワードを入力し、`Login` を押します。
2. **二段階認証**
   二段階認証が有効な場合はコード入力欄が有効になります。認証アプリのコードを入力して `Verify` を押してください。
   メールで届くコードを使う場合は `Email OTP` にチェックを入れてから入力します。
3. **マッピングを登録**
   `アプリを選択` を押して起動中のアプリからプロセスを選び、対応するステータスとステータスメッセージを入力します。
   行はいくつでも追加でき、不要な行は削除できます。
4. **監視を開始**
   `▶ 監視開始` を押すと監視が始まります。以降、登録したアプリが前面に来るたびにステータスが更新されます。
5. **常駐**
   ウィンドウを閉じるとタスクトレイに格納され、監視は継続します。
   トレイアイコンのダブルクリックでウィンドウを再表示、右クリックメニューから設定のリセットや終了ができます。

> 監視中はマッピングの編集はできません。変更する場合は一度 `■ 監視停止` を押してください。

## 設定ファイルとプライバシー

設定は次の場所に保存されます。

```
%AppData%\VISTASystem\settings.json
```

- パスワードとセッション Cookie は **Windows DPAPI（CurrentUser スコープ）** で暗号化されます。同じ Windows ユーザーアカウント以外では復号できません。
- 旧バージョン（VRCStatus）の設定が `%AppData%\VRChatStatusUpdater\settings.json` に残っている場合は、初回起動時に自動で読み込まれます。

### ログイン情報の送信先

**入力されたログイン情報（ユーザー名・パスワード・二段階認証コード）は、VRChat 公式 API 以外のどこにも送信されません。**

- 本ソフトウェアが通信する相手は `https://api.vrchat.cloud/api/1` **ただひとつ**です。それ以外の接続先は存在しません。
- 作者や第三者が運営するサーバーは経由しません。認証情報の収集・送信・共有は一切行いません。
- 利用統計・アクセス解析・クラッシュレポート・自動アップデート確認などの通信も行いません。
- **アクティブなアプリ名（プロセス名）が外部へ送信されることはありません。** マッピングの判定はすべて手元の PC 内で完結し、VRChat へ送るのは設定したステータスとステータスメッセージだけです。

通信を行っているのは以下の 4 か所のみで、いずれも [src/VRChat/VRChatApiClient.cs](src/VRChat/VRChatApiClient.cs) に集約されています。

| 送信先 | 送信するもの | タイミング |
| --- | --- | --- |
| `GET /auth/user` | ユーザー名・パスワード（Basic 認証）／セッション Cookie | ログイン時・セッション復元時・二段階認証の直後 |
| `POST /auth/twofactorauth/{totp\|emailotp}/verify` | 二段階認証コード | 二段階認証時 |
| `PUT /users/{userId}` | ステータスとステータスメッセージ | 監視中にアプリが切り替わったとき |
| `DELETE /auth/session` | （なし） | ログアウト時 |

ソースコードは全文公開されています。上記は実際のコードで確認できます。

## 注意事項

- 本ソフトウェアは VRChat 公式のツールではありません。VRChat Inc. とは無関係の非公式プロジェクトです。
- VRChat の API 利用にあたっては、[VRChat の利用規約](https://hello.vrchat.com/legal) および API の利用ガイドラインを遵守してください。
- ステータス更新はアクティブウィンドウが切り替わったときのみ行われ、不要な API リクエストは発生しません。
- 本ソフトウェアの利用によって生じたいかなる損害についても、作者は責任を負いません。自己責任でご利用ください。

## ライセンス

[MIT License](LICENSE) — Copyright (c) 2026 ChikumaTateshina

開発への参加方法は [CONTRIBUTING.md](CONTRIBUTING.md)、脆弱性の報告方法は [SECURITY.md](SECURITY.md) を参照してください。

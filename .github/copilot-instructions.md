# SessionGuard プロジェクト - 開発仕様

## プロジェクト概要
SessionGuard は .NET 10 製の Windows サービスアプリケーションです。設定ファイルに基づいてWindowsユーザーのセッションを監視し、強制ログアウトポリシーを適用します。

## 機能

### 1. ユーザー識別
- 設定ファイルに対象ユーザーアカウントを列挙する
- 設定ファイルに記載されたユーザーのみが強制ログアウトの対象となる
- 記載のないユーザーは一切処理しない

### 2. 強制ログアウト条件
各条件は独立して動作する。

#### 2a. 1日の利用時間上限
- ユーザーごとに当日の累積ログイン時間を計測する
- ログイン中は継続的に時間を加算する
- 日本標準時（JST / UTC+9）の0時に累積時間をリセットする
- 累積利用時間が設定値を超えた場合、強制ログアウトを実行する
- 強制ログアウト後に再ログインした場合、当日の累積時間はリセットせず引き続き加算される

#### 2b. 禁止時間帯
- 設定ファイルに複数の禁止時間帯（開始時刻・終了時刻のペア）を設定できる
- 現在時刻がいずれかの禁止時間帯に入った場合、利用時間に関わらず強制ログアウトを実行する
- 禁止時間帯と利用時間上限の条件はそれぞれ独立して動作する
- 禁止時間帯が終了した後に再ログインした場合、当日の累積利用時間は保持されたまま加算が再開される

## アーキテクチャ

### コアコンポーネント

1. **SessionConfig.cs**
   - `appsettings.json` にバインドする設定クラス
   - プロパティ:
     - `TargetUsers`: ログアウトポリシーの対象となる Windows ユーザー名リスト
     - `ProhibitedTimeWindows`: 禁止時間帯のリスト（複数設定可能）。各エントリは `StartTime`（開始時刻）と `EndTime`（終了時刻）を持つ（24時間表記、例: `"22:00"`）
     - `MaxDailyUsageMinutes`: 1日の累積利用時間の上限（分）
     - `EnableLogout`: 実際のログアウト実行の有効/無効フラグ（bool）
     - `CheckIntervalSeconds`: セッションチェックの実行間隔（秒）

2. **SessionInfo.cs**
   - ユーザーごとのセッション状態を管理する
   - 保持する情報: ユーザー名、現在のログイン開始時刻、当日の累積利用時間
   - JST基準の0時リセット処理を提供する

3. **SessionManager.cs**
   - セッション管理のコアロジック
   - 現在ログイン中の Windows ユーザーを取得する
   - `TargetUsers` に基づいて対象ユーザーを絞り込む
   - 以下の強制ログアウト条件を評価する:
     - 累積利用時間が `MaxDailyUsageMinutes` を超えているか
     - 現在の JST 時刻が禁止時間帯に入っているか
   - 同一日内のログイン・ログアウトをまたいでユーザーごとの `SessionInfo` 状態を保持する

4. **LogoutHandler.cs**
   - 指定ユーザーの Windows ログアウトを実行する
   - ログアウト前にユーザーへ警告メッセージを表示する

5. **Worker.cs**
   - `BackgroundService` を継承したバックグラウンドサービス
   - `IOptionsMonitor<SessionConfig>` によりサービス再起動なしで設定変更を即時反映する
   - 定期実行: ログイン中ユーザーの取得 → 利用時間の更新 → 条件評価 → 該当時にログアウト実行

## プロジェクト構成
```
SessionGuard/
├── SessionGuard.csproj
├── Program.cs
├── Worker.cs
├── SessionConfig.cs
├── SessionInfo.cs
├── SessionManager.cs
├── LogoutHandler.cs
├── Properties/
├── appsettings.json
├── appsettings.Development.json
├── README.md
└── .github/
    └── copilot-instructions.md
```

## 設定ファイル例（`appsettings.json`）
```json
{
  "SessionGuard": {
    "TargetUsers": ["alice", "bob"],
    "ProhibitedTimeWindows": [
      { "StartTime": "22:00", "EndTime": "08:00" },
      { "StartTime": "12:00", "EndTime": "13:00" }
    ],
    "MaxDailyUsageMinutes": 120,
    "EnableLogout": true,
    "CheckIntervalSeconds": 60
  }
}
```

## ビルド・デプロイ

### ビルド
```powershell
dotnet build -c Release
```

### Windows サービスとしてインストール
```powershell
sc.exe create SessionGuard binPath="C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0\SessionGuard.exe"
net start SessionGuard
```

### サービスのアンインストール
```powershell
net stop SessionGuard
sc.exe delete SessionGuard
```

## チェックリスト

- [x] .NET 10 Worker Service テンプレートでプロジェクト作成
- [x] セッション管理機能の実装
- [x] 必要な拡張機能のインストール（不要）
- [x] プロジェクトのビルド確認（.NET 10 でビルド成功）
- [x] Windows サービスとしてのデプロイ準備完了
- [x] ドキュメント整備

## ビルド状態
✅ エラーなしでビルド成功
✅ 依存関係解決済み（.NET 10 SDK）
✅ Windows サービスとしてデプロイ可能

## 今後の開発課題

1. **ユニットテスト**: SessionManager のログアウト条件判定・0時リセットのテスト作成
2. **統合テスト**: Windows サービスのインストール・ライフサイクルのテスト
3. **ログ強化**: Windows イベントログへの対応
4. **設定UI**: 非技術者向けの設定ツール作成
5. **デプロイ**: MSI インストーラーとしてのパッケージング

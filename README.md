# SessionGuard - Windows Session Manager Service

SessionGuard は、Windows OS 上で動作するバックグラウンドサービスです。設定ファイルに基づいて対象ユーザーのセッションを監視し、条件に合致した場合に強制ログアウトを実行します。

## 機能

### 1. ユーザー識別
- 設定ファイルに記載されたユーザーのみが強制ログアウトの対象
- 記載のないユーザーは一切処理しない

### 2. 禁止時間帯
- 複数の禁止時間帯（開始・終了時刻）を設定可能
- 現在時刻がいずれかの禁止時間帯に入ったら強制ログアウト
- 日をまたぐ時間帯（例: 22:00〜08:00）も設定可能

### 3. 1日の利用時間上限
- 当日の累積ログイン時間が設定値を超えたら強制ログアウト
- 日本標準時（JST）の0時に累積時間をリセット
- 禁止時間帯によるログアウト後に再ログインしても、累積時間は引き継がれる

### 4. リアルタイム設定反映
- `appsettings.json` を編集・保存するだけで設定が即時反映
- サービス再起動不要

## システム要件

- **OS**: Windows 10/11 または Windows Server 2016 以上
- **.NET**: .NET 10 ランタイム（`SelfContained=false` のため必要）

## インストール手順

### 1. ビルド（単一ファイル発行）

```powershell
cd C:\wk\repos\SessionGuard\SessionGuard

dotnet publish -c Release -r win-x64
```

**発行結果：** `bin\Release\net10.0\win-x64\publish\` に以下が生成されます

```
publish\
├── SessionGuard.exe          ← 全DLLを含む単一ファイル
├── appsettings.json          ← 設定ファイル
├── SessionGuard.pdb          ← デバッグシンボル
└── SessionGuard.runtimeconfig.json
```

### 2. Windows サービスとして登録

**管理者権限の PowerShell** で実行してください。

```powershell
# サービス登録
sc.exe create SessionGuard binPath="C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0\win-x64\publish\SessionGuard.exe" start= auto

# サービス起動
net start SessionGuard

# 状態確認
sc.exe query SessionGuard
```

### 3. サービスの停止・削除

```powershell
net stop SessionGuard
sc.exe delete SessionGuard
```

### 4. 更新デプロイ手順

```powershell
net stop SessionGuard
cd C:\wk\repos\SessionGuard\SessionGuard
dotnet publish -c Release -r win-x64
net start SessionGuard
```

## 設定

`bin\Release\net10.0\win-x64\publish\appsettings.json` を編集します（サービス再起動不要で即時反映）。

```json
{
  "SessionConfig": {
    "TargetUsers": [
      "user1",
      "user2"
    ],
    "ProhibitedTimeWindows": [
      { "StartTime": "22:00", "EndTime": "08:00", "Memo": "夜間・早朝の使用禁止" },
      { "StartTime": "12:00", "EndTime": "13:00", "Memo": "昼休み" }
    ],
    "MaxDailyUsageMinutes": 120,
    "EnableLogout": true,
    "CheckIntervalSeconds": 15
  }
}
```

### 設定項目

| 項目 | 型 | 説明 |
|------|-----|------|
| `TargetUsers` | string[] | 強制ログアウト対象のユーザー名リスト。空の場合は誰も対象にならない |
| `ProhibitedTimeWindows` | array | 禁止時間帯のリスト（複数設定可） |
| `ProhibitedTimeWindows[].StartTime` | string | 禁止時間帯の開始時刻（HH:mm形式） |
| `ProhibitedTimeWindows[].EndTime` | string | 禁止時間帯の終了時刻（HH:mm形式） |
| `ProhibitedTimeWindows[].Memo` | string | 備考（任意） |
| `MaxDailyUsageMinutes` | int | 1日の累積利用時間の上限（分単位） |
| `EnableLogout` | bool | ログアウト実行の有効/無効 |
| `CheckIntervalSeconds` | int | セッションチェックの実行間隔（秒単位） |

## ログ

ログファイルは `publish\logs\` ディレクトリに日次で保存されます（7日間保持）。

```
logs\SessionGuard-YYYYMMDD.log
```

```powershell
# リアルタイム監視
Get-Content -Path "C:\wk\repos\SessionGuard\SessionGuard\bin\Release\net10.0\win-x64\publish\logs\SessionGuard-*.log" -Wait
```

### ログレベル

| レベル | 説明 |
|--------|------|
| `[INF]` | 通常動作（起動・停止・セッション状態） |
| `[WRN]` | ログアウト条件検出・ユーザー変更など |
| `[ERR]` | エラー発生 |
| `[CRI]` | ログアウト実行 |

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
└── appsettings.json
```

## トラブルシューティング

### サービスが起動しない
- `dotnet publish -c Release -r win-x64` が成功しているか確認
- `appsettings.json` が正しい JSON 形式か確認

### ログアウトが実行されない
- `EnableLogout` が `true` になっているか確認
- `TargetUsers` に対象ユーザー名が記載されているか確認
- ログファイルでエラーが出ていないか確認

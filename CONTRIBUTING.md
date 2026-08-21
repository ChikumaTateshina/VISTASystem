# Contributing to VISTASystem

Issue や Pull Request を歓迎します。変更前に既存 Issue を確認し、大きな仕様変更は先に Issue で相談してください。

## 開発環境

- Windows 10 / 11
- .NET 10 SDK

```powershell
dotnet restore
dotnet build -c Release
```

Pull Request には、変更理由、確認方法、UI を変更した場合はスクリーンショットを含めてください。認証情報、Cookie、実際の `settings.json` は添付しないでください。

## コーディング方針

- Nullable 参照型とコンパイラ警告を有効に保つ
- UI、Windows 固有処理、VRChat API、設定保存の責務を分離する
- ネットワーク処理は非同期にし、応答とキャンセルを適切に扱う
- ユーザー名、パスワード、Cookie、ユーザー ID をログへ追加しない

提出前に `dotnet build -c Release` と `dotnet publish VISTASystem.csproj -p:PublishProfile=Release` が成功することを確認してください。

# Contributing to VISTASystem


## 開発環境

- Windows 10 / 11
- .NET 10 SDK

```powershell
dotnet restore
dotnet build -c Release
```
## コーディング方針

- Nullable 参照型とコンパイラ警告を有効に保つ
- UI、Windows 固有処理、VRChat API、設定保存の責務を分離する
- ネットワーク処理は非同期にし、応答とキャンセルを適切に扱う
- ユーザー名、パスワード、Cookie、ユーザー ID をログへ追加しない


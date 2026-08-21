# Security Policy

脆弱性や、認証情報が漏えいする可能性のある問題は公開 Issue に投稿しないでください。GitHub の Security Advisories から非公開で報告してください。

報告には、影響範囲、再現手順、確認したバージョンを含めてください。VRChat のパスワード、Cookie、二段階認証コードなどの実データは送らないでください。

VISTASystem はパスワードとセッション Cookie を Windows DPAPI の CurrentUser スコープで暗号化します。ただし、同じ Windows ユーザーとして動くプロセスからの保護を保証するものではありません。

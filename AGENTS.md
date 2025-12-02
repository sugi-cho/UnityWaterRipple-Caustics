# AGENTS

このリポジトリで作業するエージェント向けの基本方針です。

- 返答は日本語で、簡潔かつ丁寧に行うこと。
- Git 大型ファイルストレージ（LFS）は使用しない。
- 大容量の画像・モデルを前提にしない構成。必要になった場合は相談してから追加する。
- Unity 6000.2.8f1 で作成されたプロジェクト。変更時は `ProjectSettings/ProjectVersion.txt` を更新確認する。
- 生成物（Library、Temp、Logs、Build/Builds、UserSettings など）はコミットしない。.gitignore 参照。
- 新しいパッケージを追加したときは `Packages/manifest.json` と `Packages/packages-lock.json` の差分を確認する。

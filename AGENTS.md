# AGENTS

このリポジトリで作業するエージェント向けの基本方針です。

- 返答は日本語で、簡潔かつ丁寧に。
- Git LFS は使わない。大容量画像・モデルが必要になったら事前に相談。
- Unity 6000.2.8f1 プロジェクト。バージョン変更時は `ProjectSettings/ProjectVersion.txt` を必ず確認。
- 生成物（Library, Temp, Logs, Build/Builds, UserSettings など）はコミットしない。.gitignore を遵守。
- 新規パッケージ追加時は `Packages/manifest.json` と `Packages/packages-lock.json` の差分を確認。
- 作業履歴は `COMMUNICATION_LOG.md` に時系列で追記すること。
- フォトンスプラット等の動的生成メッシュ・マテリアルは `HideAndDontSave` を付与し、シーンに保存しない（保存されていた場合は除去する）。

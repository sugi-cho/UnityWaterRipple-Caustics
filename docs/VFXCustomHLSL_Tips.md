# VFX Custom HLSL 関数 実装メモ

これまで遭遇したコンパイル・ランタイムエラーの回避策をまとめる。Custom Block/Operator 実装時のチェックリストとして利用。

- **シグネチャは一意に**  
  - VFX は関数オーバーロード解決が弱い。引数違いの同名関数を複数置かない。必須の形だけ残す。
- **先頭引数は `inout VFXAttributes attributes`**  
  - Custom HLSL Block はこれが必須。`attributes` を使わないと「invalid subscript 'particleId'」になるので、`attributes.particleId` などで必ず参照する。
- **粒子IDは `attributes.particleId` を直接使用**  
  - グローバル `particleId` ではなく attributes 経由で参照。未使用扱いを防ぐため、処理内で確実に使う。
- **テクスチャと UAV の型を混在させない**  
  - 同じプロパティ名で `Texture2D` と `GraphicsBuffer` を切り替えない。PositionIntensityTexture などは Texture2D/RWTexture2D に統一し、Buffer は別名で持つ。
- **RWTexture2D を UAV で生成する**  
  - `enableRandomWrite = true` を付けて生成し、VFX の exposed プロパティに外部からセット。VFX 内部でデフォルト生成させない。
- **include パスはプロジェクト基準で統一**  
- `Packages/com.sugi-cho.hamoninteractive/Runtime/Shaders/HLSL/...` からの相対に揃え、VFXCommon など既定の include を重複させない。
- **再帰・多重委譲を避ける**  
  - 関数同士の呼び合いで再帰と誤判定される場合がある。1 関数内に処理をまとめ、必要最小限のヘルパーに留める。
- **gridSize などは型を揃える**  
  - VFX から渡しやすい `float2` を受け取り、内部で `uint2` にキャスト。型違いシグネチャは持たない。
- **行末/改行は CRLF に統一**  
  - 混在するとスタックトレース行番号がずれる。HLSL は CRLF に揃える。

運用フロー:
1. 関数は 1 シグネチャで書く（attributes 先頭、必要な引数のみ）。
2. Particle ID を attributes 内で使用していることを確認。
3. RWTexture2D が UAV で生成されているか確認（スクリプト側）。
4. VFX Graph プロパティ名と型の整合をチェック（Texture2D vs Buffer）。
5. 変更後は VFX を再インポートしてコンパイルログを確認。

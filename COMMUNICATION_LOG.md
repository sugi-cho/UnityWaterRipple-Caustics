# COMMUNICATION LOG

依頼と対応の履歴を時系列でメモする。新しい項目は末尾に追記してください。

- 2025-12-02 23:55 「gitで基本設定して」→ git init、.gitignore/.gitattributes/AGENTS を追加し初回コミット。
- 2025-12-03 00:10 「波紋シミュレーション方針を検討」→ Computeベースの設計案を提示。
- 2025-12-03 00:32 「方針どおり実装＆インスペクタで確認したい」→ RippleCompute/スクリプト/カスタムインスペクタを実装しコミット。
- 2025-12-03 00:40 「コンパイルエラー修正」→ StateTexture の参照名を修正しコミット。
- 2025-12-03 00:48 「マウス離したらForceクリア＆プレビュー更新」→ マウス入力処理とエディタ更新を追加しコミット。
- 2025-12-03 00:55 「波が遅いので速く調整したい」→ waveSpeed上限拡大と timeScale 追加でスピード調整可能にしコミット。
- 2025-12-03 01:00 「Force軌跡を残さずその場だけ反映」→ 毎フレームForceをクリアし瞬間入力に変更しコミット。
- 2025-12-03 01:12 「マウスデバッグを別クラスに分離」→ RippleMouseDebug を新設し、RippleSimulation からマウス関連を分離しコミット。
- 2025-12-03 01:18 「Resultを任意RTへBlitで出力」→ outputTexture と autoBlit を追加し、サイズ差をBlitで吸収できるよう対応中。
- 2025-12-03 01:25 「波が減衰せず暴れる」→ Computeに振幅減衰パラメータを追加し、高さと速度に時間減衰を適用。
- 2025-12-03 01:32 「Result法線をZ+基準で青系に」→ 法線計算を (x,y,z)=(左右,上下,奥行) で z+ を基準にエンコードするよう変更。
- 2025-12-03 02:20 「波動方程式で多重波紋にしたい」→ Computeを中央差分の波動方程式へ置き換え、高さと前フレーム高さで2階時間微分を表現し、力を加えたときに多重の波紋が出るよう調整。
- 2025-12-03 02:36 「波が消えるときのチラつき」→ CFLクランプと微小粘性・ゼロクリップを追加し、高周波ノイズを抑制。
- 2025-12-03 02:50 「フレームレート非依存にしたい」→ RippleSimulation に固定タイムステップ/サブステップ方式を追加し、60fps相当で積み上げる可変＋固定ハイブリッドに対応。
- 2025-12-03 03:05 「パラメータ説明のREADMEが欲しい」→ README.md を作成し主要パラメータ・入力テクスチャを整理。
- 2025-12-03 03:25 「端の処理を選べるように」→ 水平方向/垂直方向それぞれ Bounce・Absorb・Wrap を選択できるように Compute/スクリプト/README を更新。
- 2025-12-03 03:35 「インスペクタに新パラメータが出ない」→ カスタムインスペクタに Edge モードと固定タイムステップ関連のプロパティを追加し、状態プレビューのラベルも更新。
- 2025-12-03 03:45 「Absorbでも反射する」→ 流れの逆流時も端処理を適用できるよう、エッジ処理関数を追加し clamping を廃止。Absorb/Warp/Bounce が正しく効くよう修正。
- 2025-12-03 04:10 「ResultRTから簡易コースティクス生成を実装して」→ CausticsHit.compute と CausticsAdd.shader、CausticsRenderer コンポーネントを追加し、Computeで反射先を算出して加算ブレンドで描画するパイプラインを実装。READMEに使い方を追記。
- 2025-12-03 04:25 「カメラ位置でコースティクス結果が変わる」→ CausticsAdd の頂点変換をクリップ空間直書きに変更し、描画がシーンカメラ行列に依存しないよう修正。
- 2025-12-03 04:45 「Quad の表裏で法線が逆」→ Unity Quad が -Z 向きであることに合わせ、Source/Target の法線を -forward で Compute に渡すよう修正し、法線ブレンドもその向きを基準に。
- 2025-12-03 05:00 「可視化用メッシュを自動生成し調整したい」→ CausticsRenderer を ExecuteAlways 化し、エディタ上で床/壁のQuadを自動生成・サイズ調整できるように追加。Transformのスケール・位置はそのまま利用し、法線は right×up で算出。
- 2025-12-03 05:15 「Play/StopでFloor/Wallが増える」→ 自動生成メッシュを名前で再利用し、OnDisableで安全に破棄する処理を追加して増殖を防止。
- 2025-12-03 19:51 「RayとPlaneの交点を返すCustomHLSLを追加して」→ VFX用HLSLを新規作成し、RayPlaneIntersection 関数を実装（Custom HLSL Operator 用に out を廃止し、距離を返す仕様に修正）。
- 2025-12-03 20:27 「direction を normal で反射したベクトルを返す関数を追加して」→ ReflectDirection を RayPlaneIntersection.hlsl に追加。
- 2025-12-03 20:30 「Caustics VFX Graph の可視化をコミット＆プッシュして」→ 現在の差分（CausticsVisalize.vfx、hamon.unity、RayPlaneIntersection.hlsl など）をコミットし push。
- 2025-12-04 17:41 RippleCompute デッドゾーンを緩和し、微小高さも伝搬するようノイズカットを削除
- 2025-12-04 17:57 MakeNormals 改善: Sobelで勾配平滑化、NormalNoiseThreshold削除（Compute/C#）
- 2025-12-04 18:04 RippleSimulationEditor NullReference対応: NormalNoiseThreshold関連のSerializedPropertyとInspector項目を削除

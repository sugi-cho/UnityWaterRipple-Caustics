# UnityHamonInteractive

波紋シミュレーションの簡易マニュアル。

## 主要パラメータ（`RippleSimulation`）
- `resolution` : シミュレーション解像度。
- `waveSpeed` : 波速。高いほど広がりが速く強い。
- `timeScale` : 時間スケール。全体の進行を加速/減速。
- `damping` : 運動エネルギーの線形減衰（ダンパー）。
- `amplitudeDecay` : 振幅の時間減衰。残響を抑える。
- `depthScale` : 深度テクスチャによる波速変化の強さ。
- `flowScale` : フローテクスチャによる移流の強さ。
- `boundaryBounce` : 境界での反射係数。
- `forceToVelocity` : ブラシ/外力を速度に変換する係数。
- `horizontalEdge` / `verticalEdge` : テクスチャ端での処理モード（Bounce=反射, Absorb=端でクランプし伝搬を止める, Wrap=反対側へループ）。
- `horizontalEdge` / `verticalEdge` : テクスチャ端での処理モード（Bounce=反射, Absorb=端から約12pxを強減衰させて消す, Wrap=反対側へループ）。
  - `normalGradScale` : Sobel 勾配の倍率。法線の強さを調整。
  - `normalBlurRadius` : 法線ガウシアンぼかし半径(0~3)。
  - `normalBlurSigma` : ぼかしのシグマ。

### 時間ステップ
- `useFixedTimeStep` : 固定ステップを使うか（推奨オン）。
- `fixedTimeStep` : 1 ステップ時間（デフォルト 1/60s）。
- `maxSubSteps` : 1 フレームでの最大サブステップ数。
- `maxAccumulatedTime` : フレーム落ち時に積める上限時間。

## コースティクス（VFX/ShaderGraph版・推奨）
- コンポーネント: `CausticsMeshRenderer` + `CausticsMeshVfx` (VFX Graph) + `CausticsMesh.shadergraph`
- 入力: `RippleSimulation.ResultTexture`（ワールド法線/高さ）、`gridSize` は Ripple 解像度そのまま。
- 仕組み: `CausticsMeshRenderer` が (W+1,H+1) メッシュと PositionIntensityTexture を生成し、VFX にバインド。VFX Graph で頂点ごとの位置/強度を書き込み、ShaderGraph で可視化する。
- 注意: PositionIntensityTexture は外部生成(UAV有効)を VFX プロパティにセットする。VFX 内部でデフォルト生成させない。
- 微調整: ShaderGraph 側で色/強度カーブ、VFX Graph 側で積算ロジックを編集して見た目を調整。
- 備考: 旧「Compute版コースティクス」パイプラインはプロジェクトから除去済み。Caustics は本構成のみを使用。

## 入力テクスチャ
- `boundaryTexture` : 白=水面、黒=地形。反射/固体セル判定に使用。
- `depthTexture` : 0~1 の水深。深いほど波速が上がる。
- `flowTexture` : RG で XY 流速。水面の移流に使用。
- `externalForce` : 外部から与える力テクスチャ。`useExternalForce` が有効なとき加算。

## 内部RTと出力
- `State` : R=現在高さ, G=前フレーム高さ。**RGFloat**
- `Result` : RGB=Y-up ワールド法線(非パック, -1..1), A=高さ。**ARGBFloat**。`outputTexture` を設定すれば自動 Blit。
- `ResultTemp` : MakeNormals の出力（ARGBFloat）→ BlurNormals で平滑化。
- `Force` : ブラシ／外力の蓄積用。**RFloat**

## ブラシ入力
`AddForceBrush(uv, radius, strength, falloff)` で力を与えられます。`forceToVelocity` と合わせて強度を調整してください。

### マウス／タッチデバッグ
- `RippleMouseDebug` が MeshCollider へレイキャストし、ヒット UV を RippleSimulation に適用します。
- `targetMeshCollider` を指定するとそのコライダー以外への入力を無視できます（スクリーン座標デバッグは廃止）。


## 注意
- `ProjectSettings/ProjectVersion.txt` で Unity 6000.2.8f1 を確認すること。
- 大きな生成物（Library, Temp, Logs など）はコミットしないでください。


## Movie
https://github.com/user-attachments/assets/9df4a116-d174-4b62-95b5-62a68fcabf17


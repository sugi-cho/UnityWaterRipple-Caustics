using UnityEngine;
using UnityEngine.VFX;

namespace HamonInteractive
{
    /// <summary>
    /// Ripple の結果テクスチャ解像度に合わせて (W+1,H+1) 分割の Quad メッシュを生成し、
    /// メッシュの VertexBuffer と書き込み用 GraphicsBuffer を VFX Graph にバインドする。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CausticsMeshRenderer : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RippleSimulation ripple;
        [SerializeField] private VisualEffect vfx;

        [Header("VFX プロパティ名")]
        [SerializeField] private string rippleTextureProperty = "RippleTexture";
        [SerializeField] private string meshProperty = "SourceMesh";
        [SerializeField] private string vertexBufferProperty = "MeshVertexBuffer";
        [SerializeField] private string positionBufferProperty = "PositionBuffer";
        [SerializeField] private string intensityBufferProperty = "IntensityBuffer";
        [SerializeField] private string positionIntensityTextureProperty = "PositionIntensityTexture";
        [SerializeField] private string vertexCountProperty = "VertexCount";

        [Header("動作設定")]
        [SerializeField] private bool autoUpdate = true;

        private Mesh _mesh;
        private GraphicsBuffer _meshVB;
        private GraphicsBuffer _positionsBuffer;
        private GraphicsBuffer _intensityBuffer;
        private RenderTexture _positionIntensityRT;
        private Vector2Int _currentResolution = Vector2Int.zero;

        private void OnEnable()
        {
            Refresh(true);
        }

        private void OnDisable()
        {
            ReleaseBuffers();
            CleanupMesh();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Refresh(true);
            }
        }

        private void Update()
        {
            if (autoUpdate)
            {
                Refresh(false);
            }
        }

        /// <summary>
        /// Ripple の解像度を確認し、必要ならメッシュとバッファを再生成して VFX に渡す。
        /// </summary>
        public void Refresh(bool force)
        {
            if (ripple == null || vfx == null) return;
            var tex = ripple.ResultTexture;
            if (tex == null) return;

            var res = new Vector2Int(tex.width, tex.height);
            if (force || res != _currentResolution || _mesh == null)
            {
                _currentResolution = res;
                RebuildMesh(res);
                RebuildBuffers(res);
            }

            BindToVfx(tex);
        }

        private void RebuildMesh(Vector2Int res)
        {
            CleanupMesh();

            _mesh = BuildGridMesh(res.x, res.y);
            _mesh.name = "CausticsMeshRenderer_Mesh";
            _mesh.hideFlags = HideFlags.HideAndDontSave;
            _mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            _mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            _mesh.UploadMeshData(false);
            _meshVB = _mesh.GetVertexBuffer(0);
        }

        private void RebuildBuffers(Vector2Int res)
        {
            ReleaseBuffers();

            int vertCount = (res.x + 1) * (res.y + 1);
            _positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertCount, sizeof(float) * 3);
            _intensityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertCount, sizeof(float));

            RebuildPositionIntensityRT(res);
        }

        private void RebuildPositionIntensityRT(Vector2Int res)
        {
            ReleasePositionIntensityRT();

            int w = res.x + 1;
            int h = res.y + 1;
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBFloat, 0)
            {
                enableRandomWrite = true,
                sRGB = false,
                msaaSamples = 1,
                volumeDepth = 1
            };
            _positionIntensityRT = new RenderTexture(desc)
            {
                name = "Caustics_PositionIntensity",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _positionIntensityRT.Create();
        }

        private void ReleasePositionIntensityRT()
        {
            if (_positionIntensityRT == null) return;
            _positionIntensityRT.Release();
            if (Application.isPlaying)
                Destroy(_positionIntensityRT);
            else
                DestroyImmediate(_positionIntensityRT);
            _positionIntensityRT = null;
        }

        private void BindToVfx(Texture rippleTex)
        {
            if (_mesh != null)
            {
                vfx.SetMesh(meshProperty, _mesh);
            }
            if (_meshVB == null && _mesh != null)
            {
                _meshVB = _mesh.GetVertexBuffer(0);
            }
            if (_meshVB != null)
            {
                vfx.SetGraphicsBuffer(vertexBufferProperty, _meshVB);
            }
            if (_positionsBuffer != null)
            {
                vfx.SetGraphicsBuffer(positionBufferProperty, _positionsBuffer);
            }
            if (_intensityBuffer != null)
            {
                vfx.SetGraphicsBuffer(intensityBufferProperty, _intensityBuffer);
            }
            if (_positionIntensityRT != null)
            {
                vfx.SetTexture(positionIntensityTextureProperty, _positionIntensityRT);
            }
            if (rippleTex != null)
            {
                vfx.SetTexture(rippleTextureProperty, rippleTex);
            }
            if (!string.IsNullOrEmpty(vertexCountProperty) && _positionsBuffer != null)
            {
                vfx.SetInt(vertexCountProperty, _positionsBuffer.count);
            }
        }

        private void ReleaseBuffers()
        {
            _meshVB = null; // 所有権は Mesh 側
            _positionsBuffer?.Release();
            _positionsBuffer = null;
            _intensityBuffer?.Release();
            _intensityBuffer = null;
            ReleasePositionIntensityRT();
        }

        private void CleanupMesh()
        {
            _meshVB = null;
            if (_mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
                _mesh = null;
            }
        }

        /// <summary>
        /// (W+1,H+1) グリッドの Quad メッシュを生成。サイズは 1x1、中心が原点。
        /// </summary>
        private static Mesh BuildGridMesh(int w, int h)
        {
            int vertW = w + 1;
            int vertH = h + 1;
            int vertCount = vertW * vertH;

            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            int idx = 0;
            for (int y = 0; y < vertH; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < vertW; x++)
                {
                    float u = (float)x / w;
                    verts[idx] = new Vector3(u - 0.5f, 0f, v - 0.5f); // 幅1,高さ1の平面
                    uvs[idx] = new Vector2(u, v);
                    idx++;
                }
            }

            var indices = new int[w * h * 6];
            int t = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i0 = y * vertW + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + vertW;
                    int i3 = i2 + 1;
                    indices[t++] = i0; indices[t++] = i2; indices[t++] = i1;
                    indices[t++] = i1; indices[t++] = i2; indices[t++] = i3;
                }
            }

            var mesh = new Mesh
            {
                indexFormat = vertCount > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using HamonInteractive;
using UnityEditor;
using UnityEngine;

namespace HamonInteractive.Editor
{
    [CustomEditor(typeof(RippleSimulation))]
    public class RippleSimulationEditor : UnityEditor.Editor
    {
        private SerializedProperty _rippleCompute;
        private SerializedProperty _resolution;
        private SerializedProperty _waveSpeed;
        private SerializedProperty _timeScale;
        private SerializedProperty _damping;
        private SerializedProperty _amplitudeDecay;
        private SerializedProperty _depthScale;
        private SerializedProperty _flowScale;
        private SerializedProperty _boundaryBounce;
        private SerializedProperty _forceToVelocity;

        private SerializedProperty _normalGradScale;
        private SerializedProperty _normalBlurRadius;
        private SerializedProperty _normalBlurSigma;

        private SerializedProperty _horizontalEdge;
        private SerializedProperty _verticalEdge;

        private SerializedProperty _useFixedTimeStep;
        private SerializedProperty _fixedTimeStep;
        private SerializedProperty _maxSubSteps;
        private SerializedProperty _maxAccumulatedTime;

        private SerializedProperty _outputTexture;
        private SerializedProperty _autoBlitResult;

        private SerializedProperty _boundaryTexture;
        private SerializedProperty _depthTexture;
        private SerializedProperty _flowTexture;
        private SerializedProperty _externalForce;
        private SerializedProperty _useExternalForce;

        private SerializedProperty _showPreviews;

        private void OnEnable()
        {
            _rippleCompute = serializedObject.FindProperty("rippleCompute");
            _resolution = serializedObject.FindProperty("resolution");
            _waveSpeed = serializedObject.FindProperty("waveSpeed");
            _timeScale = serializedObject.FindProperty("timeScale");
            _damping = serializedObject.FindProperty("damping");
            _amplitudeDecay = serializedObject.FindProperty("amplitudeDecay");
            _depthScale = serializedObject.FindProperty("depthScale");
            _flowScale = serializedObject.FindProperty("flowScale");
            _boundaryBounce = serializedObject.FindProperty("boundaryBounce");
            _forceToVelocity = serializedObject.FindProperty("forceToVelocity");
            _normalGradScale = serializedObject.FindProperty("normalGradScale");
            _normalBlurRadius = serializedObject.FindProperty("normalBlurRadius");
            _normalBlurSigma = serializedObject.FindProperty("normalBlurSigma");

            _horizontalEdge = serializedObject.FindProperty("horizontalEdge");
            _verticalEdge = serializedObject.FindProperty("verticalEdge");

            _useFixedTimeStep = serializedObject.FindProperty("useFixedTimeStep");
            _fixedTimeStep = serializedObject.FindProperty("fixedTimeStep");
            _maxSubSteps = serializedObject.FindProperty("maxSubSteps");
            _maxAccumulatedTime = serializedObject.FindProperty("maxAccumulatedTime");

            _outputTexture = serializedObject.FindProperty("outputTexture");
            _autoBlitResult = serializedObject.FindProperty("autoBlitResult");

            _boundaryTexture = serializedObject.FindProperty("boundaryTexture");
            _depthTexture = serializedObject.FindProperty("depthTexture");
            _flowTexture = serializedObject.FindProperty("flowTexture");
            _externalForce = serializedObject.FindProperty("externalForce");
            _useExternalForce = serializedObject.FindProperty("useExternalForce");

            _showPreviews = serializedObject.FindProperty("showPreviews");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_rippleCompute);
            EditorGUILayout.PropertyField(_resolution);
            EditorGUILayout.PropertyField(_waveSpeed);
            EditorGUILayout.PropertyField(_timeScale, new GUIContent("Time Scale"));
            EditorGUILayout.PropertyField(_damping);
            EditorGUILayout.PropertyField(_amplitudeDecay, new GUIContent("Amplitude Decay"));
            EditorGUILayout.PropertyField(_depthScale);
            EditorGUILayout.PropertyField(_flowScale);
            EditorGUILayout.PropertyField(_boundaryBounce);
            EditorGUILayout.PropertyField(_forceToVelocity);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Normals", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_normalGradScale, new GUIContent("Gradient Scale"));
            EditorGUILayout.PropertyField(_normalBlurRadius, new GUIContent("Blur Radius (px)"));
            EditorGUILayout.PropertyField(_normalBlurSigma, new GUIContent("Blur Sigma"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Edge Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_horizontalEdge, new GUIContent("Horizontal Edge"));
            EditorGUILayout.PropertyField(_verticalEdge, new GUIContent("Vertical Edge"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Time Step", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useFixedTimeStep, new GUIContent("Use Fixed Time Step"));
            if (_useFixedTimeStep.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_fixedTimeStep, new GUIContent("Fixed Time Step (s)"));
                EditorGUILayout.PropertyField(_maxSubSteps, new GUIContent("Max Sub Steps"));
                EditorGUILayout.PropertyField(_maxAccumulatedTime, new GUIContent("Max Accumulated Time (s)"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox("可変Δtモード。大きなΔtになると不安定になりやすいので注意。", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_outputTexture, new GUIContent("Output Texture"));
            EditorGUILayout.PropertyField(_autoBlitResult, new GUIContent("Auto Blit Result"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Input Textures", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_boundaryTexture);
            EditorGUILayout.PropertyField(_depthTexture);
            EditorGUILayout.PropertyField(_flowTexture);
            EditorGUILayout.PropertyField(_externalForce);
            EditorGUILayout.PropertyField(_useExternalForce, new GUIContent("Use External Force"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_showPreviews, new GUIContent("Show Previews"));

            serializedObject.ApplyModifiedProperties();

            var sim = target as RippleSimulation;
            if (sim == null) return;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Force")) sim.ClearForceTexture();
                if (GUILayout.Button("Reset Simulation")) sim.ResetSimulation();
            }

            if (_showPreviews.boolValue)
            {
                DrawPreview(sim.DebugResultTexture, "Result (WorldNormal/Height)");
                DrawPreview(sim.DebugStateTexture, "State (R=Height G=PrevHeight)");
                DrawPreview(sim.DebugForceTexture, "Force");

                EditorGUILayout.LabelField("RenderTextures", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("State RT", sim.StateTexture, typeof(RenderTexture), false);
                EditorGUILayout.ObjectField("Result RT", sim.ResultTexture, typeof(RenderTexture), false);
                EditorGUILayout.ObjectField("Force RT", sim.ForceTexture, typeof(RenderTexture), false);
            }

            if (Application.isPlaying && _showPreviews.boolValue)
            {
                // keep previews updating during play mode
                Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private void DrawPreview(Texture tex, string label)
        {
            if (tex == null) return;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            float aspect = tex.height == 0 ? 1f : (float)tex.width / tex.height;
            Rect rect = GUILayoutUtility.GetAspectRect(aspect);
            EditorGUI.DrawPreviewTexture(rect, tex, null, ScaleMode.StretchToFill);
        }
    }
}


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
        private SerializedProperty _damping;
        private SerializedProperty _depthScale;
        private SerializedProperty _flowScale;
        private SerializedProperty _boundaryBounce;
        private SerializedProperty _forceToVelocity;

        private SerializedProperty _boundaryTexture;
        private SerializedProperty _depthTexture;
        private SerializedProperty _flowTexture;
        private SerializedProperty _externalForce;
        private SerializedProperty _useExternalForce;

        private SerializedProperty _enableMouseInput;
        private SerializedProperty _mouseRadius;
        private SerializedProperty _mouseStrength;
        private SerializedProperty _mouseFalloff;
        private SerializedProperty _mouseCamera;

        private SerializedProperty _showPreviews;

        private void OnEnable()
        {
            _rippleCompute = serializedObject.FindProperty("rippleCompute");
            _resolution = serializedObject.FindProperty("resolution");
            _waveSpeed = serializedObject.FindProperty("waveSpeed");
            _damping = serializedObject.FindProperty("damping");
            _depthScale = serializedObject.FindProperty("depthScale");
            _flowScale = serializedObject.FindProperty("flowScale");
            _boundaryBounce = serializedObject.FindProperty("boundaryBounce");
            _forceToVelocity = serializedObject.FindProperty("forceToVelocity");

            _boundaryTexture = serializedObject.FindProperty("boundaryTexture");
            _depthTexture = serializedObject.FindProperty("depthTexture");
            _flowTexture = serializedObject.FindProperty("flowTexture");
            _externalForce = serializedObject.FindProperty("externalForce");
            _useExternalForce = serializedObject.FindProperty("useExternalForce");

            _enableMouseInput = serializedObject.FindProperty("enableMouseInput");
            _mouseRadius = serializedObject.FindProperty("mouseRadius");
            _mouseStrength = serializedObject.FindProperty("mouseStrength");
            _mouseFalloff = serializedObject.FindProperty("mouseFalloff");
            _mouseCamera = serializedObject.FindProperty("mouseCamera");

            _showPreviews = serializedObject.FindProperty("showPreviews");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_rippleCompute);
            EditorGUILayout.PropertyField(_resolution);
            EditorGUILayout.PropertyField(_waveSpeed);
            EditorGUILayout.PropertyField(_damping);
            EditorGUILayout.PropertyField(_depthScale);
            EditorGUILayout.PropertyField(_flowScale);
            EditorGUILayout.PropertyField(_boundaryBounce);
            EditorGUILayout.PropertyField(_forceToVelocity);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Input Textures", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_boundaryTexture);
            EditorGUILayout.PropertyField(_depthTexture);
            EditorGUILayout.PropertyField(_flowTexture);
            EditorGUILayout.PropertyField(_externalForce);
            EditorGUILayout.PropertyField(_useExternalForce, new GUIContent("Use External Force"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mouse Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableMouseInput);
            using (new EditorGUI.DisabledScope(!_enableMouseInput.boolValue))
            {
                EditorGUILayout.PropertyField(_mouseRadius, new GUIContent("Brush Radius (UV)"));
                EditorGUILayout.PropertyField(_mouseStrength, new GUIContent("Brush Strength"));
                EditorGUILayout.PropertyField(_mouseFalloff, new GUIContent("Brush Falloff"));
                EditorGUILayout.PropertyField(_mouseCamera, new GUIContent("Camera"));
            }

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
                DrawPreview(sim.DebugResultTexture, "Result (Normal/Height)");
                DrawPreview(sim.DebugStateTexture, "State (R=Height G=Velocity)");
                DrawPreview(sim.DebugForceTexture, "Force");

                EditorGUILayout.LabelField("RenderTextures", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("State RT", sim.StateTexture, typeof(RenderTexture), false);
                EditorGUILayout.ObjectField("Result RT", sim.ResultTexture, typeof(RenderTexture), false);
                EditorGUILayout.ObjectField("Force RT", sim.ForceTexture, typeof(RenderTexture), false);
            }

            if (Application.isPlaying && _showPreviews.boolValue)
            {
                // プレイ中にプレビューを更新し続ける
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

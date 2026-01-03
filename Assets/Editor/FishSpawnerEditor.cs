#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FishSpawner))]
public class FishSpawnerEditor : Editor
{
    private bool showAssetTypes = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var spawner = (FishSpawner)target;
        if (spawner == null) return;

        if (spawner.fishTypeAssets == null || spawner.fishTypeAssets.Count == 0)
            return;

        EditorGUILayout.Space(8);
        showAssetTypes = EditorGUILayout.Foldout(showAssetTypes, "Balik Turleri (Asset) - Detay", true);
        if (!showAssetTypes) return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < spawner.fishTypeAssets.Count; i++)
        {
            var asset = spawner.fishTypeAssets[i];
            if (asset == null) continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(asset.name, EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                var assetEditor = CreateEditor(asset);
                try
                {
                    EditorGUI.BeginChangeCheck();
                    assetEditor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(asset);
                    }
                }
                finally
                {
                    if (assetEditor != null)
                        DestroyImmediate(assetEditor);
                }
                EditorGUI.indentLevel--;
            }
        }
        EditorGUI.indentLevel--;
    }
}
#endif

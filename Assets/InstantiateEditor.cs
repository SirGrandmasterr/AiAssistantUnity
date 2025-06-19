using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class GltfEditorInstantiator
{
    [MenuItem("Tools/Instantiate GLB In Scene")]
    private static void InstantiateGltfInScene()
    {
        // 1) Path to your glb
        string assetPath = "Assets/Character.glb";
        GameObject glbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (glbAsset == null)
        {
            Debug.LogError($"Couldn’t find GLB at {assetPath}");
            return;
        }

        // 2) Instantiate it as a prefab variant
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(glbAsset);
        instance.name = glbAsset.name;

        // 3) Register with Undo & mark scene dirty
        Undo.RegisterCreatedObjectUndo(instance, "Instantiate GLB");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        // 4) Select it so you can move/rotate immediately
        Selection.activeGameObject = instance;
    }
}

// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEditor;
using UnityEngine;

namespace CivilianSystem.EditorTools
{
    /// <summary>
    /// Removes "missing script" components (dead GUID references) from prefabs / scene objects.
    /// Menu: Tools > Civilian System > Clean Missing Scripts
    /// </summary>
    public static class MissingScriptCleaner
    {
        [MenuItem("Tools/Civilian System/Clean Missing Scripts (Selected)")]
        public static void CleanSelected()
        {
            int removed = 0;
            foreach (var obj in Selection.gameObjects)
                removed += CleanRecursive(obj);

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Clean Missing Scripts",
                removed > 0 ? $"Removed {removed} missing script component(s)."
                            : "Nothing to clean on selected objects.", "OK");
        }

        [MenuItem("Tools/Civilian System/Clean Missing Scripts (All Prefabs In Project)")]
        public static void CleanAllPrefabs()
        {
            int removed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int r = CleanRecursive(root);
                if (r > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    removed += r;
                    Debug.Log($"[Cleaner] {path}: removed {r} missing script(s)");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Clean Missing Scripts",
                removed > 0 ? $"Removed {removed} missing script component(s) across project prefabs."
                            : "No missing scripts found in any prefab.", "OK");
        }

        static int CleanRecursive(GameObject go)
        {
            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform)
                count += CleanRecursive(child.gameObject);
            return count;
        }
    }
}

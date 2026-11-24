// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEditor;
using UnityEngine;

namespace CivilianSystem.EditorTools
{
    /// <summary>
    /// Waypoint creation tool.
    /// - Menu: Tools > Civilian System > Create Waypoint Path (new path at scene view center)
    /// - Inspector buttons: add / remove / reverse / snap to ground / renumber
    /// - Scene view: SHIFT+CLICK on the ground appends a waypoint; handles move them.
    ///   CTRL+SHIFT+CLICK near a segment inserts a waypoint between two others.
    /// </summary>
    [CustomEditor(typeof(WaypointPath))]
    public class WaypointPathEditor : Editor
    {
        WaypointPath path;

        [MenuItem("Tools/Civilian System/Create Waypoint Path")]
        public static void CreatePath()
        {
            var go = new GameObject("WaypointPath");
            go.AddComponent<WaypointPath>();

            // place at scene view center, snapped to ground
            var sv = SceneView.lastActiveSceneView;
            Vector3 pos = Vector3.zero;
            if (sv != null)
            {
                pos = sv.pivot;
                if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 200f))
                    pos = hit.point;
            }
            go.transform.position = pos;

            Undo.RegisterCreatedObjectUndo(go, "Create Waypoint Path");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        void OnEnable() => path = (WaypointPath)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Scene view:\n" +
                "SHIFT + CLICK ground = append waypoint\n" +
                "CTRL + SHIFT + CLICK near line = insert between\n" +
                "Drag handles to move points", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Waypoint")) AddWaypoint(GetNextPosition());
            if (GUILayout.Button("Remove Last")) RemoveLast();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Snap All To Ground")) SnapAllToGround();
            if (GUILayout.Button("Reverse Order")) ReverseOrder();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Collect Children As Waypoints")) CollectChildren();
        }

        // ---------- inspector actions ----------

        Vector3 GetNextPosition()
        {
            int n = path.transform.childCount;
            if (n == 0) return path.transform.position;
            Transform last = path.transform.GetChild(n - 1);
            Vector3 dir = n >= 2
                ? (last.position - path.transform.GetChild(n - 2).position).normalized
                : Vector3.forward;
            return last.position + dir * 3f;
        }

        void AddWaypoint(Vector3 worldPos, int siblingIndex = -1)
        {
            var wp = new GameObject($"WP_{path.transform.childCount}");
            Undo.RegisterCreatedObjectUndo(wp, "Add Waypoint");
            wp.transform.SetParent(path.transform);
            wp.transform.position = worldPos;
            if (siblingIndex >= 0) wp.transform.SetSiblingIndex(siblingIndex);
            RefreshArray();
            Renumber();
        }

        void RemoveLast()
        {
            int n = path.transform.childCount;
            if (n == 0) return;
            Undo.DestroyObjectImmediate(path.transform.GetChild(n - 1).gameObject);
            RefreshArray();
        }

        void SnapAllToGround()
        {
            Undo.RecordObjects(GetChildTransforms(), "Snap Waypoints");
            foreach (Transform t in path.transform)
            {
                if (Physics.Raycast(t.position + Vector3.up * 50f, Vector3.down, out var hit, 200f))
                    t.position = hit.point + Vector3.up * 0.05f;
            }
        }

        void ReverseOrder()
        {
            int n = path.transform.childCount;
            Undo.RecordObject(path.transform, "Reverse Waypoints");
            for (int i = 0; i < n; i++)
                path.transform.GetChild(0).SetSiblingIndex(n - 1 - i);
            RefreshArray();
            Renumber();
        }

        void CollectChildren() { RefreshArray(); Renumber(); }

        void Renumber()
        {
            for (int i = 0; i < path.transform.childCount; i++)
                path.transform.GetChild(i).name = $"WP_{i}";
        }

        void RefreshArray()
        {
            Undo.RecordObject(path, "Update Waypoints");
            var wps = new Transform[path.transform.childCount];
            for (int i = 0; i < wps.Length; i++)
                wps[i] = path.transform.GetChild(i);
            path.waypoints = wps;
            EditorUtility.SetDirty(path);
        }

        Object[] GetChildTransforms()
        {
            var arr = new Object[path.transform.childCount];
            for (int i = 0; i < arr.Length; i++) arr[i] = path.transform.GetChild(i);
            return arr;
        }

        // ---------- scene view ----------

        void OnSceneGUI()
        {
            Event e = Event.current;
            int n = path.transform.childCount;

            // move handles + labels
            for (int i = 0; i < n; i++)
            {
                Transform wp = path.transform.GetChild(i);
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(wp.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(wp, "Move Waypoint");
                    wp.position = newPos;
                }
                Handles.Label(wp.position + Vector3.up * 0.6f, $"  {i}",
                    new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } });
            }

            // shift+click = append / ctrl+shift+click = insert
            if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out var hit, 2000f))
                {
                    Vector3 p = hit.point + Vector3.up * 0.05f;
                    if (e.control && n >= 2)
                        AddWaypoint(p, FindInsertIndex(p));
                    else
                        AddWaypoint(p);
                    e.Use();
                }
            }
        }

        /// <summary>Sibling index for inserting near the closest path segment.</summary>
        int FindInsertIndex(Vector3 point)
        {
            int n = path.transform.childCount;
            float best = float.MaxValue;
            int bestIdx = n;
            bool loop = path.mode == PathMode.Loop;
            int segments = loop ? n : n - 1;

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = path.transform.GetChild(i).position;
                Vector3 b = path.transform.GetChild((i + 1) % n).position;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                float d = (a + ab * t - point).sqrMagnitude;
                if (d < best) { best = d; bestIdx = i + 1; }
            }
            return bestIdx;
        }
    }
}

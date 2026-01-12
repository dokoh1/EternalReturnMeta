// using UnityEngine;
// using UnityEditor;
//
// public class RemoveMissingScriptsTool {
//     [MenuItem("Tools/Cleanup/Remove Missing Scripts In Selected %#r")] // Ctrl+Shift+R
//     static void RemoveMissingScripts() {
//         GameObject[] selectedObjects = Selection.gameObjects;
//
//         int count = 0;
//         foreach (GameObject go in selectedObjects) {
//             int before = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
//             if (before > 0) {
//                 Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
//                 GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
//                 count += before;
//             }
//         }
//
//         Debug.Log($"🧹 Removed {count} missing scripts.");
//     }
// }
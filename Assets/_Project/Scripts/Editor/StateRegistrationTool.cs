#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OblastZero.Core;

namespace OblastZero.EditorTools
{
    /// <summary>
    /// One-click setup: ensures every GameState has a corresponding BaseGameState component
    /// registered as a child of the GameStateMachine in the currently open scene.
    ///
    /// Run via menu: OblastZero → Setup → Register Missing States.
    /// Idempotent — skips states that already exist, only adds the missing ones.
    ///
    /// This is now a convenience rather than a requirement: <c>GameStateMachine.Initialize</c> performs the
    /// same reconciliation at runtime, so forgetting to run this no longer dead-ends a wipe. Running it is
    /// still preferred, because a scene-authored state persists its serialized inspector values (e.g.
    /// <c>TransitionCutsceneState.cutsceneSeconds</c>) where a runtime-created one gets field defaults.
    /// The list of states lives in <see cref="GameStateMachine.DiscoverStateTypes"/> — one source, so the
    /// tool and the runtime fallback can never disagree about what "all states" means.
    /// </summary>
    public static class StateRegistrationTool
    {
        [MenuItem("OblastZero/Setup/Register Missing States")]
        public static void RegisterMissingStates()
        {
            var machine = Object.FindFirstObjectByType<GameStateMachine>();
            if (machine == null)
            {
                EditorUtility.DisplayDialog("State Registration",
                    "No GameStateMachine found in the open scene.\n\n" +
                    "Open Assets/Scenes/_Bootstrap.unity first, then run this again.",
                    "OK");
                return;
            }

            // Every concrete state type that must live as a child of the machine. Shared with the
            // runtime fallback so the two can never drift apart.
            var stateTypes = GameStateMachine.DiscoverStateTypes();

            int added = 0, skipped = 0;
            foreach (var type in stateTypes)
            {
                // Already present anywhere under the machine? Skip.
                var existing = machine.GetComponentInChildren(type, includeInactive: true);
                if (existing != null)
                {
                    skipped++;
                    continue;
                }

                var go = new GameObject(type.Name);
                go.transform.SetParent(machine.transform, false);
                go.AddComponent(type);
                Undo.RegisterCreatedObjectUndo(go, "Register State");
                added++;
                Debug.Log($"[StateRegistrationTool] Added {type.Name} under GameStateMachine.");
            }

            EditorSceneManager.MarkSceneDirty(machine.gameObject.scene);
            EditorSceneManager.SaveScene(machine.gameObject.scene);

            EditorUtility.DisplayDialog("State Registration",
                $"Done.\n\nAdded: {added}\nAlready present: {skipped}\n\nScene saved. Press Play from _Bootstrap.",
                "OK");
        }
    }
}
#endif

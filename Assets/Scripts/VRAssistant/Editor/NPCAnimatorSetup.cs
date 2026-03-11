using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

namespace VRAssistant.Editor
{
    /// <summary>
    /// Automatically configures the NPC Animator Controller with
    /// parameters, states, and transitions.
    ///
    /// Usage: Select your Avatar in the Hierarchy, then go to
    /// Tools → VR Assistant → Setup NPC Animator
    /// </summary>
    public class NPCAnimatorSetup : EditorWindow
    {
        [MenuItem("Tools/VR Assistant/Setup NPC Animator")]
        public static void SetupAnimator()
        {
            // Find the selected object
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Please select the NPC Avatar in the Hierarchy first.", "OK");
                return;
            }

            Animator animator = selected.GetComponent<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Selected object has no Animator component.", "OK");
                return;
            }

            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                // Create a new controller
                string path = "Assets/Animations/NPCAnimatorController.controller";

                // Ensure directory exists
                if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                    AssetDatabase.CreateFolder("Assets", "Animations");

                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                animator.runtimeAnimatorController = controller;
                Debug.Log("[NPCSetup] Created new Animator Controller at " + path);
            }

            // ─── Add Parameters ────────────────────────────────
            AddParameterIfMissing(controller, "State", AnimatorControllerParameterType.Int);
            AddParameterIfMissing(controller, "IsTalking", AnimatorControllerParameterType.Bool);
            AddParameterIfMissing(controller, "GestureIndex", AnimatorControllerParameterType.Int);
            AddParameterIfMissing(controller, "Nod", AnimatorControllerParameterType.Trigger);

            Debug.Log("[NPCSetup] Parameters added: State, IsTalking, GestureIndex, Nod");

            // ─── Get or create the base layer state machine ────
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            // ─── Find existing states or create them ───────────
            AnimatorState idleState = FindOrCreateState(rootStateMachine, "Neutral Idle");
            AnimatorState talkingState = FindOrCreateState(rootStateMachine, "Talking");
            AnimatorState talking1State = FindOrCreateState(rootStateMachine, "Talking1");
            AnimatorState talking2State = FindOrCreateState(rootStateMachine, "Talking2");

            // Set idle as default
            rootStateMachine.defaultState = idleState;

            // ─── Clear existing transitions ────────────────────
            ClearTransitions(idleState);
            ClearTransitions(talkingState);
            ClearTransitions(talking1State);
            ClearTransitions(talking2State);

            // ─── Idle → Talking (when IsTalking becomes true) ──
            var idleToTalking = idleState.AddTransition(talkingState);
            idleToTalking.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            idleToTalking.hasExitTime = false;
            idleToTalking.duration = 0.25f;

            // ─── Talking → Idle (when IsTalking becomes false) ─
            var talkingToIdle = talkingState.AddTransition(idleState);
            talkingToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsTalking");
            talkingToIdle.hasExitTime = false;
            talkingToIdle.duration = 0.25f;

            // ─── Talking → Talking1 (gesture switch) ───────────
            var talkingToT1 = talkingState.AddTransition(talking1State);
            talkingToT1.AddCondition(AnimatorConditionMode.Equals, 1, "GestureIndex");
            talkingToT1.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            talkingToT1.hasExitTime = false;
            talkingToT1.duration = 0.25f;

            // ─── Talking → Talking2 (gesture switch) ───────────
            var talkingToT2 = talkingState.AddTransition(talking2State);
            talkingToT2.AddCondition(AnimatorConditionMode.Equals, 2, "GestureIndex");
            talkingToT2.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            talkingToT2.hasExitTime = false;
            talkingToT2.duration = 0.25f;

            // ─── Talking1 → Talking (gesture switch back) ──────
            var t1ToTalking = talking1State.AddTransition(talkingState);
            t1ToTalking.AddCondition(AnimatorConditionMode.Equals, 0, "GestureIndex");
            t1ToTalking.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            t1ToTalking.hasExitTime = false;
            t1ToTalking.duration = 0.25f;

            // ─── Talking1 → Idle (stop talking) ────────────────
            var t1ToIdle = talking1State.AddTransition(idleState);
            t1ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsTalking");
            t1ToIdle.hasExitTime = false;
            t1ToIdle.duration = 0.25f;

            // ─── Talking2 → Talking (gesture switch back) ──────
            var t2ToTalking = talking2State.AddTransition(talkingState);
            t2ToTalking.AddCondition(AnimatorConditionMode.Equals, 0, "GestureIndex");
            t2ToTalking.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            t2ToTalking.hasExitTime = false;
            t2ToTalking.duration = 0.25f;

            // ─── Talking2 → Idle (stop talking) ────────────────
            var t2ToIdle = talking2State.AddTransition(idleState);
            t2ToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsTalking");
            t2ToIdle.hasExitTime = false;
            t2ToIdle.duration = 0.25f;

            // ─── Talking1 → Talking2 (direct gesture switch) ───
            var t1ToT2 = talking1State.AddTransition(talking2State);
            t1ToT2.AddCondition(AnimatorConditionMode.Equals, 2, "GestureIndex");
            t1ToT2.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            t1ToT2.hasExitTime = false;
            t1ToT2.duration = 0.25f;

            // ─── Talking2 → Talking1 (direct gesture switch) ───
            var t2ToT1 = talking2State.AddTransition(talking1State);
            t2ToT1.AddCondition(AnimatorConditionMode.Equals, 1, "GestureIndex");
            t2ToT1.AddCondition(AnimatorConditionMode.If, 0, "IsTalking");
            t2ToT1.hasExitTime = false;
            t2ToT1.duration = 0.25f;

            // ─── Save ──────────────────────────────────────────
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[NPCSetup] Animator Controller setup complete!");
            Debug.Log("[NPCSetup] States: Neutral Idle, Talking, Talking1, Talking2");
            Debug.Log("[NPCSetup] Transitions: All configured with no Exit Time");

            EditorUtility.DisplayDialog("Success",
                "NPC Animator setup complete!\n\n" +
                "Parameters added: State, IsTalking, GestureIndex, Nod\n\n" +
                "Transitions configured between all states.\n\n" +
                "Make sure your animation clips are assigned to each state.",
                "OK");
        }

        // ─── Helper Methods ────────────────────────────────────

        private static void AddParameterIfMissing(AnimatorController controller,
            string name, AnimatorControllerParameterType type)
        {
            bool exists = controller.parameters.Any(p => p.name == name);
            if (!exists)
            {
                controller.AddParameter(name, type);
            }
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine,
            string stateName)
        {
            // Look for existing state
            foreach (var child in stateMachine.states)
            {
                if (child.state.name == stateName)
                    return child.state;
            }

            // Create new state
            AnimatorState newState = stateMachine.AddState(stateName);
            Debug.Log($"[NPCSetup] Created state: {stateName}");
            return newState;
        }

        private static void ClearTransitions(AnimatorState state)
        {
            // Remove all existing transitions to avoid duplicates
            var transitions = state.transitions.ToArray();
            foreach (var t in transitions)
            {
                state.RemoveTransition(t);
            }
        }
    }
}
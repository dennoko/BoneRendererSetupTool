using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Hays.EditorTools
{
	/// <summary>
	/// Sets up a BoneRenderer component on the selected humanoid avatar root(s) using Mecanim's HumanBodyBones map.
	/// Menu: Tools/Animation Rigging/Setup Bone Renderer for Humanoid
	/// </summary>
	public static class BoneRendererSetupTool
	{
		private const string MenuPath = "Tools/Animation Rigging/Setup Bone Renderer for Humanoid";

		[MenuItem(MenuPath)]
		private static void SetupSelected()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
			{
				EditorUtility.DisplayDialog("Setup Bone Renderer", "No GameObject selected. Please select a GameObject with an Animator using a Humanoid Avatar.", "OK");
				return;
			}

			int processed = 0;
			foreach (var go in selected)
			{
				if (TrySetup(go, out string message))
				{
					processed++;
					Debug.Log($"[BoneRendererSetup] {go.name}: {message}");
				}
				else
				{
					Debug.LogWarning($"[BoneRendererSetup] {go.name}: {message}");
				}
			}

			if (processed > 0)
			{
				// Mark active scene dirty once after processing at least one object
				var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
				if (scene.IsValid())
				{
					EditorSceneManager.MarkSceneDirty(scene);
				}
			}
		}

		[MenuItem(MenuPath, true)]
		private static bool ValidateSetupSelected()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
				return false;

			// Enable only if at least one selected object has Animator with humanoid Avatar
			foreach (var go in selected)
			{
				if (TryGetHumanoidAnimator(go, out _))
					return true;
			}
			return false;
		}

		private static bool TrySetup(GameObject go, out string resultMessage)
		{
			if (!TryGetHumanoidAnimator(go, out var animator))
			{
				resultMessage = "Animator with Humanoid Avatar not found.";
				return false;
			}

			// Ensure BoneRenderer exists; use Undo for proper editor integration
			BoneRenderer boneRenderer = go.GetComponent<BoneRenderer>();
			if (boneRenderer == null)
			{
				boneRenderer = Undo.AddComponent<BoneRenderer>(go);
			}

			// Collect humanoid bone transforms via Mecanim mapping
			var transforms = CollectHumanoidTransforms(animator);
			if (transforms.Count == 0)
			{
				resultMessage = "No humanoid bones found to assign.";
				return false;
			}

			// Assign to BoneRenderer.m_Transforms via SerializedObject to respect serialization
			var so = new SerializedObject(boneRenderer);
			var transformsProp = so.FindProperty("m_Transforms");
			if (transformsProp == null)
			{
				// Fallback: try common alternative field names (package versions may vary)
				transformsProp = so.FindProperty("m_Bones");
			}

			if (transformsProp == null || !transformsProp.isArray)
			{
				resultMessage = "BoneRenderer does not expose a transforms array property (m_Transforms/m_Bones).";
				return false;
			}

			Undo.RecordObject(boneRenderer, "Setup Bone Renderer");

			so.Update();
			transformsProp.arraySize = transforms.Count;
			for (int i = 0; i < transforms.Count; i++)
			{
				transformsProp.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
			}
			so.ApplyModifiedProperties();

			EditorUtility.SetDirty(boneRenderer);

			resultMessage = $"Assigned {transforms.Count} humanoid bones.";
			return true;
		}

		private static bool TryGetHumanoidAnimator(GameObject go, out Animator animator)
		{
			animator = go != null ? go.GetComponent<Animator>() : null;
			if (animator == null)
				return false;

			var avatar = animator.avatar;
			if (avatar == null)
				return false;

			// Must be a valid Humanoid avatar
			return avatar.isValid && avatar.isHuman;
		}

		private static List<Transform> CollectHumanoidTransforms(Animator animator)
		{
			var list = new List<Transform>(64);

			// Iterate all defined HumanBodyBones except the sentinel LastBone
			for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
			{
				var bone = (HumanBodyBones)i;
				var t = animator.GetBoneTransform(bone);
				if (t != null)
				{
					list.Add(t);
				}
			}

			// Remove duplicates while preserving order (shouldn't usually occur, but it's safe)
			var unique = new List<Transform>(list.Count);
			var seen = new HashSet<Transform>();
			foreach (var t in list)
			{
				if (seen.Add(t))
					unique.Add(t);
			}

			return unique;
		}
	}
}


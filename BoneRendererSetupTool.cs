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
		private const string GoMenuAnimator = "GameObject/Animation Rigging/Setup Bone Renderer (Humanoid via Animator)";
		private const string GoMenuName = "GameObject/Animation Rigging/Setup Bone Renderer (Humanoid by Name)";

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

			if (!AssignToBoneRenderer(boneRenderer, transforms, out string assignErr))
			{
				resultMessage = assignErr;
				return false;
			}

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

		private static bool AssignToBoneRenderer(BoneRenderer boneRenderer, List<Transform> transforms, out string error)
		{
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
				error = "BoneRenderer does not expose a transforms array property (m_Transforms/m_Bones).";
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
			error = null;
			return true;
		}

		// ---------------- GameObject (Hierarchy) context menus ----------------

		[MenuItem(GoMenuAnimator, false, 0)]
		private static void SetupFromAnimator_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
			{
				EditorUtility.DisplayDialog("Setup Bone Renderer", "Select one or more GameObjects with an Animator using a Humanoid Avatar.", "OK");
				return;
			}

			int processed = 0;
			foreach (var go in selected)
			{
				if (TrySetup(go, out var msg))
				{
					processed++;
					Debug.Log($"[BoneRendererSetup] {go.name}: {msg}");
				}
				else
				{
					Debug.LogWarning($"[BoneRendererSetup] {go.name}: {msg}");
				}
			}

			if (processed > 0)
			{
				var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
				if (scene.IsValid())
					EditorSceneManager.MarkSceneDirty(scene);
			}
		}

		[MenuItem(GoMenuAnimator, true)]
		private static bool Validate_SetupFromAnimator_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
				return false;
			foreach (var go in selected)
			{
				if (TryGetHumanoidAnimator(go, out _))
					return true;
			}
			return false;
		}

		[MenuItem(GoMenuName, false, 1)]
		private static void SetupByName_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
			{
				EditorUtility.DisplayDialog("Setup Bone Renderer (By Name)", "Select one or more armature roots in the Hierarchy.", "OK");
				return;
			}

			int processed = 0;
			foreach (var go in selected)
			{
				BoneRenderer boneRenderer = go.GetComponent<BoneRenderer>();
				if (boneRenderer == null)
				{
					boneRenderer = Undo.AddComponent<BoneRenderer>(go);
				}

				var transforms = CollectHumanoidTransformsByName(go);
				if (transforms.Count == 0)
				{
					Debug.LogWarning($"[BoneRendererSetup] {go.name}: No humanoid-named bones found under selection.");
					continue;
				}

				if (AssignToBoneRenderer(boneRenderer, transforms, out var err))
				{
					processed++;
					Debug.Log($"[BoneRendererSetup] {go.name}: Assigned {transforms.Count} humanoid-named bones.");
				}
				else
				{
					Debug.LogWarning($"[BoneRendererSetup] {go.name}: {err}");
				}
			}

			if (processed > 0)
			{
				var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
				if (scene.IsValid())
					EditorSceneManager.MarkSceneDirty(scene);
			}
		}

		[MenuItem(GoMenuName, true)]
		private static bool Validate_SetupByName_Context()
		{
			var selected = Selection.gameObjects;
			return selected != null && selected.Length > 0;
		}

		private static List<Transform> CollectHumanoidTransformsByName(GameObject root)
		{
			// Build a fast lookup of all child transforms (include inactive)
			var all = root.GetComponentsInChildren<Transform>(true);
			// Preserving first match order by HumanBodyBones enum iteration
			var result = new List<Transform>(64);
			var seen = new HashSet<Transform>();

			for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
			{
				var bone = (HumanBodyBones)i;
				var canonical = bone.ToString(); // e.g., Hips, LeftUpperArm
				Transform found = null;

				// 1) Exact (case-insensitive) match first
				for (int t = 0; t < all.Length; t++)
				{
					if (string.Equals(all[t].name, canonical, System.StringComparison.OrdinalIgnoreCase))
					{
						found = all[t];
						break;
					}
				}

				// 2) Fallbacks for L/R suffix patterns (Blender-style .L/_L/L)
				if (found == null && (canonical.Contains("Left") || canonical.Contains("Right")))
				{
					bool isLeft = canonical.Contains("Left");
					string baseName = canonical.Replace("Left", string.Empty).Replace("Right", string.Empty);
					var candidates = new[]
					{
						baseName + (isLeft ? "L" : "R"),
						baseName + (isLeft ? "_L" : "_R"),
						baseName + (isLeft ? ".L" : ".R"),
						(isLeft ? "L_" : "R_") + baseName,
						(isLeft ? "Left_" : "Right_") + baseName,
						baseName + (isLeft ? "-L" : "-R")
					};

					foreach (var cand in candidates)
					{
						for (int t = 0; t < all.Length; t++)
						{
							if (string.Equals(all[t].name, cand, System.StringComparison.OrdinalIgnoreCase))
							{
								found = all[t];
								break;
							}
						}
						if (found != null) break;
					}
				}

				if (found != null && seen.Add(found))
				{
					result.Add(found);
				}
			}

			return result;
		}
	}
}


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
	private const string GoMenuFromSMR = "GameObject/Animation Rigging/Setup Bone Renderer (From Skinned Meshes)";
	private const string GoMenuRemove = "GameObject/Animation Rigging/Remove Bone Renderer(s)";

	private const string HueEditorPrefsKey = "Hays.BoneRendererSetupTool.HueBase";
	private const float HueStep = 0.13f; // about 47 degrees per step for good separation

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

			// Auto color: assign and advance hue
			var color = ColorFromNextHue();
			TrySetBoneRendererColor(boneRenderer, color);

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

		private static List<Transform> CollectBonesFromSkinnedMeshes(GameObject root, bool onlyHumanoidRelated, out int smrCount)
		{
			var result = new List<Transform>(128);
			smrCount = 0;

			if (root == null)
				return result;

			var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			smrCount = smrs?.Length ?? 0;

			// Prepare humanoid set if filtering enabled
			HashSet<Transform> humanoidSet = null;
			if (onlyHumanoidRelated)
			{
				if (TryGetHumanoidAnimator(root, out var animator))
				{
					humanoidSet = new HashSet<Transform>(CollectHumanoidTransforms(animator));
				}
				else
				{
					// Try to find in parents
					var a = root.GetComponentInParent<Animator>();
					if (a != null && a.avatar != null && a.avatar.isValid && a.avatar.isHuman)
						humanoidSet = new HashSet<Transform>(CollectHumanoidTransforms(a));
				}
			}

			var seen = new HashSet<Transform>();
			if (smrs != null)
			{
				foreach (var smr in smrs)
				{
					if (smr == null || smr.bones == null) continue;
					foreach (var b in smr.bones)
					{
						if (b == null) continue;
						if (humanoidSet != null && !IsRelatedToHumanoid(b, humanoidSet))
							continue;
						if (seen.Add(b))
							result.Add(b);
					}
				}
			}

			return result;
		}

		private static bool IsRelatedToHumanoid(Transform t, HashSet<Transform> humanoidSet)
		{
			if (t == null || humanoidSet == null) return false;
			var cur = t;
			// walk up until root
			while (cur != null)
			{
				if (humanoidSet.Contains(cur))
					return true;
				cur = cur.parent;
			}
			return false;
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

		private static bool TrySetBoneRendererColor(BoneRenderer boneRenderer, Color color)
		{
			if (boneRenderer == null) return false;
			var so = new SerializedObject(boneRenderer);
			// Try common serialized color property names
			var candidates = new[] { "m_Color", "m_BoneColor", "color", "boneColor" };
			SerializedProperty colorProp = null;
			foreach (var name in candidates)
			{
				colorProp = so.FindProperty(name);
				if (colorProp != null && colorProp.propertyType == SerializedPropertyType.Color)
					break;
				colorProp = null;
			}

			if (colorProp == null)
			{
				// Unable to set color for this BoneRenderer version
				return false;
			}

			Undo.RecordObject(boneRenderer, "Set Bone Renderer Color");
			so.Update();
			colorProp.colorValue = color;
			so.ApplyModifiedProperties();
			EditorUtility.SetDirty(boneRenderer);
			return true;
		}

		private static Color ColorFromNextHue()
		{
			float h = EditorPrefs.GetFloat(HueEditorPrefsKey, 0f);
			// Convert HSV to RGB
			var c = Color.HSVToRGB(NormalizeHue01(h), 0.85f, 0.95f);
			// advance and persist
			h = NormalizeHue01(h + HueStep);
			EditorPrefs.SetFloat(HueEditorPrefsKey, h);
			return c;
		}

		private static float NormalizeHue01(float h)
		{
			if (h >= 0f && h < 1f) return h;
			h %= 1f;
			if (h < 0f) h += 1f;
			return h;
		}

		[MenuItem("Tools/Animation Rigging/Reset Bone Renderer Color Cycle")] 
		private static void ResetColorCycle()
		{
			EditorPrefs.DeleteKey(HueEditorPrefsKey);
			Debug.Log("[BoneRendererSetup] Color cycle reset.");
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
					// Auto color
					TrySetBoneRendererColor(boneRenderer, ColorFromNextHue());
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

		[MenuItem(GoMenuFromSMR, false, 2)]
		private static void SetupFromSkinnedMeshes_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
			{
				EditorUtility.DisplayDialog("Setup Bone Renderer (From Skinned Meshes)", "Select one or more GameObjects containing SkinnedMeshRenderers.", "OK");
				return;
			}

			int processed = 0;
			foreach (var go in selected)
			{
				// Add or get BoneRenderer on the same root
				BoneRenderer boneRenderer = go.GetComponent<BoneRenderer>();
				if (boneRenderer == null)
				{
					boneRenderer = Undo.AddComponent<BoneRenderer>(go);
				}

				// Prefer filtering to humanoid-related if possible
				var transforms = CollectBonesFromSkinnedMeshes(go, onlyHumanoidRelated: true, out int smrCount);

				// If filtering yields nothing but SMRs exist, fallback to unfiltered (pure SMR bones)
				if (transforms.Count == 0 && smrCount > 0)
				{
					transforms = CollectBonesFromSkinnedMeshes(go, onlyHumanoidRelated: false, out _);
				}

				if (transforms.Count == 0)
				{
					Debug.LogWarning($"[BoneRendererSetup] {go.name}: No bones found from SkinnedMeshRenderers under selection.");
					continue;
				}

				if (AssignToBoneRenderer(boneRenderer, transforms, out var err))
				{
					processed++;
					// Auto color
					TrySetBoneRendererColor(boneRenderer, ColorFromNextHue());
					Debug.Log($"[BoneRendererSetup] {go.name}: Assigned {transforms.Count} bones from SkinnedMeshRenderers.");
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

		[MenuItem(GoMenuFromSMR, true)]
		private static bool Validate_SetupFromSkinnedMeshes_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
				return false;
			// Enable if any selection has SMR in children
			foreach (var go in selected)
			{
				if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
					return true;
			}
			return false;
		}

		[MenuItem(GoMenuRemove, false, 49)]
		private static void RemoveBoneRenderers_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0)
			{
				EditorUtility.DisplayDialog("Remove Bone Renderer", "Select one or more GameObjects with BoneRenderer components.", "OK");
				return;
			}

			int removed = 0;
			foreach (var go in selected)
			{
				var comps = go.GetComponents<BoneRenderer>();
				foreach (var c in comps)
				{
					Undo.DestroyObjectImmediate(c);
					removed++;
				}
			}

			if (removed > 0)
			{
				var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
				if (scene.IsValid())
					EditorSceneManager.MarkSceneDirty(scene);
				Debug.Log($"[BoneRendererSetup] Removed {removed} BoneRenderer component(s).");
			}
			else
			{
				Debug.Log("[BoneRendererSetup] No BoneRenderer components found to remove.");
			}
		}

		[MenuItem(GoMenuRemove, true)]
		private static bool Validate_RemoveBoneRenderers_Context()
		{
			var selected = Selection.gameObjects;
			if (selected == null || selected.Length == 0) return false;
			foreach (var go in selected)
			{
				if (go.GetComponent<BoneRenderer>() != null)
					return true;
			}
			return false;
		}
	}
}


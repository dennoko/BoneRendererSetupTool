# Bone Renderer Setup Tool

Unity Editor tool that automatically configures the Animation Rigging `BoneRenderer` on selected Humanoid avatars using the Mecanim bone map. It also supports outfit bones via SkinnedMeshRenderers and offers hierarchy right‑click shortcuts.

- Extracts only Humanoid bones via `Animator.GetBoneTransform(HumanBodyBones)`
- Adds `BoneRenderer` if missing and assigns the collected transforms
- Works from menu items:
	- `Tools/Animation Rigging/Setup Bone Renderer for Humanoid` (Animator/Mecanim based)
	- Hierarchy right‑click: `GameObject/Animation Rigging/Setup Bone Renderer (Humanoid via Animator)`
	- Hierarchy right‑click: `GameObject/Animation Rigging/Setup Bone Renderer (Humanoid by Name)`
	- Hierarchy right‑click: `GameObject/Animation Rigging/Setup Bone Renderer (From Skinned Meshes)`
- Auto color: each setup assigns a distinct color (cycled hue). Reset via `Tools/Animation Rigging/Reset Bone Renderer Color Cycle`.
- Quick cleanup: `GameObject/Animation Rigging/Remove Bone Renderer(s)`
- Supports multi-selection, Undo/Redo, and marks the scene dirty

## Requirements
- Unity 2019.4+ (tested on modern LTS)
- Animation Rigging package installed (provides `BoneRenderer`)
- For Animator-based setup: target objects must have an `Animator` with a Humanoid `Avatar`.
- For SMR-based setup: selection should contain `SkinnedMeshRenderer` components as children.

## Installation
- Place `BoneRendererSetupTool.cs` in an `Editor` folder of your project.
- Ensure the Animation Rigging package is installed via Package Manager.

## Usage
1. In the Hierarchy, select one or more target objects.
2. Choose one of the following:
	 - Animator/Mecanim: `Tools > Animation Rigging > Setup Bone Renderer for Humanoid` or right‑click > `Setup Bone Renderer (Humanoid via Animator)`
	 - Name-based: right‑click > `Setup Bone Renderer (Humanoid by Name)` (fallback for armatures without Animator)
	 - From Skinned Meshes: right‑click > `Setup Bone Renderer (From Skinned Meshes)` to gather actual bones referenced by SMRs (optionally filtered to humanoid‑related)
3. Inspect the `BoneRenderer` on the selected objects; its transforms array will list assigned bones. Colors will differ per setup for clarity.

## Notes
- The script writes to the serialized array named `m_Transforms` (or `m_Bones` as a fallback, depending on package version).
- If you wish to filter bone types (e.g., exclude fingers), you can extend the collection step to skip specific `HumanBodyBones` values.
- SMR-based mode does not expose humanoid names; humanoid relation is inferred by ancestor checks against the Animator’s humanoid set.
- Name-based mode depends on transform naming; prefer Animator/SMR modes for reliability.
- To reset the auto-color sequence, use `Tools > Animation Rigging > Reset Bone Renderer Color Cycle`.

## License
MIT. See `LICENCE`.

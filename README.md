# Bone Renderer Setup Tool

Unity Editor tool that automatically configures the Animation Rigging `BoneRenderer` on selected Humanoid avatars using the Mecanim bone map.

- Extracts only Humanoid bones via `Animator.GetBoneTransform(HumanBodyBones)`
- Adds `BoneRenderer` if missing and assigns the collected transforms
- Works from a simple menu item: `Tools/Animation Rigging/Setup Bone Renderer for Humanoid`
- Supports multi-selection, Undo/Redo, and marks the scene dirty

## Requirements
- Unity 2019.4+ (tested on modern LTS)
- Animation Rigging package installed (provides `BoneRenderer`)
- Target objects must have an `Animator` with a Humanoid `Avatar`

## Installation
- Place `BoneRendererSetupTool.cs` in an `Editor` folder of your project.
- Ensure the Animation Rigging package is installed via Package Manager.

## Usage
1. In the Hierarchy, select one or more avatar root objects that have an `Animator` with a valid Humanoid avatar.
2. Run the menu: `Tools > Animation Rigging > Setup Bone Renderer for Humanoid`.
3. Inspect the `BoneRenderer` component on the selected objects; its transforms array will list the humanoid bones.

## Notes
- The script writes to the serialized array named `m_Transforms` (or `m_Bones` as a fallback, depending on package version).
- If you wish to filter bone types (e.g., exclude fingers), you can extend the collection step to skip specific `HumanBodyBones` values.

## License
MIT. See `LICENCE`.

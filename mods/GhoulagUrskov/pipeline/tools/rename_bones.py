"""
Renames Mixamo bones → Ironhorn DEF- bones on urskov.fbx
Writes urskov_def.fbx, ready for Unity AssetBundle packaging.

Run: blender --background --python rename_bones.py
"""

import bpy

INPUT  = r"F:\projects\blackveil\raiders-of-blackveil-mods\mods\GhoulagUrskov\pipeline\source\urskov.fbx"
OUTPUT = r"F:\projects\blackveil\raiders-of-blackveil-mods\mods\GhoulagUrskov\pipeline\source\urskov_def.fbx"

# Mixamo → Ironhorn DEF- mapping.
# Finger bones that have no DEF- equivalent are prefixed UNUSED- so they
# don't accidentally bind to an Ironhorn bone.
BONE_MAP = {
    "mixamorig:Hips":             "DEF-HIPS",
    "mixamorig:Spine":            "DEF-SPINE",
    "mixamorig:Spine1":           "DEF-SPINE-01",
    "mixamorig:Spine2":           "DEF-CHEST",
    "mixamorig:Neck":             "DEF-NECK",
    "mixamorig:Head":             "DEF-HEAD",
    # Left arm
    "mixamorig:LeftShoulder":     "DEF-SHOULDER.L",
    "mixamorig:LeftArm":          "DEF-UPPERARM01.L",
    "mixamorig:LeftForeArm":      "DEF-LOWERARM01.L",
    "mixamorig:LeftHand":         "DEF-HAND.L",
    # Left fingers — only thumb and middle have DEF- equivalents
    "mixamorig:LeftHandThumb1":   "DEF-F_THUMB.01.L",
    "mixamorig:LeftHandThumb2":   "DEF-F_THUMB.02.L",
    "mixamorig:LeftHandThumb3":   "DEF-F_THUMB.03.L",
    "mixamorig:LeftHandMiddle1":  "DEF-F_MIDDLE.01.L",
    "mixamorig:LeftHandMiddle2":  "DEF-F_MIDDLE.02.L",
    "mixamorig:LeftHandMiddle3":  "DEF-F_MIDDLE.03.L",
    "mixamorig:LeftHandIndex1":   "DEF-PALM.02.L",
    "mixamorig:LeftHandIndex2":   "UNUSED-L_IDX2",
    "mixamorig:LeftHandIndex3":   "UNUSED-L_IDX3",
    "mixamorig:LeftHandRing1":    "UNUSED-L_RNG1",
    "mixamorig:LeftHandRing2":    "UNUSED-L_RNG2",
    "mixamorig:LeftHandRing3":    "UNUSED-L_RNG3",
    "mixamorig:LeftHandPinky1":   "UNUSED-L_PNK1",
    "mixamorig:LeftHandPinky2":   "UNUSED-L_PNK2",
    "mixamorig:LeftHandPinky3":   "UNUSED-L_PNK3",
    # Left leg
    "mixamorig:LeftUpLeg":        "DEF-THIGH01.L",
    "mixamorig:LeftLeg":          "DEF-CALF01.L",
    "mixamorig:LeftFoot":         "DEF-FOOT.L",
    "mixamorig:LeftToeBase":      "DEF-TOE.L",
    # Right arm
    "mixamorig:RightShoulder":    "DEF-SHOULDER.R",
    "mixamorig:RightArm":         "DEF-UPPERARM01.R",
    "mixamorig:RightForeArm":     "DEF-LOWERARM01.R",
    "mixamorig:RightHand":        "DEF-HAND.R",
    # Right fingers
    "mixamorig:RightHandThumb1":  "DEF-F_THUMB.01.R",
    "mixamorig:RightHandThumb2":  "DEF-F_THUMB.02.R",
    "mixamorig:RightHandThumb3":  "DEF-F_THUMB.03.R",
    "mixamorig:RightHandMiddle1": "DEF-F_MIDDLE.01.R",
    "mixamorig:RightHandMiddle2": "DEF-F_MIDDLE.02.R",
    "mixamorig:RightHandMiddle3": "DEF-F_MIDDLE.03.R",
    "mixamorig:RightHandIndex1":  "DEF-PALM.02.R",
    "mixamorig:RightHandIndex2":  "UNUSED-R_IDX2",
    "mixamorig:RightHandIndex3":  "UNUSED-R_IDX3",
    "mixamorig:RightHandRing1":   "UNUSED-R_RNG1",
    "mixamorig:RightHandRing2":   "UNUSED-R_RNG2",
    "mixamorig:RightHandRing3":   "UNUSED-R_RNG3",
    "mixamorig:RightHandPinky1":  "UNUSED-R_PNK1",
    "mixamorig:RightHandPinky2":  "UNUSED-R_PNK2",
    "mixamorig:RightHandPinky3":  "UNUSED-R_PNK3",
    # Right leg
    "mixamorig:RightUpLeg":       "DEF-THIGH01.R",
    "mixamorig:RightLeg":         "DEF-CALF01.R",
    "mixamorig:RightFoot":        "DEF-FOOT.R",
    "mixamorig:RightToeBase":     "DEF-TOE.R",
}

# ── import ────────────────────────────────────────────────────────────────────
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=INPUT, automatic_bone_orientation=False)
print(f"Imported: {INPUT}")

renamed_bones = 0
renamed_vgroups = 0

for obj in bpy.data.objects:
    if obj.type == 'ARMATURE':
        # Rename in edit mode to avoid Blender auto-uniquifying names mid-loop.
        # We rename via data.bones (pose/object mode is fine for this).
        for bone in obj.data.bones:
            new_name = BONE_MAP.get(bone.name)
            if new_name:
                print(f"  bone: {bone.name!r} → {new_name!r}")
                bone.name = new_name
                renamed_bones += 1

    if obj.type == 'MESH':
        for vg in obj.vertex_groups:
            new_name = BONE_MAP.get(vg.name)
            if new_name:
                print(f"  vgroup: {vg.name!r} → {new_name!r}")
                vg.name = new_name
                renamed_vgroups += 1

print(f"\nRenamed {renamed_bones} bones, {renamed_vgroups} vertex groups.")

# ── orient to match Ironhorn's rest pose and align origin ─────────────────────
# Ironhorn lies face-down (horizontal). Urskov is standing upright in T-pose.
# Rotating +90° around X tips the bear forward so it lies face-down, matching
# Ironhorn's orientation. We do this AFTER feet alignment so the pivot stays
# at the model's natural foot position before it falls forward.
import math
import bmesh as _bmesh
import mathutils

# 1. Feet to Z=0 first (while still upright).
min_z = float('inf')
max_z = float('-inf')
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    bm = _bmesh.new()
    bm.from_mesh(obj.data)
    for v in bm.verts:
        world_z = (obj.matrix_world @ v.co).z
        min_z = min(min_z, world_z)
        max_z = max(max_z, world_z)
    bm.free()

mesh_height = max_z - min_z
print(f"Mesh Z range: {min_z:.4f} → {max_z:.4f}  (height={mesh_height:.4f})")

delta_z = -min_z
if abs(delta_z) > 1e-6:
    for obj in bpy.data.objects:
        obj.location.z += delta_z
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    print(f"Shifted all objects by Z={delta_z:.4f} to place feet at ground.")

# 2. Rotate +90° around X: tips the character forward (head goes toward -Y),
#    matching Ironhorn's face-down horizontal rest pose.
rot = mathutils.Matrix.Rotation(math.radians(90), 4, 'X')
for obj in bpy.data.objects:
    obj.matrix_world = rot @ obj.matrix_world
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
print("Rotated +90° around X (face-down, matching Ironhorn rest pose).")

# global_scale folds cm→m conversion (0.01) and measured Unity scale (2.5 * 0.44 = 1.1).
# Keeping obj.scale untouched avoids compounding errors with armature/mesh parent-child.
global_scale = 0.025
print(f"Export global_scale={global_scale:.4f} (body length ~{mesh_height * global_scale:.3f} Unity units)")

# ── export texture separately ─────────────────────────────────────────────────
import os
tex_dir = os.path.join(os.path.dirname(OUTPUT), "textures")
os.makedirs(tex_dir, exist_ok=True)
for img in bpy.data.images:
    if img.type == 'IMAGE' and img.has_data:
        tex_path = os.path.join(tex_dir, img.name if img.name.endswith(".png") else img.name + ".png")
        img.file_format = 'PNG'
        img.save(filepath=tex_path)
        print(f"  texture: {img.name!r} → {tex_path!r}")

# ── export FBX (no embedded textures — embedding corrupts AssetBundles) ───────
bpy.ops.export_scene.fbx(
    filepath=OUTPUT,
    use_selection=False,
    global_scale=global_scale,
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=False,
    object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    primary_bone_axis='Y',
    secondary_bone_axis='X',
    path_mode='STRIP',
    embed_textures=False,
)
print(f"Exported: {OUTPUT}")

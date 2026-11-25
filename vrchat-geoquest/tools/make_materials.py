"""Write the map and panorama materials with their textures already assigned.

  Materials/Map.mat        GeoQuest/Map shader: NCMap + CountyIds + CountySelection
  Materials/Panorama.mat   GeoQuest/Panorama shader: LobbyPanorama

The references use the same path-derived GUIDs as make_unitypackage.py, so the
materials work the moment Unity imports the folder; the builder doesn't have to
make them.

  python make_materials.py
"""
import os
from make_unitypackage import guid, PROJECT

R = "Assets/GeoQuest"


def tex(path):
    return f"{{fileID: 2800000, guid: {guid(R + '/' + path)}, type: 3}}"


def material(name, shader, texs, colors):
    t = "".join(f"    - {k}:\n        m_Texture: {tex(v) if v else '{fileID: 0}'}\n"
                f"        m_Scale: {{x: 1, y: 1}}\n        m_Offset: {{x: 0, y: 0}}\n" for k, v in texs)
    c = "".join(f"    - {k}: {{r: {r}, g: {g}, b: {b}, a: {a}}}\n" for k, (r, g, b, a) in colors)
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {guid(R + '/Shaders/' + shader)}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
{t}    m_Ints: []
    m_Floats:
    - _Yaw: 0
    m_Colors:
{c}  m_BuildTextureStacks: []
"""


def main():
    out = os.path.join(PROJECT, R, "Materials")
    os.makedirs(out, exist_ok=True)
    mats = {
        "Map": material("Map", "GeoQuestMap.shader",
                        [("_MainTex", "Textures/NCMap.png"), ("_IdTex", "Textures/CountyIds.png"),
                         ("_SelTex", "Textures/CountySelection.png")],
                        [("_OffColor", (0.62, 0.64, 0.68, 1)), ("_Color", (1, 1, 1, 1))]),
        "Panorama": material("Panorama", "GeoQuestPanorama.shader",
                             [("_MainTex", "Textures/LobbyPanorama.png")],
                             [("_Tint", (1, 1, 1, 1)), ("_Color", (1, 1, 1, 1))]),
    }
    for name, text in mats.items():
        with open(os.path.join(out, name + ".mat"), "w", newline="\n") as f:
            f.write(text)
    print("wrote", ", ".join(m + ".mat" for m in mats))


if __name__ == "__main__":
    main()

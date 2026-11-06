using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// A grabbable pin. Drop it on the big floor map and it becomes your guess
/// (same as pointing at the table map). Position is shared with VRCObjectSync.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GeoFloorPin : UdonSharpBehaviour
{
    public GeoGameManager game;
    public Transform floorMap;          // unscaled root: local x = east, local z = north, centred on the map
    public float width = 6f, height = 3f;

    public override void OnDrop()
    {
        if (game == null || game.map == null || floorMap == null) return;
        Vector3 p = floorMap.InverseTransformPoint(transform.position);
        float u = p.x / width + 0.5f, v = p.z / height + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return;
        MapBoard m = game.map;
        float lon = m.minLon + u * (m.maxLon - m.minLon);
        float lat = m.minLat + v * (m.maxLat - m.minLat);
        m.PlacePinAt(lat, lon);
    }
}

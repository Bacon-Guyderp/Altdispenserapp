#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the voice lines (the sub-folders of Audio; the sound effects and music at the top level keep
/// Unity's defaults, so jump scares are never late): loaded in the background when first needed rather than all at
/// start-up, and kept compressed in memory. The compression quality is untouched, so nothing sounds any different.
/// </summary>
public class FirAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/FirHorror/Audio/")) return;
        if (assetPath.Substring("Assets/FirHorror/Audio/".Length).IndexOf('/') < 0) return;   // top-level effects and music
        var imp = (AudioImporter)assetImporter;
        imp.loadInBackground = true;
        var s = imp.defaultSampleSettings;
        s.loadType = AudioClipLoadType.CompressedInMemory;
        s.preloadAudioData = false;
        imp.defaultSampleSettings = s;
    }
}
#endif

// Assets/Editor/WineSimSaveTools.cs
#if UNITY_EDITOR
using UnityEditor;
public static class WineSimSaveTools
{
    [MenuItem("Tools/WineSim/Clear Save")]
    public static void ClearSave()
    {
        SaveLoadManager.Delete();
        UnityEngine.Debug.Log("WineSim: Save cleared.");
    }

    [MenuItem("Tools/WineSim/Toggle Reset On Play")]
    public static void ToggleResetOnPlay()
    {
        var holder = UnityEngine.Object.FindFirstObjectByType<GameConfigHolder>();
        if (!holder) { UnityEngine.Debug.LogWarning("No GameConfigHolder in scene."); return; }
        holder.Config.resetOnPlay = !holder.Config.resetOnPlay;
        EditorUtility.SetDirty(holder.Config);
        UnityEngine.Debug.Log("ResetOnPlay: " + holder.Config.resetOnPlay);
    }
}
#endif
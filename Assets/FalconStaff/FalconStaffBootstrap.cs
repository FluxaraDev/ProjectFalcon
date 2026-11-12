using UnityEngine;

// Spawns the Falcon Staff system at runtime when the game scene loads.
// No Unity Editor scene editing is needed: the staff object, its network
// listener, the soundboard and the hand menu are all code-created, which
// keeps the setup identical on every build and immune to scene re-imports.
public static class FalconStaffBootstrap
{
    private static bool spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (spawned) return;
        spawned = true;

        var go = new GameObject("FalconStaff");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<FalconStaffNet>();
        go.AddComponent<FalconSoundboard>();
        go.AddComponent<FalconStaffHandMenu>();
        go.AddComponent<FalconCoordCube>();
        // Game-wide pieces that ride the same bootstrap: the void guard
        // protects every player, and the board-text pass applies the
        // Falcon code of conduct to the scene's static boards.
        go.AddComponent<FalconVoidGuard>();
        go.AddComponent<FalconBoardTexts>();
        go.AddComponent<FalconRave>();
    }
}

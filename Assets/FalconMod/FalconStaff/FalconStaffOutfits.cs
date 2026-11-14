using System.Collections;
using System.Collections.Generic;
using GorillaNetworking;
using Photon.Pun;
using UnityEngine;

// Outfit guns. These run ON THE TARGET's own client (the net layer targets
// the event at them). That is what makes unowned cosmetics work session-only:
// items are applied into the rig's try-on set, the same mechanism the
// fitting room uses to preview cosmetics you do not own. Nothing is granted
// and nothing is saved to PlayerPrefs (applyToPlayerPrefs stays false), and
// the wear state syncs to the lobby through the rig's normal stream.
// Leaving the room ends it. Types below are nested in CosmeticsController
// in this source tree, and CosmeticItem is a struct.
public static class FalconStaffOutfits
{
    public static void RandomOutfit()
    {
        VRRig rig = LocalRig();
        CosmeticsController cc = CosmeticsController.instance;
        if (rig == null || cc == null) return;

        rig.tryOnSet = new CosmeticsController.CosmeticSet();
        var all = new List<CosmeticsController.CosmeticItem>(cc.allCosmeticsDict.Values);
        // Apply a shuffled pass of everything: each item lands in its own
        // slot through the controller, so every slot ends with a random
        // item and no slot math is needed here.
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
        }
        foreach (var item in all)
        {
            if (item.isNullItem) continue;
            cc.ApplyCosmeticItemToSet(rig.tryOnSet, item, Random.value < 0.5f, false);
        }
        rig.SetCosmeticsActive();
        SetColor(rig, Random.value, Random.value, Random.value);
    }

    public static IEnumerator AllCosmetics()
    {
        VRRig rig = LocalRig();
        CosmeticsController cc = CosmeticsController.instance;
        if (rig == null || cc == null) yield break;

        var savedTryOn = new CosmeticsController.CosmeticSet();
        savedTryOn.CopyItems(rig.tryOnSet);
        rig.tryOnSet = new CosmeticsController.CosmeticSet();
        foreach (var item in cc.allCosmeticsDict.Values)
        {
            if (item.isNullItem) continue;
            cc.ApplyCosmeticItemToSet(rig.tryOnSet, item, false, false);
            cc.ApplyCosmeticItemToSet(rig.tryOnSet, item, true, false);
        }
        rig.SetCosmeticsActive();
        SetColor(rig, 1f, 1f, 1f); // color code 9 9 9
        yield return new WaitForSeconds(20f);
        rig.tryOnSet = savedTryOn;
        rig.SetCosmeticsActive();
    }

    private static VRRig LocalRig()
    {
        if (GorillaParent.instance == null) return null;
        VRRig rig;
        return GorillaParent.instance.vrrigDict.TryGetValue(PhotonNetwork.LocalPlayer, out rig)
            ? rig : null;
    }

    private static void SetColor(VRRig rig, float r, float g, float b)
    {
        // Same path the game uses for the local player's color (line 374 in
        // VRRig.cs). Color-code values 0-9 map to 0..1 by /9, so the code
        // 9 9 9 is (1, 1, 1).
        bool leftHanded = GorillaComputer.instance != null && GorillaComputer.instance.leftHanded;
        rig.InitializeNoobMaterialLocal(r, g, b, leftHanded);
    }
}

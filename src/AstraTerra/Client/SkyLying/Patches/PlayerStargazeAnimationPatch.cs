using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace AstraTerra.Client.SkyLying.Patches;

[HarmonyPatch]
public static class PlayerStargazeAnimationPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
        => typeof(EntityPlayer).GetMethod(
                nameof(EntityPlayer.OnTesselation),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(Shape).MakeByRefType(), typeof(string)],
                modifiers: null)
            ?? throw new MissingMethodException(typeof(EntityPlayer).FullName, nameof(EntityPlayer.OnTesselation));

    [HarmonyPrefix]
    public static void Prefix(ref Shape entityShape)
        => SkyLyingAnimation.EnsureSupineClip(entityShape);
}

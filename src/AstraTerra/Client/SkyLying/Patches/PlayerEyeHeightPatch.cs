using System.Reflection;
using AstraTerra.Observation;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Client.SkyLying.Patches;

[HarmonyPatch]
public static class PlayerEyeHeightPatch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
        => typeof(EntityPlayer).GetMethod(
                "updateEyeHeight",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(float)],
                modifiers: null)
            ?? throw new MissingMethodException(typeof(EntityPlayer).FullName, "updateEyeHeight");

    [HarmonyPostfix]
    public static void Postfix(EntityPlayer __instance, float dt)
    {
        if (!SkyLyingState.IsLying)
        {
            return;
        }

        var localPlayerUid = (__instance.Api as ICoreClientAPI)?.World?.Player?.PlayerUID;
        if (!SkyLyingPolicy.ShouldLowerEyes(true, __instance.PlayerUID, localPlayerUid))
        {
            return;
        }

        var standingEyeHeight = __instance.Properties.EyeHeight;
        var standingCollisionHeight = __instance.Properties.CollisionBoxSize.Y;
        var eyeTarget = SkyLyingPolicy.EyeHeight(standingEyeHeight);
        var collisionTarget = SkyLyingPolicy.CollisionHeight(standingCollisionHeight);

        __instance.LocalEyePos.Y = SkyLyingPolicy.StepToward(__instance.LocalEyePos.Y, eyeTarget, dt);
        __instance.LocalEyePos.X = 0.0;
        __instance.LocalEyePos.Z = 0.0;

        var nextCollision = (float)SkyLyingPolicy.StepToward(__instance.CollisionBox.Y2, collisionTarget, dt);
        __instance.OriginSelectionBox.Y2 = __instance.SelectionBox.Y2 = nextCollision;
        __instance.OriginCollisionBox.Y2 = __instance.CollisionBox.Y2 = nextCollision;
    }
}

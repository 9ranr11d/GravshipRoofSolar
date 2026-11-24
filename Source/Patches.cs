using HarmonyLib;
using RimWorld;
using Verse;

namespace GravshipRoofSolar
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            new Harmony("9ranr11d.GravshipRoofSolar").PatchAll();
            // 이 패널 전용 프레임 def(건물마다 따로 생성됨)만 수정: 프레임 동안에도 엄폐·통행비용·식물 방해가 없게
            ThingDef frame = PanelUtil.PanelDef?.frameDef;
            if (frame != null)
            {
                frame.fillPercent = 0f;
                frame.pathCost = 0;
            }
        }
    }

    public static class PanelUtil
    {
        private static ThingDef panelDef;
        public static ThingDef PanelDef => panelDef ?? (panelDef = DefDatabase<ThingDef>.GetNamedSilentFail("GravshipRoofSolarPanel"));

        // 완성품·청사진·프레임 모두 true
        public static bool IsPanel(BuildableDef d)
        {
            ThingDef p = PanelDef;
            if (p == null || !(d is ThingDef t)) return false;
            return t == p || t.entityDefToBuild == p;
        }
    }

    // 수경 재배기 위 지붕에 깔 수 있도록, 아래 식물이 건설 방해물이 되지 않게 한다
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.BlocksConstruction))]
    public static class Patch_BlocksConstruction
    {
        public static void Postfix(Thing constructible, Thing t, ref bool __result)
        {
            if (__result && t.def.category == ThingCategory.Plant && PanelUtil.IsPanel(constructible.def))
                __result = false;
        }
    }

    // 프레임/완성품 스폰 시 아래 식물을 지우지 않는다
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.SpawningWipes))]
    public static class Patch_SpawningWipes
    {
        public static void Postfix(BuildableDef newEntDef, BuildableDef oldEntDef, ref bool __result)
        {
            if (__result && oldEntDef is ThingDef o && o.category == ThingCategory.Plant && PanelUtil.IsPanel(newEntDef))
                __result = false;
        }
    }
}

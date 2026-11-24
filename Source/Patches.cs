using System.Collections.Generic;
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
        // 캐시하지 않는다: 게임 중 언어를 바꾸면 def 를 전부 새로 읽어, 잡아 둔 옛 def 와 == 비교가 전부 어긋난다(사전 조회라 매번 찾아도 싸다)
        public static ThingDef PanelDef => DefDatabase<ThingDef>.GetNamedSilentFail("GravshipRoofSolarPanel");

        // 완성품·청사진·프레임 모두 true
        public static bool IsPanel(BuildableDef d)
        {
            ThingDef p = PanelDef;
            if (p == null || !(d is ThingDef t)) return false;
            return t == p || t.entityDefToBuild == p;
        }

        // 완성품이든 청사진·프레임이든, 다 지어졌을 때의 정의
        public static ThingDef Built(ThingDef d) => d == null ? null : (d.entityDefToBuild as ThingDef ?? d);

        // 전선류(일반·숨은·방수 전선). 바닐라가 "전송 건물이 깔고 앉으면 지워지는 것"으로 치는 기준과 같다
        public static bool IsConduit(ThingDef d) => d?.building != null && d.building.isPowerConduit;

        // 패널 칸에 겹친 전선을 없앤다. 패널이 그 칸의 전선 역할을 대신하므로 망은 끊기지 않는다.
        // 바닐라는 한 칸에 전송 건물 하나만 가정한다(PowerNetGrid 칸당 망 하나, ContiguousPowerBuildings 는 칸마다 첫 전송 건물만).
        // 환급 없음(Vanish) — GenSpawn.WipeExistingThings 가 전송 건물 밑 전선을 지울 때와 같다.
        public static void WipeConduitsUnder(Thing panel, Map map)
        {
            if (map == null) return;
            foreach (IntVec3 c in panel.OccupiedRect())
            {
                if (!c.InBounds(map)) continue;
                var things = c.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing t = things[i];
                    if (t == panel || t.Destroyed || !IsConduit(t.def)) continue;
                    DetachChildren(t, map);
                    t.Destroy(DestroyMode.Vanish);
                }
            }
        }

        // 전선을 지우기 전에, 그 전선에 물려 있던 기기(connectParent 가 이 전선)를 우리가 먼저 떼어 낸다.
        // 그냥 지우면 바닐라가 두 가지를 한다(PowerNetManager 의 DeregisterTransmitter → DisconnectAllFromTransmitterAndSetWantConnect):
        //  (1) 물려 있던 기기 전부 PowerOn=false — 망 틱이 몇 대씩 다시 켜 줄 때까지 "전력 없음"(로드 직후 일시정지면 계속 꺼진 채로 경고가 뜬다)
        //  (2) 목록에 남은 옛 세이브의 패널까지 "연결 원함"으로 등록 → 패널이 자기 칸의 전송 건물(자신)에 connector 로 붙어
        //      "PowerNet adding powerComp … which it already has" 오류.
        // 목록을 비워 두면 둘 다 일어나지 않고, 기기는 꺼지지 않은 채 다음 망 갱신 때 6칸 안 가장 가까운 전송 칸(패널 포함)에 바닐라 방식으로 붙는다.
        private static void DetachChildren(Thing conduit, Map map)
        {
            CompPower cp = (conduit as ThingWithComps)?.GetComp<CompPower>();
            List<CompPower> children = cp?.connectChildren;
            if (children == null) return;
            cp.connectChildren = null;
            foreach (CompPower child in children)
            {
                if (child == null || child.connectParent != cp) continue;
                child.connectParent = null;
                // 이미 스폰된 진짜 connector 만 재연결 대기열에 넣는다(바닐라 LostConnectParent 와 같음, 중복 등록은 바닐라가 걸러 낸다).
                // 아직 스폰 전인 기기는 자기 PostSpawnSetup 이 등록하고, 전송 건물(패널)은 connector 가 아니라 등록하면 안 된다.
                if (child.parent.Spawned && child.parent.def.ConnectToPower)
                    map.powerNetManager.Notify_ConnectorWantsConnect(child);
            }
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

    // 패널은 전송 건물(transmitsPower=true)인데 비-edifice 라, 바닐라 CanPlaceBlueprintOver 가
    // "비-edifice 전송 건물은 다른 전송 건물 위 금지" 줄에서 전선 위 배치까지 막는다(바닐라 태양광은 edifice 라 전선 위에 놓인다).
    // 여기서는 통과시키고, 전선이 아닌 전송 건물(배터리·발전기·스위치)은 PlaceWorker_RoofSolar 가 사유를 달아 거부한다.
    // 반대로 패널(청사진·프레임 포함) 위에 다른 전송 건물을 짓는 것은 막는다 — edifice 는 비-edifice 위에 놓이는 게 기본이라
    // 그대로 두면 한 칸에 전송 건물이 둘이 된다. 전선은 PlaceWorker_Conduit 이 이미 막는다.
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanPlaceBlueprintOver))]
    public static class Patch_CanPlaceBlueprintOver
    {
        public static void Postfix(BuildableDef newDef, ThingDef oldDef, ref bool __result)
        {
            ThingDef panel = PanelUtil.PanelDef;
            if (panel == null || oldDef == null) return;
            if (newDef == panel)
            {
                if (!__result && PanelUtil.Built(oldDef).EverTransmitsPower
                    && (oldDef.building == null || oldDef.building.canBuildNonEdificesUnder))
                    __result = true;
            }
            else if (__result && newDef is ThingDef nd && nd.EverTransmitsPower && PanelUtil.IsPanel(oldDef))
            {
                __result = false;
            }
        }
    }

    // 세이브 로드는 건물을 크기순으로 SpawnBuildingAsPossible 에 넣는데, 이 함수는 "새 건물이 지울 건물"이 칸에 있으면
    // 지우는 대신 새 건물 쪽을 환급해 버린다. 전송 건물이 되기 전(connector 방식)에 지은 패널·프레임은 숨은 전선과 겹쳐 있어
    // 그대로 두면 로드할 때 패널이 사라진다. 그래서 패널(프레임)이 스폰되기 직전에 밑 전선을 먼저 없앤다(전선은 1x1 이라 먼저 스폰돼 있다).
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.SpawnBuildingAsPossible))]
    public static class Patch_SpawnBuildingAsPossible
    {
        public static void Prefix(Building building, Map map)
        {
            if (building == null || map == null) return;
            if (building.def == PanelUtil.PanelDef || (building.def.IsFrame && PanelUtil.IsPanel(building.def)))
                PanelUtil.WipeConduitsUnder(building, map);
        }
    }
}

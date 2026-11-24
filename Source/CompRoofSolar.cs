using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipRoofSolar
{
    public class CompProperties_RoofSolar : CompProperties
    {
        public CompProperties_RoofSolar() { compClass = typeof(CompRoofSolar); }
    }

    // 발전 로직(CompPowerPlant 흉내) + 지지(하부구조·지붕) 검사. 전력망 등록은 정확히 CompPowerTrader 인 별도 comp 가 맡는다
    // (ThingDef.ConnectToPower 는 compClass 가 정확히 CompPowerTrader/CompPowerBattery 일 때만 true).
    public class CompRoofSolar : ThingComp
    {
        private const int CheckInterval = 60;
        // 스폰 직후 유예. 중력선 착륙은 건물(SpawnNonPawnThings)이 지붕(SpawnRoofs)보다 먼저 스폰되므로 필요하다
        private const int SpawnGraceTicks = 600;
        // 중력선 연출 중에는 검사를 미루고, 끝난 뒤에도 이만큼 더 유예한다
        private const int GravshipGraceTicks = 180;

        private int graceUntilTick = -1;
        private bool onSubstructure = true;
        private bool roofed = true;
        private CompPowerTrader trader;

        private bool Supported => onSubstructure && roofed;

        private static bool InGravshipTransit =>
            GravshipPlacementUtility.placingGravship || WorldComponent_GravshipController.CutsceneInProgress;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            trader = parent.GetComp<CompPowerTrader>();
            if (!respawningAfterLoad)
                graceUntilTick = Find.TickManager.TicksGame + SpawnGraceTicks;
            Scan();
            // CompPowerPlant.PostSpawnSetup 와 동일
            if (trader != null && trader.Props.PowerConsumption < 0f && !parent.IsBrokenDown() && FlickUtility.WantsToBeOn(parent))
                trader.PowerOn = true;
        }

        public override void CompTick()
        {
            if (!parent.Spawned) return;
            UpdateOutput();
            if (!parent.IsHashIntervalTick(CheckInterval)) return;
            Scan();
            if (Supported) return;

            int now = Find.TickManager.TicksGame;
            if (InGravshipTransit)
            {
                graceUntilTick = System.Math.Max(graceUntilTick, now + GravshipGraceTicks);
                return;
            }
            if (now < graceUntilTick) return;
            Collapse();
        }

        // CompPowerPlant.UpdateDesiredPowerOutput 와 같은 조건 + 지지 여부. 지붕 감쇠는 없음
        private void UpdateOutput()
        {
            if (trader == null) return;
            // 패널 자신이 전송 건물이라 항상 자기 망(transNet)이 있다 — 바닐라 발전기처럼 망 유무는 따지지 않는다
            if (!Supported || parent.IsBrokenDown() || !FlickUtility.WantsToBeOn(parent) || !trader.PowerOn)
                trader.PowerOutput = 0f;
            else
                trader.PowerOutput = Mathf.Lerp(0f, -trader.Props.PowerConsumption, parent.Map.skyManager.CurSkyGlow);
        }

        private void Scan()
        {
            Map map = parent.Map;
            bool sub = true, roof = true;
            foreach (IntVec3 c in parent.OccupiedRect())
            {
                if (!(map.terrainGrid.FoundationAt(c)?.IsSubstructure ?? false)) sub = false;
                if (!c.Roofed(map)) roof = false;
            }
            onSubstructure = sub;
            roofed = roof;
        }

        private void Collapse()
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            bool player = parent.Faction == Faction.OfPlayer;
            string label = parent.LabelCap;
            string key = !onSubstructure ? "GravshipRoofSolar_CollapsedNoSubstructure" : "GravshipRoofSolar_CollapsedNoRoof";
            parent.Kill(); // KillFinalize -> 바닐라 잔해(비용의 25%)
            if (player)
                Messages.Message(key.Translate(label), new TargetInfo(pos, map), MessageTypeDefOf.NegativeEvent);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref graceUntilTick, "graceUntilTick", -1);
        }

        public override string CompInspectStringExtra()
        {
            string sub = (onSubstructure ? "GravshipRoofSolar_StatusOk" : "GravshipRoofSolar_StatusMissing").Translate();
            string roof = (roofed ? "GravshipRoofSolar_StatusOk" : "GravshipRoofSolar_StatusMissing").Translate();
            string line = "GravshipRoofSolar_Inspect".Translate(sub, roof);
            return Supported ? line : line.Colorize(ColorLibrary.RedReadable);
        }
    }
}

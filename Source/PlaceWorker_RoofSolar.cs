using RimWorld;
using Verse;

namespace GravshipRoofSolar
{
    // 16칸 전부 하부구조 + 지붕이 있어야 하고, 다른 지붕 패널(청사진·프레임 포함)과 겹치면 안 된다.
    // 바닥 건물·벽·가구와의 겹침은 바닐라 규칙(비-edifice)으로 이미 허용된다.
    // 패널은 모든 칸이 전송 건물이라 한 칸에 다른 전송 건물과 같이 있을 수 없다: 전선은 건설 때 치워지고(패널이 대신함),
    // 전선이 아닌 전송 건물(배터리·발전기·전원 스위치 등, 청사진·프레임 포함)은 치울 수 없으니 여기서 거부한다.
    public class PlaceWorker_RoofSolar : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            foreach (IntVec3 c in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
            {
                if (!c.InBounds(map)) continue;
                if (!(map.terrainGrid.FoundationAt(c)?.IsSubstructure ?? false))
                    return "GravshipRoofSolar_NeedSubstructure".Translate();
                if (!c.Roofed(map))
                    return "GravshipRoofSolar_NeedRoof".Translate();
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (t == thingToIgnore || t == thing) continue;
                    if (t.def == checkingDef || t.def.entityDefToBuild == checkingDef)
                        return "GravshipRoofSolar_Overlap".Translate();
                    ThingDef built = PanelUtil.Built(t.def);
                    if (built.EverTransmitsPower && !PanelUtil.IsConduit(built))
                        return "GravshipRoofSolar_OverTransmitter".Translate(built.label);
                }
            }
            return AcceptanceReport.WasAccepted;
        }
    }
}

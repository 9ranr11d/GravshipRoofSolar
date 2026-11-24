using RimWorld;
using Verse;

namespace GravshipRoofSolar
{
    // 16칸 전부 하부구조 + 지붕이 있어야 하고, 다른 지붕 패널(청사진·프레임 포함)과 겹치면 안 된다.
    // 바닥 건물·벽·가구·전선과의 겹침은 바닐라 규칙(비-edifice)으로 이미 허용된다.
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
                }
            }
            return AcceptanceReport.WasAccepted;
        }
    }
}

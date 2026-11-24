using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipRoofSolar
{
    // 지붕 위에 얹힌 패널. 평소엔 그리지 않고(아래 가구를 가리지 않게), 선택·전력 오버레이·해체/건설 지정 중에만 옅게 그린다.
    // 배치 중 고스트와 건설 중 프레임은 바닐라 Blueprint/Frame 이 따로 그린다.
    // drawerType 이 MapMeshAndRealTime 인 이유: 전력 오버레이 전선(SectionLayer_ThingsPowerGrid)이 맵 메시 대상만 찍기 때문.
    // 대신 Print 를 비워 맵 메시에 상시 그려지지 않게 하고, 실시간 그리기는 직접 한다.
    public class Building_RoofSolarPanel : Building
    {
        private bool ShouldDraw
        {
            get
            {
                if (OverlayDrawHandler.ShouldDrawPowerGrid) return true;
                if (Find.Selector != null && Find.Selector.IsSelected(this)) return true;
                var dm = Find.DesignatorManager;
                if (dm?.SelectedDesignator is Designator_Deconstruct) return true;
                if (dm?.SelectedDesignator is Designator_Build db && db.PlacingDef == def) return true;
                return Spawned && Map.designationManager.DesignationOn(this, DesignationDefOf.Deconstruct) != null;
            }
        }

        public override void Print(SectionLayer layer) { }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (ShouldDraw)
                Graphic.Draw(drawLoc, flip ? Rotation.Opposite : Rotation, this);
        }
    }
}

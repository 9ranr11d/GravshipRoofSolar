using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipRoofSolar
{
    // 지붕 위에 얹힌 패널. 평소엔 그리지 않고(아래 가구를 가리지 않게), 선택·전력 오버레이·해체/건설 지정 중에만 옅게 그린다.
    // 배치 중 고스트와 건설 중 프레임은 바닐라 Blueprint/Frame 이 따로 그린다.
    // Print 에서 본체를 빼 맵 메시에 상시 그려지지 않게 하고, 실시간 그리기는 직접 한다.
    public class Building_RoofSolarPanel : Building
    {
        private bool ShouldDraw
        {
            get
            {
                if (GameComponent_RoofSolarView.Show) return true;
                if (OverlayDrawHandler.ShouldDrawPowerGrid) return true;
                if (Find.Selector != null && Find.Selector.IsSelected(this)) return true;
                var dm = Find.DesignatorManager;
                if (dm?.SelectedDesignator is Designator_Deconstruct) return true;
                if (dm?.SelectedDesignator is Designator_Build db && db.PlacingDef == def) return true;
                return Spawned && Map.designationManager.DesignationOn(this, DesignationDefOf.Deconstruct) != null;
            }
        }

        // 본체를 맵 메시에 찍지 않는다(상시 표시 방지). 전송 건물이라 찍을 연결선도 없다.
        // 전력 오버레이의 전송 표시는 Print 가 아니라 PrintForPowerGrid(CompPower.CompPrintForPowerGrid)가 바닐라 그대로 찍는다.
        public override void Print(SectionLayer layer)
        {
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            // connector 방식(transmitsPower=false) 때 저장된 세이브는 근처 전선을 connectParent 로 들고 온다.
            // 전송 건물에 connectParent 가 남으면 "다시 연결" 기즈모·가짜 연결선이 생기므로, 망에 등록되기 전에 끊는다.
            CompPower power = PowerComp;
            // 5차 DLL 로 한 번 저장된 세이브는 패널이 자기 자신을 connectParent 로 들고 올 수도 있다(6차에서 원인 제거) — 같은 식으로 끊긴다.
            if (power?.connectParent != null)
            {
                CompPower old = power.connectParent;
                power.connectParent = null;
                if (old.connectChildren != null)
                {
                    old.connectChildren.Remove(power);
                    // 빈 목록은 바닐라처럼 null 로(PowerConnectionMaker.DisconnectFromPowerNet 과 같음)
                    if (old.connectChildren.Count == 0) old.connectChildren = null;
                }
            }
            // 한 칸에 전송 건물 둘이 되지 않게, 망에 등록(base)되기 전에 밑 전선을 치운다.
            // 보통은 바닐라 GenSpawn 이 스폰 직전에 이미 지웠고, 로드 때는 Patch_SpawnBuildingAsPossible 이 먼저 지운다 — 그 밖의 경로용 안전판
            PanelUtil.WipeConduitsUnder(this, map);
            base.SpawnSetup(map, respawningAfterLoad);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!ShouldDraw) return;
            // def 의 altitudeLayer(Conduits)는 선택 우선순위·겹쳐 짓기용이라 그대로 두고, 그릴 때만 높이를 올린다.
            // Conduits 높이 그대로 그리면 아래 방의 가구·벽(Building 층)에 전부 가려 안 보인다(지붕 위 패널인데 바닥에 깔린 꼴).
            // 전력 오버레이 연결선(MapDataOverlay)보다는 살짝 아래라 연결선·접속점은 패널 위에 보인다.
            drawLoc.y = AltitudeLayer.MapDataOverlay.AltitudeFor() - 0.01f;
            Graphic.Draw(drawLoc, flip ? Rotation.Opposite : Rotation, this);
        }
    }
}

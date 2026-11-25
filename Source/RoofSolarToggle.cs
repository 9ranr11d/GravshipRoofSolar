using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GravshipRoofSolar
{
    // 세이브별로 "지붕 태양광 표시" 토글 상태를 기억한다.
    // GameComponent 는 Game.FillComponents 가 (Game) 생성자를 가진 하위 클래스를 자동 추가하므로 기존 세이브도 안전하다.
    public class GameComponent_RoofSolarView : GameComponent
    {
        public bool showRoofSolar;

        public GameComponent_RoofSolarView(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref showRoofSolar, "showRoofSolar", false);
        }

        // 그리기 경로(매 프레임)에서 불리므로 null 안전하게
        public static bool Show => Current.Game?.GetComponent<GameComponent_RoofSolarView>()?.showRoofSolar ?? false;
    }

    // 전용 아이콘(바닐라 플레이 설정 아이콘과 같은 64px 회색 채움 + 검은 외곽선). 패널 텍스처를 줄여 쓰면 색이 있어 옆 버튼들 사이에서 튄다.
    // 생성 스크립트는 GravshipRoofSolar-work/icon-candidates/make_roof_variants.py (roof_e). 텍스처 로드는 메인 스레드에서만 가능 — 버튼 그리기(GUI)가 메인 스레드라 거기서 늦게 읽는다.
    // static readonly 로 한 번만 잡으면 게임 중 언어 변경(콘텐츠 전부 재로드) 때 옛 텍스처가 파괴돼 null 취급 → 버튼이 사라졌다. 파괴됐으면 다시 찾는다.
    public static class RoofSolarToggleIcon
    {
        private static Texture2D icon;
        public static Texture2D Icon
        {
            get
            {
                if (icon == null) icon = ContentFinder<Texture2D>.Get("UI/GravshipRoofSolar/ShowRoofSolar", false);
                return icon;
            }
        }
    }

    // 오른쪽 아래 플레이 설정 토글 줄(방·온도 표시 등)에 버튼을 추가
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettingsToggle
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null) return;
            var comp = Current.Game?.GetComponent<GameComponent_RoofSolarView>();
            if (comp == null || RoofSolarToggleIcon.Icon == null) return;
            bool before = comp.showRoofSolar;
            row.ToggleableIcon(ref comp.showRoofSolar, RoofSolarToggleIcon.Icon,
                "GravshipRoofSolar_ShowToggle".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            // 끄는 순간 선택돼 있던 패널은 풀어 준다(꺼진 동안은 선택 불가이므로)
            if (before && !comp.showRoofSolar && Find.Selector != null)
            {
                foreach (object o in Find.Selector.SelectedObjects.ToList())
                    if (o is Building_RoofSolarPanel) Find.Selector.Deselect(o);
            }
        }
    }

    // 토글이 꺼져 있으면 패널을 지도 클릭·드래그 박스·더블클릭으로 선택하지 못하게 한다(2026-11-25 사용자: 아래 가구를 고를 때 패널이 걸림).
    // 클릭(TargetingParameters.mustBeSelectable)·드래그 박스(MultiSelectableThingsInScreenRectDistinct)·같은 종류 전체 선택이 모두 이 함수를 거친다.
    // 해체 지정 도구는 선택과 별개라 그대로 쓸 수 있다.
    [HarmonyPatch(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectableByMapClick))]
    public static class Patch_SelectableByMapClick
    {
        public static void Postfix(Thing t, ref bool __result)
        {
            if (__result && t is Building_RoofSolarPanel && !GameComponent_RoofSolarView.Show) __result = false;
        }
    }
}

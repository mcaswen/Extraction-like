using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    /// <summary>Uses the existing saved HUD without loading/saving the map, NavMesh or player saves.</summary>
    public sealed class MapGraphViewportVisibilityTests
    {
        private GameObject _root;
        private MapGraphPresenter _map;
        private MapGraphViewport Viewport=>_map.Viewport;
        private CanvasGroup Group=>_root.GetComponent<CanvasGroup>();
        private GameObject Backdrop=>_root.transform.Find("ExpandedBackdrop").gameObject;

        [SetUp] public void Setup()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MapGraphUguiPrefabFactory.HudPath);
            Assert.That(prefab,Is.Not.Null);
            _root=Object.Instantiate(prefab);
            _root.hideFlags=HideFlags.HideAndDontSave;
            _map=_root.GetComponent<MapGraphPresenter>();
            var definition=AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>("Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset");
            var theme=AssetDatabase.LoadAssetAtPath<SO_MapGraphTheme>(MapGraphUguiPrefabFactory.ThemePath);
            _map.Overlay.Initialize(definition,theme,_=>{},(_,__)=>{});
            Viewport.Initialize(_map.Overlay);
        }

        [TearDown] public void Cleanup()
        {
            if(_root!=null)Object.DestroyImmediate(_root);
        }

        [Test] public void MapStartsVisibleAndCompact()
        {
            Assert.That(Viewport.IsVisible,Is.True);
            Assert.That(Viewport.IsExpanded,Is.False);
            Assert.That(Group.alpha,Is.EqualTo(1));
            Assert.That(Group.interactable&&Group.blocksRaycasts,Is.True);
            Assert.That(Backdrop.activeSelf,Is.False);
        }

        [Test] public void HideAndRestoreCompactKeepsTheControllerRunningAndReleasesPointerInput()
        {
            int builds=_map.Overlay.BuildCount;
            Viewport.ToggleVisibility();
            Assert.That(Viewport.IsVisible,Is.False);
            Assert.That(Viewport.isActiveAndEnabled,Is.True);
            Assert.That(_root.activeInHierarchy,Is.True);
            Assert.That(Group.alpha,Is.Zero);
            Assert.That(Group.interactable||Group.blocksRaycasts,Is.False);
            Viewport.ToggleVisibility();
            Assert.That(Viewport.IsVisible,Is.True);
            Assert.That(Viewport.IsExpanded,Is.False);
            Assert.That(Group.alpha,Is.EqualTo(1));
            Assert.That(Group.interactable&&Group.blocksRaycasts,Is.True);
            Assert.That(_map.Overlay.BuildCount,Is.EqualTo(builds));
            Assert.That(_map.SubmittedCommandCount,Is.Zero);
        }

        [Test] public void ExpandedHideAndRestorePreservesZoomPanAndSameViews()
        {
            Viewport.SetExpanded(true);
            Viewport.OnScroll(new PointerEventData(null){scrollDelta=new Vector2(0,2)});
            Viewport.OnDrag(new PointerEventData(null){button=PointerEventData.InputButton.Left,delta=new Vector2(20,-10)});
            float zoom=Viewport.Zoom;Vector2 pan=Viewport.Pan;
            int builds=_map.Overlay.BuildCount;
            var nodes=_map.Overlay.NodeViews;
            Viewport.ToggleVisibility();
            Assert.That(Backdrop.activeSelf,Is.False);
            Viewport.ToggleVisibility();
            Assert.That(Viewport.IsExpanded,Is.True);
            Assert.That(Backdrop.activeSelf,Is.True);
            Assert.That(Viewport.Zoom,Is.EqualTo(zoom));
            Assert.That(Viewport.Pan,Is.EqualTo(pan));
            Assert.That(_map.Overlay.NodeViews,Is.SameAs(nodes));
            Assert.That(_map.Overlay.BuildCount,Is.EqualTo(builds));
            Assert.That(_map.SubmittedCommandCount,Is.Zero);
        }

        [Test] public void HiddenMapIgnoresScrollAndDrag()
        {
            Viewport.SetExpanded(true);Viewport.SetVisible(false);
            Viewport.OnScroll(new PointerEventData(null){scrollDelta=new Vector2(0,2)});
            Viewport.OnDrag(new PointerEventData(null){button=PointerEventData.InputButton.Left,delta=new Vector2(20,-10)});
            Assert.That(Viewport.Zoom,Is.EqualTo(1));
            Assert.That(Viewport.Pan,Is.EqualTo(Vector2.zero));
            Assert.That(Viewport.IsVisible,Is.False);
        }

        [Test] public void HiddenMapRejectsGraphicRaycastFiltersAndRestoringAcceptsThem()
        {
            var graphic=Viewport.Panel.GetComponent<Image>();
            Vector2 pointer=RectTransformUtility.WorldToScreenPoint(null,Viewport.Panel.TransformPoint(Viewport.Panel.rect.center));
            // Exercise uGUI's real parent CanvasGroup filter without requiring an onscreen render/depth pass in EditMode.
            Assert.That(graphic.Raycast(pointer,null),Is.True,"Visible map must accept pointer raycasts.");
            Viewport.ToggleVisibility();
            Assert.That(graphic.Raycast(pointer,null),Is.False,"Invisible map must reject pointer raycasts.");
            Viewport.ToggleVisibility();
            Assert.That(graphic.Raycast(pointer,null),Is.True,"Restored map must accept pointer raycasts again.");
        }

        [Test] public void MWhileHiddenOpensExpandedAndThenReturnsToCompact()
        {
            Viewport.SetVisible(false);Viewport.ToggleExpanded();
            Assert.That(Viewport.IsVisible,Is.True);
            Assert.That(Viewport.IsExpanded,Is.True);
            Viewport.ToggleExpanded();
            Assert.That(Viewport.IsVisible,Is.True);
            Assert.That(Viewport.IsExpanded,Is.False);
            Assert.That(_map.SubmittedCommandCount,Is.Zero);
        }

        [Test] public void HiddenStateSurvivesViewportReinitializationAndLayoutChanges()
        {
            Viewport.SetVisible(false);
            Viewport.Initialize(_map.Overlay);
            Viewport.SetExpanded(true);
            Assert.That(Viewport.IsVisible,Is.False);
            Assert.That(Group.alpha,Is.Zero);
            Assert.That(Group.blocksRaycasts,Is.False);
            Assert.That(Backdrop.activeSelf,Is.False);
            Viewport.ToggleVisibility();
            Assert.That(Viewport.IsVisible,Is.True);
            Assert.That(Viewport.IsExpanded,Is.True);
            Assert.That(Backdrop.activeSelf,Is.True);
        }
    }
}

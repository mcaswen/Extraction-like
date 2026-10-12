using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Vector-machined terminal surfaces. No textures, layout changes or extra input targets.</summary>
    public sealed class AerospaceInstrumentSurface : Image, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum Finish { Panel, Control, Tab, Archive, Primary }
        public Finish finish;
        public bool hitTargetOnly;
        public Func<bool> reduceMotion;
        public bool IsSelected { get; private set; }
        public float HoverAmount => hover;
        private bool inside, pressed;
        private float hover, selection;
        private Button button;
        private static readonly Color Edge = new Color(.23f, .43f, .65f, .48f);
        private static readonly Color Signal = new Color(.48f, .90f, 1f);

        public void SetSelected(bool value)
        {
            if (IsSelected == value) return;
            IsSelected = value; SetVerticesDirty();
        }
        protected override void OnEnable() { base.OnEnable(); button = GetComponent<Button>(); }
        protected override void OnDisable()
        {
            inside = pressed = false; hover = 0; selection = IsSelected ? 1 : 0;
            base.OnDisable();
        }
        private void LateUpdate()
        {
            if (finish == Finish.Panel) return;
            if (button == null) button = GetComponent<Button>();
            bool enabled = button == null || button.IsInteractable();
            float target = enabled && inside ? (pressed ? .35f : 1) : 0;
            float step = reduceMotion != null && reduceMotion() ? 1 : Time.unscaledDeltaTime / .14f;
            float nextHover = Mathf.MoveTowards(hover, target, step);
            float nextSelection = Mathf.MoveTowards(selection, IsSelected ? 1 : 0, step);
            if (nextHover == hover && nextSelection == selection) return;
            hover = nextHover; selection = nextSelection; SetVerticesDirty();
        }
        public void OnPointerEnter(PointerEventData data) { inside = true; }
        public void OnPointerExit(PointerEventData data) { inside = pressed = false; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (hitTargetOnly) return; Rect r = rectTransform.rect;
            float cut = finish == Finish.Control || finish == Finish.Primary ? 6 : finish == Finish.Archive ? 9 : 0;
            cut = Mathf.Min(cut, Mathf.Min(r.width, r.height) * .2f);
            // Only two opposing corners are chamfered. The full rectangular hit area is retained.
            Vector2[] points = {
                new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax - cut, r.yMin),
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax - cut)
            };
            Color fill = color;
            if (finish == Finish.Archive && fill.a < .01f) fill = new Color(.045f, .083f, .145f, .36f + hover * .32f);
            if (finish == Finish.Primary) fill = Color.Lerp(fill, new Color(.08f, .29f, .43f, 1), .45f);
            Color upper = new Color(fill.r + .014f, fill.g + .024f, fill.b + .042f, fill.a);
            Color lower = new Color(fill.r * .78f, fill.g * .80f, fill.b * .86f, fill.a);
            mesh.AddVert(r.center, Color.Lerp(lower, upper, .5f), Vector2.zero);
            foreach (Vector2 point in points)
                mesh.AddVert(point, Color.Lerp(lower, upper, Mathf.InverseLerp(r.yMin, r.yMax, point.y)), Vector2.zero);
            for (int i = 0; i < points.Length; i++) mesh.AddTriangle(0, i + 1, (i + 1) % points.Length + 1);

            float emphasis = Mathf.Max(selection, finish == Finish.Primary ? .66f : hover * .65f);
            Color edge = Color.Lerp(Edge, Signal, emphasis); edge.a = Mathf.Lerp(.36f, .85f, emphasis);
            if (finish == Finish.Panel) edge.a = .28f;
            if (finish == Finish.Archive && !IsSelected) edge.a *= .45f;
            for (int i = 0; i < points.Length; i++) Line(mesh, points[i], points[(i + 1) % points.Length], edge, .8f);
            if (finish == Finish.Panel)
            {
                Line(mesh, new Vector2(r.xMax - 2, r.yMax - 24), new Vector2(r.xMax - 2, r.yMin + 24), new Color(.13f, .30f, .50f, .23f));
                return;
            }
            Line(mesh, new Vector2(r.xMin + cut + 3, r.yMax - 2), new Vector2(r.xMax - 3, r.yMax - 2), new Color(.43f, .72f, .92f, .06f + .10f * hover));
            Line(mesh, new Vector2(r.xMin + 3, r.yMin + 2), new Vector2(r.xMax - cut - 3, r.yMin + 2), new Color(.01f, .025f, .05f, .45f));
            if (finish == Finish.Primary)
            {
                Line(mesh, new Vector2(r.xMax - 15, r.yMin + 12), new Vector2(r.xMax - 10, r.yMin + 17), Signal, 1.3f);
                Line(mesh, new Vector2(r.xMax - 19, r.yMin + 12), new Vector2(r.xMax - 14, r.yMin + 17), new Color(Signal.r, Signal.g, Signal.b, .4f), 1.3f);
            }
            if (selection > .001f && finish != Finish.Archive)
            {
                Color signal = Signal; signal.a = selection * .94f;
                float length = (r.width - 20) * selection;
                Line(mesh, new Vector2(r.center.x - length * .5f, r.yMin + 1), new Vector2(r.center.x + length * .5f, r.yMin + 1), signal, 2);
            }
        }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color tint, float width = 1)
        {
            if ((a - b).sqrMagnitude < .001f) return;
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * (width * .5f);
            int i = mesh.currentVertCount;
            mesh.AddVert(a - n, tint, Vector2.zero); mesh.AddVert(a + n, tint, Vector2.zero);
            mesh.AddVert(b + n, tint, Vector2.zero); mesh.AddVert(b - n, tint, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}

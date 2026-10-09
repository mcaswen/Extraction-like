using UnityEngine;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceInspectionStage
    {
        private Vector2 orbitVelocity;
        private bool dragging, resetting;
        private float lastDragTime, coastRemaining, shownZoom = 1, resetAge, assembledFitDistance;
        private float resetYaw, resetPitch, shownYaw, shownPitch;
        private Vector3 resetCameraPosition, resetCameraFocus;
        private float shownFold, foldFrom, foldTarget, foldAge;
        public bool IsCoasting => coastRemaining > 0;
        public bool IsResettingView => resetting;
        public float DisplayZoom => shownZoom;
        public float TargetZoom => zoom;
        public float FoldAngle => shownFold;

        private static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
        public void BeginOrbitGesture()
        {
            if (!Ready) return;
            CancelCameraMotion(); dragging = true;
        }
        public void EndOrbitGesture()
        {
            if (!dragging) return;
            dragging = false;
            coastRemaining = !ReducedMotion && Time.unscaledTime - lastDragTime < .09f && orbitVelocity.sqrMagnitude > 1 ? .22f : 0;
            if (coastRemaining == 0) orbitVelocity = Vector2.zero;
        }
        public void CancelCameraMotion()
        {
            if (resetting) { pitch = shownPitch; yaw = shownYaw; }
            dragging = resetting = false; coastRemaining = 0; orbitVelocity = Vector2.zero;
        }
        public void Orbit(Vector2 delta)
        {
            if (!Ready) return;
            if (resetting) { pitch = shownPitch; yaw = shownYaw; }
            resetting = false; coastRemaining = 0; userCamera = true;
            Vector2 degrees = delta * .32f;
            yaw += degrees.x; pitch -= degrees.y;
            if (dragging)
            {
                var speed = Vector2.ClampMagnitude(degrees / Mathf.Max(Time.unscaledDeltaTime, 1f / 120), 240);
                orbitVelocity = Vector2.Lerp(orbitVelocity, speed, .65f); lastDragTime = Time.unscaledTime;
            }
        }
        public void Zoom(float delta)
        {
            if (!Ready) return;
            if (resetting) { pitch = shownPitch; yaw = shownYaw; }
            resetting = false; userCamera = true;
            zoom = Mathf.Clamp(zoom - delta * .055f, .64f, 1.85f);
            if (ReducedMotion) shownZoom = zoom;
        }
        private void TickViewMotion()
        {
            float dt = Time.unscaledDeltaTime;
            if (ReducedMotion) { CancelCameraMotion(); shownZoom = zoom; }
            if (!dragging && coastRemaining > 0)
            {
                float step = Mathf.Min(dt, coastRemaining), decay = Mathf.Exp(-step * 18);
                Vector2 travel = orbitVelocity * ((1 - decay) / 18);
                yaw += travel.x; pitch -= travel.y;
                orbitVelocity *= decay; coastRemaining = Mathf.Max(0, coastRemaining - dt);
                if (coastRemaining == 0) orbitVelocity = Vector2.zero;
            }
            shownZoom = Mathf.Lerp(shownZoom, zoom, ReducedMotion ? 1 : 1 - Mathf.Exp(-dt * 23));
            if (Mathf.Abs(shownZoom - zoom) < .0001f) shownZoom = zoom;
            if (resetting) resetAge = Mathf.Min(.4f, resetAge + dt);
            shownPitch = resetting ? Mathf.LerpAngle(resetPitch, 0, Ease(resetAge / .4f)) : pitch;
            shownYaw = resetting ? Mathf.LerpAngle(resetYaw, 0, Ease(resetAge / .4f)) : yaw;
            pivot.localRotation = Quaternion.Euler(shownPitch, shownYaw, 0);
        }
        private Vector3 TargetPosition(Pose p)
        {
            Vector3 extra = Vector3.zero;
            float release = DemoActive && config.code == "R05" ? Ease((DemoTime - 2.4f) / .56f) : 1;
            float separation = DemoActive && config.code == "R05" ? Ease((DemoTime - 4.8f) / .6f) : 1;
            if (RelationPhase >= 1 && ReleaseParts.Contains(p.semantic)) extra += new Vector3(0, 0, -.105f) * release;
            if (RelationPhase == 2 && UpperParts.Contains(p.semantic)) extra += new Vector3(0, .19f, 0) * separation;
            return p.position + p.t.parent.InverseTransformVector(pivot.TransformVector(extra * modelScale)) + (Mode == "exploded" ? p.offset : Vector3.zero);
        }
        private void TickMechanicalPose(Pose p)
        {
            Vector3 target = TargetPosition(p);
            if ((p.moveTarget - target).sqrMagnitude > .00000001f)
            {
                p.moveFrom = p.t.localPosition; p.moveTarget = target; p.moveAge = 0;
                p.moveDelay = Mode == "exploded" ? p.motionOrder : .12f - p.motionOrder;
            }
            p.moveAge += Time.unscaledDeltaTime;
            // Demonstration geometry follows the same clock as its tracers, so Pause freezes both.
            p.t.localPosition = ReducedMotion || (DemoActive && config.code == "R05") ? target : Vector3.LerpUnclamped(p.moveFrom, target, Ease((p.moveAge - p.moveDelay) / .46f));
            p.t.localRotation = ReducedMotion ? p.rotation : Quaternion.Slerp(p.t.localRotation, p.rotation, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18));
            float visible = Mode == "cutaway" && p.cut ? 0 : 1;
            if (p.visibleTarget != visible) { p.visibleFrom = p.visibility; p.visibleTarget = visible; p.visibleAge = 0; }
            p.visibleAge += Time.unscaledDeltaTime;
            p.visibility = ReducedMotion ? visible : Mathf.Lerp(p.visibleFrom, visible, Ease(p.visibleAge / .42f));
            p.renderer.enabled = p.visibility > .001f;
        }
        private void TickFold()
        {
            if (foldPivot == null) return;
            float target = Folded ? 65 : 0;
            if (DemoActive && config.code == "R04")
            {
                shownFold = DemoStep == 0 ? Mathf.Lerp(65, 0, Ease(DemoTime / 1.85f)) : 0;
                foldFrom = shownFold; foldTarget = float.NaN;
            }
            else
            {
                if (float.IsNaN(foldTarget) || !Mathf.Approximately(foldTarget, target)) { foldFrom = shownFold; foldTarget = target; foldAge = 0; }
                foldAge += Time.unscaledDeltaTime;
                shownFold = ReducedMotion ? target : Mathf.Lerp(foldFrom, target, Ease(foldAge / .56f));
            }
            foldPivot.localRotation = foldRest * Quaternion.AngleAxis(shownFold, Vector3.right);
        }
        private void ClearViewMotion()
        {
            CancelCameraMotion(); shownZoom = 1; shownFold = foldFrom = foldTarget = foldAge = 0;
            shownYaw = shownPitch = 0;
            assembledFitDistance = 0;
        }
    }
}

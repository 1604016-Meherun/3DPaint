using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ImmersiDraw.Study
{
    // A world-space panel built entirely in code, so no scene edits are needed.
    // Buttons can be pressed with the XR ray (TrackedDeviceGraphicRaycaster) or with the controllers:
    // move between buttons with a thumbstick, press A / X to click.
    public class StudyPanelUI : MonoBehaviour
    {
        public struct ButtonSpec
        {
            public string label;
            public Action onClick;
            public bool highlighted;   // e.g. the currently selected stress rating
            public bool interactable;
            public ButtonSpec(string label, Action onClick, bool highlighted = false, bool interactable = true)
            { this.label = label; this.onClick = onClick; this.highlighted = highlighted; this.interactable = interactable; }
        }

        static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        static readonly Color ButtonColor = new Color(0.22f, 0.27f, 0.36f, 1f);
        static readonly Color HighlightColor = new Color(0.20f, 0.55f, 0.85f, 1f);
        static readonly Color FocusColor = new Color(0.95f, 0.75f, 0.20f, 1f);
        static readonly Color DisabledColor = new Color(0.18f, 0.18f, 0.20f, 1f);

        private Canvas canvas;
        private RectTransform panel;
        private TextMeshProUGUI titleText, bodyText;
        private RectTransform buttonArea;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<ButtonSpec> specs = new List<ButtonSpec>();
        private int focus = -1;

        // Controller navigation
        public bool LeftHandOnly;   // during drawing, only the left controller navigates (right hand draws)
        private bool prevLeftPress, prevRightPress;
        private float navCooldown;

        public static StudyPanelUI Create()
        {
            var go = new GameObject("Study Panel (runtime)");
            var ui = go.AddComponent<StudyPanelUI>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
            // Lets XR Interaction Toolkit ray interactors click these buttons.
            gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(900, 620);
            rt.localScale = Vector3.one * 0.0012f;

            panel = NewRect("Panel", rt);
            Stretch(panel);
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(40, 40, 36, 36);
            v.spacing = 22;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            titleText = NewText("Title", panel, 46, FontStyles.Bold);
            bodyText = NewText("Body", panel, 32, FontStyles.Normal);

            buttonArea = NewRect("Buttons", panel);
            var bv = buttonArea.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 16; bv.childAlignment = TextAnchor.MiddleCenter;
            bv.childControlWidth = true; bv.childControlHeight = true;
            bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
        }

        // rows: each inner array is one horizontal row of buttons.
        public void Show(string title, string body, params ButtonSpec[][] rows)
        {
            gameObject.SetActive(true);
            titleText.text = title ?? "";
            bodyText.text = body ?? "";
            bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));

            for (int i = buttonArea.childCount - 1; i >= 0; i--) Destroy(buttonArea.GetChild(i).gameObject);
            buttons.Clear(); specs.Clear();

            foreach (var row in rows)
            {
                var rowRt = NewRect("Row", buttonArea);
                var h = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 16; h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = true; h.childControlHeight = true;
                h.childForceExpandWidth = true; h.childForceExpandHeight = false;
                foreach (var spec in row) AddButton(rowRt, spec);
            }

            focus = -1;
            for (int i = 0; i < specs.Count; i++) if (specs[i].highlighted && specs[i].interactable) { focus = i; break; }
            if (focus < 0) for (int i = 0; i < specs.Count; i++) if (specs[i].interactable) { focus = i; break; }
            RefreshColors();
        }

        public void Hide() => gameObject.SetActive(false);

        // Places the panel in front of the user (yaw only). Negative yawOffset = to the user's left.
        public void PlaceInFrontOfUser(float distance, float yawOffsetDegrees = 0f, float heightOffset = 0f, float scaleFactor = 1f)
        {
            var cam = Camera.main;
            if (cam == null) return;
            canvas.worldCamera = cam;
            Vector3 fwd = cam.transform.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd = Quaternion.Euler(0, yawOffsetDegrees, 0) * fwd.normalized;
            transform.position = cam.transform.position + fwd * distance + Vector3.up * heightOffset;
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            transform.localScale = Vector3.one * 0.0012f * scaleFactor;
        }

        void AddButton(RectTransform parent, ButtonSpec spec)
        {
            var rt = NewRect("Button " + spec.label, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 84; le.preferredHeight = 84;
            var img = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = spec.interactable;
            var label = NewText("Label", rt, 32, FontStyles.Bold);
            Stretch((RectTransform)label.transform);
            label.text = spec.label;
            label.alignment = TextAlignmentOptions.Center;

            int index = specs.Count;
            btn.onClick.AddListener(() => Click(index));
            buttons.Add(btn); specs.Add(spec);
        }

        void Click(int index)
        {
            if (index < 0 || index >= specs.Count || !specs[index].interactable) return;
            specs[index].onClick?.Invoke();
        }

        void RefreshColors()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var img = (Image)buttons[i].targetGraphic;
                Color c = !specs[i].interactable ? DisabledColor : specs[i].highlighted ? HighlightColor : ButtonColor;
                img.color = c;
                if (!buttons[i].TryGetComponent(out Outline outline)) outline = buttons[i].gameObject.AddComponent<Outline>();
                outline.effectColor = FocusColor;
                outline.effectDistance = new Vector2(6, 6);
                outline.enabled = (i == focus);
            }
        }

        void Update()
        {
            if (specs.Count == 0) return;
            navCooldown -= Time.unscaledDeltaTime;

            float x = 0, y = 0; bool pressed = false;
            ReadHand(XRNode.LeftHand, ref x, ref y, ref prevLeftPress, ref pressed);
            if (!LeftHandOnly) ReadHand(XRNode.RightHand, ref x, ref y, ref prevRightPress, ref pressed);

            if (navCooldown <= 0f && (Mathf.Abs(x) > 0.7f || Mathf.Abs(y) > 0.7f))
            {
                int dir = (x > 0.7f || y < -0.7f) ? 1 : -1;
                MoveFocus(dir);
                navCooldown = 0.3f;
            }
            if (pressed && focus >= 0) Click(focus);
        }

        void ReadHand(XRNode node, ref float x, ref float y, ref bool prev, ref bool pressedEdge)
        {
            var dev = InputDevices.GetDeviceAtXRNode(node);
            if (!dev.isValid) return;
            if (dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (Mathf.Abs(axis.x) > Mathf.Abs(x)) x = axis.x;
                if (Mathf.Abs(axis.y) > Mathf.Abs(y)) y = axis.y;
            }
            dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool p);
            if (p && !prev) pressedEdge = true;
            prev = p;
        }

        void MoveFocus(int dir)
        {
            if (specs.Count == 0) return;
            int start = focus < 0 ? 0 : focus;
            for (int step = 1; step <= specs.Count; step++)
            {
                int i = ((start + dir * step) % specs.Count + specs.Count) % specs.Count;
                if (specs[i].interactable) { focus = i; break; }
            }
            RefreshColors();
        }

        // ---------- small helpers ----------
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, float size, FontStyles style)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.fontStyle = style; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }
    }
}

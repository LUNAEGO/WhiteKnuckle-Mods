using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WKEscapedSubject
{
    // 장신구 & 속박 선택 화면 보정
    // 1) 속박 칸은 3×3(9개)로 짜여 있어 10번째인 이 속박이 칸 밖(설명글 위)으로 밀려난다.
    //    9개를 넘으면 열을 늘리고 칸 전체를 줄여 원래 3×3 영역 안에 맞춘다.
    // 2) 이 속박(과 '생체 실험된')의 설명이 길어 잘리므로, 보여 줄 때만 글자 크기를 자동으로 줄인다
    internal static class PickerLayout
    {
        class Saved
        {
            public Vector3 scale, position;
            public GridLayoutGroup.Constraint constraint;
            public int constraintCount;
            public Vector2 cellSize, spacing;
        }
        static readonly Dictionary<int, Saved> saved = new Dictionary<int, Saved>();

        // 목록을 다시 채울 때 표시만 해 두고, 칸이 실제로 화면에 보일 때(Fitter.LateUpdate) 맞춘다.
        // 목록은 선택 화면이 숨겨진 채로 채워지기도 해서 그 자리에서는 잴 수 없다
        public static void FitBindings(UI_TrinketPicker picker)
        {
            if (picker == null || picker.bindingIconRoot == null) return;
            var f = picker.bindingIconRoot.GetComponent<Fitter>() ?? picker.bindingIconRoot.gameObject.AddComponent<Fitter>();
            f.dirtyFrame = Time.frameCount;
        }

        internal class Fitter : MonoBehaviour
        {
            public int dirtyFrame = -1;
            void LateUpdate()
            {
                // PopulateTrinkets가 지운 이전 아이콘은 그 프레임 끝에 사라지므로 다음 프레임부터 잰다
                if (dirtyFrame < 0 || Time.frameCount <= dirtyFrame) return;
                dirtyFrame = -1;
                try { Fit(transform as RectTransform); }
                catch (System.Exception e) { Plugin.Log.LogWarning("속박 칸 배치 조정 실패: " + e.Message); }
            }
        }

        static void Fit(RectTransform root)
        {
            if (root == null) return;
            var grid = root.GetComponent<GridLayoutGroup>();

            // 이전에 바꾼 값을 되돌린 뒤 다시 잰다 (설정 변경 등으로 목록을 다시 채우는 경우)
            Saved s;
            if (saved.TryGetValue(root.GetInstanceID(), out s))
            {
                root.localScale = s.scale;
                root.position = s.position;
                if (grid != null) { grid.constraint = s.constraint; grid.constraintCount = s.constraintCount; grid.cellSize = s.cellSize; grid.spacing = s.spacing; }
            }
            else
            {
                s = new Saved { scale = root.localScale, position = root.position };
                if (grid != null) { s.constraint = grid.constraint; s.constraintCount = grid.constraintCount; s.cellSize = grid.cellSize; s.spacing = grid.spacing; }
                saved[root.GetInstanceID()] = s;
            }

            var icons = new List<RectTransform>();
            foreach (Transform c in root)
                if (c.gameObject.activeSelf && c is RectTransform) icons.Add((RectTransform)c);
            const int capacity = 9;
            if (icons.Count <= capacity) return;
            if (grid == null)
            {
                Plugin.Log.LogWarning("속박 칸에 GridLayoutGroup이 없어 배치를 맞추지 못함");
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            // 원래 3×3 영역 = 처음 9칸이 차지한 화면 영역
            Rect area = WorldBounds(icons, capacity);
            int cols0 = CountColumns(icons);
            int rows0 = Mathf.CeilToInt(capacity / (float)cols0);

            // 열을 늘려 가며 가장 크게 들어가는 배치를 고른다
            int bestCols = cols0; float best = 0f;
            for (int c = cols0; c <= cols0 + 3; c++)
            {
                int rows = Mathf.CeilToInt(icons.Count / (float)c);
                float k = Mathf.Min(cols0 / (float)c, rows0 / (float)rows);
                if (k > best + 0.001f) { best = k; bestCols = c; }
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = bestCols;
            root.localScale = s.scale * best;
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            // 줄어든 칸 묶음을 원래 영역의 위쪽 가운데에 맞춘다
            Rect now = WorldBounds(icons, icons.Count);
            root.position += new Vector3(area.center.x - now.center.x, area.yMax - now.yMax, 0f);
            Plugin.Log.LogInfo("속박 칸 배치 조정: " + icons.Count + "개, " + cols0 + "열 → " + bestCols + "열, 크기 ×" + best.ToString("0.00"));
        }

        static int CountColumns(List<RectTransform> icons)
        {
            // 첫 줄(첫 아이콘과 높이가 같은 아이콘) 개수
            float y = icons[0].position.y;
            int n = 0;
            foreach (var r in icons) if (Mathf.Abs(r.position.y - y) < 0.01f * Mathf.Max(1f, icons[0].rect.height * icons[0].lossyScale.y)) n++;
            return Mathf.Max(1, n);
        }

        static Rect WorldBounds(List<RectTransform> icons, int count)
        {
            var corners = new Vector3[4];
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < count && i < icons.Count; i++)
            {
                icons[i].GetWorldCorners(corners);
                foreach (var c in corners)
                {
                    x0 = Mathf.Min(x0, c.x); y0 = Mathf.Min(y0, c.y);
                    x1 = Mathf.Max(x1, c.x); y1 = Mathf.Max(y1, c.y);
                }
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        // ───── 설명글 ─────
        static TMP_Text descText;
        static bool descSaved, descAuto;
        static float descMin, descMax;

        public static void OnShowDescription(UI_TrinketPicker picker, Trinket t)
        {
            TMP_Text text = picker != null ? picker.descriptionText : null;
            if (text == null) return;
            if (descText != text) { descText = text; descSaved = false; }
            if (!descSaved)
            {
                descSaved = true;
                descAuto = text.enableAutoSizing; descMin = text.fontSizeMin; descMax = text.fontSizeMax;
                if (!descAuto) descMax = text.fontSize;
            }
            bool ours = t != null && (t.name == BindingFactory.BindingName || t.name == BindingFactory.BioTrinketName);
            if (ours)
            {
                // 칸 안에 다 들어가도록 글자를 원래 크기에서 최대 70%까지 줄인다
                text.fontSizeMax = descMax;
                text.fontSizeMin = descMax * 0.7f;
                text.enableAutoSizing = true;
            }
            else if (text.enableAutoSizing != descAuto || text.fontSizeMin != descMin)
            {
                text.enableAutoSizing = descAuto;
                text.fontSizeMin = descMin;
                text.fontSizeMax = descMax;
                if (!descAuto) text.fontSize = descMax;
            }
        }
    }
}

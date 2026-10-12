using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WKBioExperiment
{
    // 장신구를 낀 플레이어의 배고픔을 지켜보며 회색 화면 연출, 추가 굶주림 피해, 테스트 키를 처리한다.
    // 기동성 버프의 켜고 끄기는 배고픔 모듈의 buffCurve가 직접 처리한다.
    internal class BioHungerController : MonoBehaviour
    {
        PostProcessVolume volume;
        ColorGrading grading;
        Vignette vignette;
        Image overlay;
        float starveTimer;
        bool ppFailed;

        void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            BioTrinketFactory.EnsureRegistered();
            DestroyVolume();
            ppFailed = false;
        }

        void Update()
        {
            PerkModule_HungerMeter hunger = HungerRecovery.Find();
            ENT_Player player = hunger != null ? ENT_Player.GetPlayer() : null;
            float gray = 0f;

            if (hunger != null && player != null && !player.dead)
            {
                float ratio = hunger.hungerMax > 0f ? Mathf.Clamp01(hunger.hungerMeter / hunger.hungerMax) : 1f;

                if (Plugin.DebugKeys.Value)
                {
                    if (KeyDown(KeyCode.F8)) hunger.hungerMeter = 0f;
                    if (KeyDown(KeyCode.F9)) hunger.hungerMeter = hunger.hungerMax;
                }

                float start = Mathf.Clamp(Plugin.GrayStart.Value, 0.01f, 1f);
                if (ratio < start) gray = 1f - ratio / start;

                if (ratio <= 0f && Plugin.StarveDamage.Value > 0f && !CL_GameManager.gMan.isPaused)
                {
                    starveTimer += Time.deltaTime;
                    if (starveTimer >= 1f)
                    {
                        starveTimer = 0f;
                        player.Damage(Damageable.DamageInfo.CreateDamageInfo(Plugin.StarveDamage.Value, "hunger"));
                    }
                }
                else starveTimer = 0f;
            }

            ApplyGray(gray);
        }

        void ApplyGray(float t)
        {
            string mode = Plugin.GrayMode.Value;
            bool usePP = mode == "PostProcess" || (mode == "Auto" && !ppFailed);
            bool useOverlay = mode == "Overlay" || (mode == "Auto" && ppFailed);
            if (mode == "Off") { usePP = false; useOverlay = false; }

            if (usePP)
            {
                if (t > 0f && volume == null) CreateVolume();
                if (volume != null)
                {
                    volume.weight = 1f;
                    grading.saturation.value = -100f * t;
                    vignette.intensity.value = 0.45f * t;
                    if (t <= 0f) DestroyVolume();
                }
            }
            else DestroyVolume();

            if (useOverlay)
            {
                if (t > 0f && overlay == null) CreateOverlay();
                if (overlay != null) overlay.color = new Color(0.32f, 0.32f, 0.32f, 0.6f * t);
            }
            else if (overlay != null) overlay.color = Color.clear;
        }

        void CreateVolume()
        {
            try
            {
                PostProcessLayer layer = null;
                Camera cam = Camera.main;
                if (cam != null) layer = cam.GetComponent<PostProcessLayer>();
                if (layer == null) layer = FindAnyObjectByType<PostProcessLayer>();
                if (layer == null || !layer.enabled) { ppFailed = true; return; }

                int mask = layer.volumeLayer.value;
                int index = 0;
                while (index < 32 && (mask & (1 << index)) == 0) index++;
                if (index >= 32) { ppFailed = true; return; }

                grading = ScriptableObject.CreateInstance<ColorGrading>();
                grading.enabled.Override(true);
                grading.saturation.Override(0f);
                vignette = ScriptableObject.CreateInstance<Vignette>();
                vignette.enabled.Override(true);
                vignette.intensity.Override(0f);
                volume = PostProcessManager.instance.QuickVolume(index, 100f, grading, vignette);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("포스트프로세싱 회색 효과 실패, 오버레이로 대체: " + e.Message);
                ppFailed = true;
                DestroyVolume();
            }
        }

        void DestroyVolume()
        {
            if (volume != null) RuntimeUtilities.DestroyVolume(volume, true, true);
            volume = null;
            grading = null;
            vignette = null;
        }

        void CreateOverlay()
        {
            var go = new GameObject("WKBio_GrayOverlay", typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var img = new GameObject("Gray", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            overlay = img.GetComponent<Image>();
            overlay.raycastTarget = false;
            overlay.color = Color.clear;
        }

        static bool KeyDown(KeyCode key)
        {
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (key == KeyCode.F8) return kb.f8Key.wasPressedThisFrame;
                    if (key == KeyCode.F9) return kb.f9Key.wasPressedThisFrame;
                }
            }
            catch (Exception) { }
            try { return Input.GetKeyDown(key); } catch (Exception) { return false; }
        }
    }
}

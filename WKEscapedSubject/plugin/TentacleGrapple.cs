using System;
using HarmonyLib;
using UnityEngine;

namespace WKEscapedSubject
{
    // 바나클 훅(HandItem_GrapplingHook)의 동작을 옮겨 온 촉수 사출.
    // 원래 바나클 훅 프리팹에서 사거리·힘·소리·혀 메시를 가져와 그대로 쓰고, 혀 메시는 검은 촉수색으로 칠한다.
    internal class TentacleGrapple
    {
        // 원래 바나클 훅 값 (프리팹을 못 찾으면 HandItem_GrapplingHook의 기본값)
        float distance = 20f, force = 1f, deadzone = 3f, forceTime = 5f;
        float whipForce = 5f, whipUp = 2f, whipTorque = 20f;
        Vector3 forceMult = Vector3.one;
        AnimationCurve forceCurve = AnimationCurve.Linear(1f, 1f, 1f, 1f);
        LayerMask mask = ~0;
        string[] foodTags = new string[0], lesserFoodTags = new string[0], blacklistTags = new string[0];

        GameObject kit;                 // 바나클 훅 손 프리팹 사본 (소리 + 혀 메시만 남김)
        UT_AudioClipHandler audio;
        Transform tentacle;             // 늘어나는 혀 메시
        MeshRenderer tentacleRenderer;
        Material tentacleMat;

        public bool Primed, Latched;
        public int HandId = -1;
        Vector3 point;
        float grappleTime;
        float extend;                   // 0~1, 촉수가 목표까지 뻗어 나간 정도
        bool retracting;

        public Action OnLatch;          // 벽에 걸려 횟수가 줄어드는 순간
        public Action OnRelease;        // 걸린 상태에서 놓은 순간
        public Action<int> OnShotEnd;   // 한 번의 사출이 끝난 순간 (놓기·점프·물체 끌어오기). 인자 = 손 번호
        // 손바닥 위치: (손 번호, 카메라에서의 깊이) → 그 깊이에서 화면상 손바닥과 겹치는 월드 좌표. 못 구하면 null
        public Func<int, float, Vector3?> PalmPoint;

        public void Load()
        {
            if (kit != null) return;
            try
            {
                GameObject item = CL_AssetManager.GetAssetGameObject("Item_BarnacleHook");
                Item_Object io = item != null ? item.GetComponent<Item_Object>() : null;
                var src = io != null && io.itemData != null ? io.itemData.handItemAsset as HandItem_GrapplingHook : null;
                if (src == null) { Plugin.Log.LogWarning("바나클 훅 프리팹을 찾지 못해 기본값 사용"); return; }

                distance = Plugin.TentacleRange.Value; // 원래 바나클 훅 사거리(20m) 대신 촉수 사거리
                force = src.grappleForce * Plugin.PullForce.Value; deadzone = src.grappleDeadzone;
                forceTime = src.grappleForceTime; forceMult = src.grappleForceMult;
                forceCurve = src.grappleForceCurve; mask = src.grappleMask;
                whipForce = src.rigidbodyWhipForce; whipUp = src.rigidbodyVerticalWhipForce; whipTorque = src.rigidbodyWhipRandomTorque;
                foodTags = src.foodTags ?? foodTags; lesserFoodTags = src.lesserFoodTags ?? lesserFoodTags; blacklistTags = src.foodBlacklistTags ?? blacklistTags;

                // 비활성 부모 아래에 복제해서 Awake/Start가 돌기 전에 손 아이템 동작을 모두 지운다
                var holder = new GameObject("WKEscaped_TentacleKitHolder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                kit = UnityEngine.Object.Instantiate(src.gameObject, holder.transform);
                var clone = kit.GetComponent<HandItem_GrapplingHook>();
                tentacleRenderer = clone.hookMesh;
                foreach (var mb in kit.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(mb is UT_AudioClipHandler)) UnityEngine.Object.DestroyImmediate(mb);
                foreach (var an in kit.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(an);
                foreach (var r in kit.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                audio = kit.GetComponent<UT_AudioClipHandler>();
                var anySource = kit.GetComponentInChildren<AudioSource>(true);
                if (anySource != null && Sounds.Mixer == null) Sounds.Mixer = anySource.outputAudioMixerGroup;

                kit.transform.SetParent(null, false);
                UnityEngine.Object.DontDestroyOnLoad(kit);
                UnityEngine.Object.Destroy(holder);
                if (tentacleRenderer != null)
                {
                    tentacle = tentacleRenderer.transform;
                    tentacle.SetParent(kit.transform, true);
                    tentacleMat = tentacleRenderer.material;
                    // 손 변이 촉수와 같은 검은색 (#0A080E 계열)
                    Color ink = new Color(0.06f, 0.045f, 0.08f, 1f);
                    foreach (string prop in new[] { "_Color", "_BaseColor", "_Tint", "_MainColor" })
                        if (tentacleMat.HasProperty(prop)) tentacleMat.SetColor(prop, ink);
                }
                kit.SetActive(true);
                Plugin.Log.LogInfo(string.Format("바나클 훅 값 로드: 사거리 {0}, 당기는 힘 {1}, 촉수 메시 {2}",
                    distance, force, tentacleRenderer != null));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("바나클 훅 로드 실패, 기본값 사용: " + e.Message);
            }
        }

        void Sound(string n) { try { if (audio != null) audio.PlaySound(n); } catch (Exception) { } }
        void Sound(string n, Vector3 p) { try { if (audio != null) audio.PlaySound(n, p); } catch (Exception) { } }

        // 촉수가 나오는 곳: 쏘는 손의 손바닥 (화면상 손바닥 위치를 깊이 depth에서 월드 좌표로)
        Vector3 HandOrigin(Camera cam, float depth = 0.35f)
        {
            Vector3? palm = PalmPoint != null ? PalmPoint(HandId, depth) : null;
            if (palm.HasValue) return palm.Value;
            float side = HandId == 0 ? -0.25f : 0.25f;
            return cam.transform.position + cam.transform.rotation * new Vector3(side, -0.22f, depth);
        }

        // 손 버튼을 누르고 있는 동안 매 프레임 호출 (원래 Primed 상태)
        public void Prime(int handId)
        {
            if (Latched || Time.time < readyTime) return;
            if (!Primed)
            {
                Primed = true;
                HandId = handId;
                Sound("grapple:prime");
            }
            Camera cam = Camera.main;
            if (cam == null) return;
            RaycastHit hit;
            if (!Physics.SphereCast(cam.transform.position, 0.1f, cam.transform.forward, out hit, distance, mask)) return;

            GameObject go = hit.collider.gameObject;
            // 손 그림에 가려지지 않도록 손바닥과 겹치는 화면 위치의 조금 안쪽(0.7m)에서 터뜨린다
            Vector3 burstAt = HandOrigin(cam, 0.7f);
            TentacleFX.Burst(burstAt, hit.point - burstAt);
            impactNormal = hit.normal;
            pendingImpact = true;
            // 음식·각성제: 끌어온다 (횟수 차감 없음)
            Item_Object item = go.GetComponent<Item_Object>();
            ObjectTagger tagger = go.GetComponent<ObjectTagger>();
            bool blacklisted = tagger != null && tagger.HasTagInList(blacklistTags);
            bool inLocker = item != null && item.itemData != null && item.itemData.HasTag("inLocker");
            if (!inLocker && !blacklisted)
            {
                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.AddForce((HandOrigin(cam) - hit.point).normalized * whipForce + Vector3.up * whipUp);
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * whipTorque);
                    bool food = (item != null && item.itemData != null && (item.itemData.HasTag("food") || item.itemData.HasTag("staminabooster")))
                                || (tagger != null && (tagger.HasTagInList(foodTags) || tagger.HasTagInList(lesserFoodTags)));
                    impactScale = 0.5f;
                    Snap(hit.point, food);
                    return;
                }
                CL_Button button = go.GetComponent<CL_Button>();
                if (button != null && button.projectilesCanPress)
                {
                    button.Interact();
                    impactScale = 0.5f;
                    Snap(hit.point, false);
                    return;
                }
            }

            // 벽에 걸림 → 횟수 1 차감
            impactScale = 1f;
            Latched = true;
            point = hit.point;
            grappleTime = 0f;
            extend = 0f;
            retracting = false;
            Sound("grapple:latch");
            Sound("grapple:latchhit", hit.point);
            OnLatch?.Invoke();
        }

        // 물체·음식·버튼을 건드리고 바로 돌아오는 촉수 (원래 Yoink)
        void Snap(Vector3 p, bool food)
        {
            if (food) Sound("grapple:eat");
            Sound("grapple:latch");
            Sound("grapple:latchhit", p);
            point = p;
            extend = 0f;
            retracting = true;
            snappedBack = false;
            Primed = false;
            readyTime = Time.time + 0.5f;
            OnShotEnd?.Invoke(HandId);
        }

        // 손 버튼을 뗐을 때
        public void StopPrime(bool lastCharge = false)
        {
            if (Latched) LetGo(lastCharge);
            Primed = false;
        }

        // 촉수가 박힌 곳 가까이에서 맨벽 잡기 (게임의 '어디든 잡기' 스위치. 굼벵이 효과가 쓰는 것과 같은 동작)
        static readonly AccessTools.FieldRef<ENT_Player, bool> ForceGrabAnywhere = AccessTools.FieldRefAccess<ENT_Player, bool>("forceGrabAnywhere");
        ENT_Player grabPlayer; // 스위치를 켠 플레이어 (우리가 켠 것만 끈다)
        int grabHand = -1;     // 박힌 지점에서 맨벽을 잡은 손 (촉수가 사라진 뒤에도 그 손이 놓을 때까지 맨벽 잡기 유지)

        // 지금 '어디든 잡기'가 우리 때문에 켜져 있고, 잡을 수 있는 곳이 박힌 지점 근처로 제한되는지
        public static TentacleGrapple Active;
        public Vector3 AnchorPoint { get { return point; } }
        public bool WallGrabOn { get { return grabPlayer != null; } }
        // 맨벽을 잡을 수 있는 손: 촉수를 쏜 손만 (촉수가 사라진 뒤에는 박힌 지점을 잡고 있는 그 손)
        public int WallGrabHand { get { return grabHand >= 0 ? grabHand : HandId; } }

        void SetWallGrab(ENT_Player p, bool on)
        {
            try
            {
                if (on)
                {
                    if (grabPlayer != null || p == null || ForceGrabAnywhere(p)) return;
                    ForceGrabAnywhere(p) = true;
                    grabPlayer = p;
                    Active = this;
                }
                else if (grabPlayer != null)
                {
                    if (grabPlayer) ForceGrabAnywhere(grabPlayer) = false;
                    grabPlayer = null;
                    grabHand = -1;
                }
            }
            catch (Exception) { grabPlayer = null; grabHand = -1; }
        }

        public void LetGo(bool lastCharge)
        {
            if (!Latched) return;
            Latched = false;
            Primed = false;
            retracting = true;
            snappedBack = true; // 이미 뻗어 있으므로 바로 회수
            readyTime = Time.time + 0.5f;
            Sound(lastCharge ? "grapple:hungry" : "grapple:letgo");
            if (grabHand < 0) SetWallGrab(null, false); // 맨벽을 잡고 있지 않으면 바로 끔 (잡고 있으면 손을 놓을 때 끔)
            OnShotEnd?.Invoke(HandId);
            OnRelease?.Invoke();
        }

        // 매 프레임 (걸린 동안 플레이어를 당김)
        public void Update(ENT_Player player, bool lastCharge)
        {
            if (!Latched || player == null) return;
            ENT_Player.Hand hand = HandId >= 0 && HandId < player.hands.Length ? player.hands[HandId] : null;
            // 버튼을 누르고 있는 동안에는 점프하거나 다른 곳을 잡아도 촉수가 사라지지 않는다
            Camera cam = Camera.main;
            // 박힌 지점 가까이 오면 맨벽도 잡을 수 있게 (잡을 수 있는 곳은 박힌 지점 근처뿐: Patch_GrabAnywhere)
            if (cam != null && Vector3.Distance(cam.transform.position, point) <= Plugin.WallGrabDistance.Value) SetWallGrab(player, true);
            if (hand != null && hand.IsHolding())
            {
                // 박힌 지점을 손으로 잡으면 촉수는 사라지고 손으로 매달린다 (맨벽 잡기는 손을 놓을 때까지 유지)
                if (Vector3.Distance(hand.GetHoldWorldPosition(), point) <= Plugin.AnchorGrabRadius.Value + 0.4f)
                {
                    if (grabPlayer != null) grabHand = HandId;
                    LetGo(lastCharge);
                }
                return; // 다른 곳을 잡고 있는 동안은 당기지 않고 촉수만 유지
            }
            Vector3 origin = cam != null ? HandOrigin(cam) : player.transform.position;
            Vector3 v = point - origin;
            float mag = v.magnitude;
            if (mag < deadzone) v *= mag / deadzone;
            grappleTime += Time.deltaTime;
            float k = forceCurve.Evaluate(Mathf.Clamp01(grappleTime / forceTime));
            Vector3 a = v;
            if (Vector3.ProjectOnPlane(v, Vector3.up).magnitude > Mathf.Abs(a.y))
            {
                a.y *= 0.3f; a.x *= 0.75f; a.z *= 0.75f;
            }
            a = Vector3.Scale(a, forceMult);
            player.AddForce(Vector3.ClampMagnitude(a, force) * k * Time.deltaTime, "");
        }

        // 촉수 메시 연출: 뻗기(0.12초) → 걸린 동안 팽팽 → 회수(0.2초)
        public void LateUpdate()
        {
            if (tentacle == null) return;
            Camera cam = Camera.main;
            bool show = cam != null && (Latched || retracting);
            tentacleRenderer.enabled = show;
            if (!show) return;

            if (Latched) extend = Mathf.MoveTowards(extend, 1f, Time.deltaTime / 0.12f);
            else if (retracting)
            {
                // Snap은 먼저 뻗었다가 돌아온다
                extend = extend < 1f && !snappedBack ? Mathf.MoveTowards(extend, 1f, Time.deltaTime / 0.08f) : Mathf.MoveTowards(extend, 0f, Time.deltaTime / 0.2f);
                if (extend >= 1f) snappedBack = true;
                if (snappedBack && extend <= 0f) { retracting = false; snappedBack = false; tentacleRenderer.enabled = false; return; }
            }
            if (pendingImpact && extend >= 1f)
            {
                pendingImpact = false;
                TentacleFX.Splat(point, impactNormal, impactScale);
            }
            Vector3 origin = HandOrigin(cam);
            tentacle.position = origin;
            Vector3 dir = point - origin;
            if (dir.sqrMagnitude > 0.0001f) tentacle.rotation = Quaternion.LookRotation(dir);
            tentacle.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, dir.magnitude * extend));
            if (tentacleMat != null && tentacleMat.HasProperty("_Wiggle"))
                tentacleMat.SetFloat("_Wiggle", Latched ? 0.15f + 0.1f * Mathf.Sin(Time.time * 9f) : 1f);
        }

        bool snappedBack;
        float readyTime;
        bool pendingImpact;
        Vector3 impactNormal = Vector3.up;
        float impactScale = 1f;

        public void Reset()
        {
            Latched = false; Primed = false; retracting = false; snappedBack = false; extend = 0f; pendingImpact = false;
            if (grabHand < 0) SetWallGrab(null, false);
        }

        // 매 프레임: 박힌 지점에서 맨벽을 잡은 손이 놓으면 맨벽 잡기를 끈다
        public void Tick(ENT_Player player)
        {
            if (grabHand < 0 || Latched) return;
            ENT_Player.Hand h = player != null && grabHand < player.hands.Length ? player.hands[grabHand] : null;
            if (h == null || !h.IsHolding()) SetWallGrab(null, false);
            if (tentacleRenderer != null) tentacleRenderer.enabled = false;
        }
    }
}

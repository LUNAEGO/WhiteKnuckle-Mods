using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WKEscapedSubject
{
    // 속박의 실시간 규칙: 점착성 표면, 변이 충전·진행, 촉수 상태(사출·매달림 회복), 우측 하단 횟수 표시
    internal class MutationController : MonoBehaviour
    {
        enum State { Idle, Charged, Tentacle, Reverting }

        static MutationController instance;
        State state = State.Idle;
        float progress;          // 변이 진행 시간
        float revertTime;
        int charges;

        // 점착성 표면: 지면 5초 → 발현(대기) → 벽을 짚으면 7초 카운트
        enum Sticky { None, Armed, Counting }
        Sticky sticky = Sticky.None;
        float groundTime;
        BuffContainer stickyBuff;
        BuffContainer grubTemplate;
        bool grubLoaded;

        readonly TentacleGrapple grapple = new TentacleGrapple();
        readonly float[] holdTime = new float[2];   // 촉수 상태에서 맨손 버튼을 누르고 있는 시간
        readonly bool[] pending = new bool[2];
        readonly float[] lastGrip = new float[2];

        Image counterImage;

        float stickyNoDrainLeft;     // 벽을 짚은 뒤 스태미나가 줄지 않는 남은 시간
        Image stickyVignette;        // 점착성 표면 전용 보라색 화면 (굼벵이를 먹었을 때의 노란 화면과 별개)
        float stickyVignetteAlpha;
        ENT_Player healthAppliedTo;  // 최대 체력 -20%를 적용한 플레이어 (레벨마다 새로 만들어짐)
        static readonly Color StickyTint = new Color(0.24f, 0.04f, 0.33f, 1f);
        const string TentacleStaminaBuffId = "escapedSubject_tentacleStamina";
        readonly Sounds.Player chargeSound = new Sounds.Player("mutation_charge.wav"); // 변이 5초 동안
        readonly Sounds.Player shootSound = new Sounds.Player("tentacle_shoot.wav");   // 발사 대기 0.65초 동안
        readonly Dictionary<int, Camera> layerCamera = new Dictionary<int, Camera>();

        void Awake()
        {
            instance = this;
            grapple.OnLatch = () => { charges = Mathf.Max(0, charges - 1); };
            grapple.OnRelease = () => { if (state == State.Tentacle && charges <= 0) StartRevert(); };
            // 촉수가 끝나면(놓기·점프·물체 끌어오기) 버튼을 계속 누르고 있어도 다시 나가지 않게, 손을 뗐다가 다시 눌러야 한다
            grapple.OnShotEnd = hand => { if (hand >= 0 && hand < 2) { pending[hand] = false; holdTime[hand] = 0f; } };
            grapple.PalmPoint = PalmPoint;
        }

        void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            BindingFactory.EnsureRegistered();
            grapple.Reset();
        }

        // 새 판 시작
        public static void ResetRun()
        {
            if (instance == null) return;
            instance.state = State.Idle;
            instance.progress = 0f;
            instance.charges = 0;
            instance.groundTime = 0f;
            instance.sticky = Sticky.None;
            instance.stickyBuff = null;
            instance.stickyNoDrainLeft = 0f;
            instance.grapple.Reset();
            instance.chargeSound.Stop();
            instance.shootSound.Stop();
            instance.RemoveTentacleStamina(ENT_Player.GetPlayer());
        }

        // 굼벵이·알약·주사기·Lemon 로치 섭취 (HandItem_Buff.StartBuff)
        public static void OnConsume(HandItem_Buff item)
        {
            if (instance == null || !BindingFactory.IsActive(ENT_Player.GetPlayer())) return;
            if (!IsChargeItem(item)) return;
            if (instance.state != State.Idle) return; // 중첩 없음
            instance.state = State.Charged;
            instance.progress = 0f;
            Plugin.Log.LogInfo("변이 충전: " + item.gameObject.name);
        }

        static bool IsChargeItem(HandItem_Buff h)
        {
            if (h is HandItem_Buff_Grub) return true;
            string names = h.gameObject.name;
            if (h.item != null) names += "|" + h.item.prefabName + "|" + h.item.itemName;
            foreach (string k in Plugin.ChargeKeywords.Value.Split(','))
            {
                string key = k.Trim();
                if (key.Length > 0 && names.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        void Update()
        {
            ENT_Player player = null;
            try { player = ENT_Player.GetPlayer(); } catch (Exception) { }
            bool active = player != null && !player.dead && BindingFactory.IsActive(player);
            if (!active)
            {
                HandOverlay.SetStage(0);
                HandOverlay.Pulse = 0f;
                ResetHandMotion();
                ShowCounter(0);
                UpdateStickyVignette(0f);
                HandSkin.Set(player, false);
                if (grapple.Latched || grapple.Primed) grapple.Reset();
                chargeSound.Stop();
                shootSound.Stop();
                return;
            }
            ApplyHealth(player);
            Assets.PreloadStep();
            grapple.Tick(player);
            // 핏자국·부글거림 손(Infested): 변이를 시작한 순간부터 촉수를 다 쓸 때까지
            HandSkin.Set(player, (state == State.Charged && progress > 0f) || state == State.Tentacle || state == State.Reverting);
            bool paused = CL_GameManager.gMan != null && CL_GameManager.gMan.isPaused;

            for (int i = 0; i < player.hands.Length && i < 2; i++)
            {
                ENT_Player.Hand h = player.hands[i];
                HandOverlay.Attach(h.handSprite, i, h.handSprite);
                HandOverlay.Attach(h.handForegroundSprite, i, h.handSprite);
            }
            grapple.Load();

            if (Plugin.DebugKeys.Value)
            {
                if (KeyDown(KeyCode.F6) && state == State.Idle) state = State.Charged;
                if (KeyDown(KeyCode.F7)) EnterTentacle();
            }

            if (!paused) UpdateSticky(player);

            switch (state)
            {
                case State.Idle:
                    HandOverlay.SetStage(0); HandOverlay.Pulse = 0f;
                    ResetHandMotion();
                    break;
                case State.Charged:
                    UpdateMutating(player, paused);
                    break;
                case State.Tentacle:
                    HandOverlay.SetStage(Assets.FrameCount);
                    // 발사 준비(버튼 유지) 중에는 촉수가 점점 크게 꿈틀거림
                    float hold = Mathf.Max(holdTime[0], holdTime[1]);
                    HandOverlay.Pulse = 0.6f + 1.6f * Mathf.Clamp01(hold / Mathf.Max(0.1f, Plugin.ShootHold.Value));
                    if (!paused) UpdateTentacle(player);
                    UpdateShootMotion(player, paused);
                    break;
                case State.Reverting:
                    revertTime += Time.deltaTime;
                    // 0.5초 동안 손끝에서 손목 쪽으로 빨려 들어감
                    HandOverlay.SetStage(Mathf.Clamp(Mathf.CeilToInt((1f - revertTime / 0.5f) * Assets.FrameCount), 0, Assets.FrameCount));
                    HandOverlay.Pulse = 0f;
                    UpdateShootMotion(player, paused); // 마지막 사출의 회수 동작을 마저 끝낸다
                    if (revertTime >= 0.5f) { state = State.Idle; HandOverlay.SetStage(0); }
                    break;
            }
            ShowCounter(state == State.Tentacle ? charges : 0);
        }

        void LateUpdate()
        {
            grapple.LateUpdate();
        }

        // 맨손을 뻗고(손 버튼을 누르고 있는데 아무것도 잡지 않음) 있는 동안 5초에 걸쳐 변이. 떼면 처음부터
        void UpdateMutating(ENT_Player player, bool paused)
        {
            bool bare = true, reaching = false;
            foreach (ENT_Player.Hand h in player.hands)
            {
                if (h.GetHandItem() != null) bare = false;
                if (InputManager.GetButton(h.fireButton).Pressed && h.interactState == ENT_Player.InteractType.none) reaching = true;
            }
            if (bare && reaching && !paused && !Inventory.IsShowingInventory())
            {
                if (progress <= 0f) chargeSound.Play(gameObject);
                progress += Time.deltaTime;
            }
            else
            {
                if (progress > 0f) chargeSound.Stop();
                progress = 0f;
            }

            float total = Mathf.Max(0.5f, Plugin.MutateSeconds.Value);
            float t = progress / total;
            // 덧그림 16프레임이 5초 시간표를 그대로 담고 있다 (0~1초 손목, 1~2초 손등, 2~3.5초 손가락, 3.5~5초 손바닥 쪽으로 감김)
            HandOverlay.SetStage(progress > 0f ? Mathf.Clamp(Mathf.CeilToInt(t * Assets.FrameCount), 1, Assets.FrameCount) : 0);
            HandOverlay.Pulse = Mathf.Clamp01((t - 0.6f) / 0.4f); // 후반부터 점점 크게 꿈틀
            UpdateTremble(progress > 0f && !paused ? t * 5f : -1f);
            if (progress >= total) { EnterTentacle(); ResetHandMotion(); }
        }

        // 최대 체력 -20% (플레이어는 레벨마다 새로 만들어지므로 새 플레이어마다 한 번 적용)
        void ApplyHealth(ENT_Player player)
        {
            if (healthAppliedTo == player) return;
            healthAppliedTo = player;
            float mult = Mathf.Max(0.05f, 1f + Plugin.Health.Value);
            player.maxHealth *= mult;
            player.health = Mathf.Min(player.health, player.maxHealth);
            Plugin.Log.LogInfo("최대 체력 적용: " + player.maxHealth);
        }

        void EnterTentacle()
        {
            state = State.Tentacle;
            charges = Plugin.Charges.Value;
            progress = 0f;
            pending[0] = pending[1] = false;
            ENT_Player p = ENT_Player.GetPlayer();
            if (p != null)
                for (int i = 0; i < p.hands.Length && i < 2; i++) lastGrip[i] = p.hands[i].gripStrength;
            if (p != null) AddTentacleStamina(p);
            Plugin.Log.LogInfo("촉수 변이 완료: " + charges + "회");
        }

        void StartRevert()
        {
            state = State.Reverting;
            revertTime = 0f;
            grapple.Reset();
            RemoveTentacleStamina(ENT_Player.GetPlayer());
        }

        void UpdateTentacle(ENT_Player player)
        {
            // 촉수 사출 (안 A): 맨손 버튼을 눌렀는데 잠깐 사이에 잡을 곳을 못 잡으면 쏜다
            for (int i = 0; i < player.hands.Length && i < 2; i++)
            {
                ENT_Player.Hand h = player.hands[i];
                var btn = InputManager.GetButton(h.fireButton);
                if (btn.Down && h.GetHandItem() == null) { holdTime[i] = 0f; pending[i] = true; }
                if (!btn.Pressed)
                {
                    if (pending[i] && grapple.HandId == i) grapple.StopPrime(charges <= 0);
                    if (pending[i] && holdTime[i] < Plugin.ShootHold.Value) shootSound.Stop(); // 다 채우기 전에 뗌
                    pending[i] = false;
                    holdTime[i] = 0f;
                    continue;
                }
                if (!pending[i]) continue;
                bool ownShot = (grapple.Primed || grapple.Latched) && grapple.HandId == i;
                if (!ownShot)
                {
                    // 누르는 동안 무언가를 잡거나 물건을 들면 취소 (다시 눌러야 함)
                    if (h.GetHandItem() != null || h.interactState != ENT_Player.InteractType.none)
                    {
                        if (holdTime[i] > 0f && holdTime[i] < Plugin.ShootHold.Value) shootSound.Stop();
                        pending[i] = false; holdTime[i] = 0f; continue;
                    }
                    bool otherBusy = (grapple.Primed || grapple.Latched) && grapple.HandId != i;
                    if (otherBusy || charges <= 0) continue;
                    if (holdTime[i] <= 0f) shootSound.Play(gameObject);
                    holdTime[i] += Time.deltaTime;
                }
                if (holdTime[i] >= Plugin.ShootHold.Value) grapple.Prime(i);
            }
            grapple.Update(player, charges <= 0);
        }

        // ───── 손 모션 (시안: concept/motion) ─────

        enum Shot { None, Charging, Fired, Retract }
        readonly Shot[] shot = new Shot[2];
        readonly float[] shotTime = new float[2];
        float trembleNext;
        readonly HandPose[] trembleTarget = { HandPose.Identity, HandPose.Identity };

        void ResetHandMotion()
        {
            for (int i = 0; i < 2; i++)
            {
                shot[i] = Shot.None;
                HandOverlay.Pose[i] = HandPose.Identity;
                HandOverlay.ShootFrame[i] = 0;
            }
        }

        // 변이 5초: 양손이 엇박자로 떨리고, 시간이 갈수록 커진다. 1초·3.5초에 크게 움찔 (sec: 5초 기준 시간, 음수면 멈춤)
        void UpdateTremble(float sec)
        {
            if (sec < 0f) { HandOverlay.Pose[0] = HandOverlay.Pose[1] = HandPose.Identity; return; }
            bool jerk = (sec >= 1f && sec < 1.12f) || (sec >= 3.5f && sec < 3.62f);
            if (Time.time >= trembleNext || jerk)
            {
                trembleNext = Time.time + 0.06f;
                float amp = 0.4f + 1.8f * sec / 5f;
                for (int i = 0; i < 2; i++)
                    trembleTarget[i] = new HandPose
                    {
                        scale = jerk ? 0.95f : 1f + UnityEngine.Random.Range(-0.01f, 0.01f),
                        rot = UnityEngine.Random.Range(-1f, 1f) * (0.6f + sec * 0.5f) + (jerk ? 4f : 0f),
                        dx = UnityEngine.Random.Range(-1f, 1f) * amp,
                        dy = UnityEngine.Random.Range(-1f, 1f) * amp * 0.7f + (jerk ? 3f : 0f),
                    };
            }
            for (int i = 0; i < 2; i++)
                HandOverlay.Pose[i] = HandPose.Lerp(HandOverlay.Pose[i], trembleTarget[i], Mathf.Clamp01(Time.deltaTime * 30f));
        }

        // 촉수 사출 손 모션 (시안 9프레임)
        // 버튼 유지 0~0.75초: 1 대기 → 2 누름(0.10) → 3 꿈틀(0.35) → 4 최대 수축(0.60) → 5 발사(0.75)
        // 발사 뒤: 5(0.07초) → 6 반동(0.09초) → 7 박힘(0.18초) → 8 유지(버튼을 누르고 촉수가 있는 동안) → 9 회수(0.16초)
        static readonly float[] ChargeKeyTimes = { 0f, 0.10f, 0.35f, 0.60f, 0.75f };
        static readonly float[] FiredKeyTimes = { 0f, 0.07f, 0.16f, 0.34f };
        const float RetractSeconds = 0.16f;

        void UpdateShootMotion(ENT_Player player, bool paused)
        {
            if (paused) return;
            float shootHold = Mathf.Max(0.05f, Plugin.ShootHold.Value);
            for (int i = 0; i < player.hands.Length && i < 2; i++)
            {
                bool pressed = InputManager.GetButton(player.hands[i].fireButton).Pressed;
                bool ownShot = (grapple.Primed || grapple.Latched) && grapple.HandId == i;
                switch (shot[i])
                {
                    case Shot.None:
                        if (state == State.Tentacle && pending[i] && holdTime[i] > 0f) shot[i] = Shot.Charging;
                        break;
                    case Shot.Charging:
                        if (holdTime[i] >= shootHold) { shot[i] = Shot.Fired; shotTime[i] = 0f; }
                        else if (!pending[i] || holdTime[i] <= 0f) shot[i] = Shot.None; // 다 채우기 전에 뗌
                        break;
                    case Shot.Fired:
                        shotTime[i] += Time.deltaTime;
                        // 버튼을 떼거나 촉수가 끝나면 회수 (박힘 프레임까지는 보여 준다)
                        if (!(pressed && ownShot) && shotTime[i] >= FiredKeyTimes[2]) { shot[i] = Shot.Retract; shotTime[i] = 0f; }
                        break;
                    case Shot.Retract:
                        shotTime[i] += Time.deltaTime;
                        if (shotTime[i] >= RetractSeconds) shot[i] = Shot.None;
                        break;
                }

                int frame;
                HandPose pose;
                switch (shot[i])
                {
                    case Shot.Charging:
                    {
                        float ct = holdTime[i] / shootHold * 0.75f; // 시안의 0.75초 기준 시간
                        int k = 0;
                        while (k < ChargeKeyTimes.Length - 2 && ct >= ChargeKeyTimes[k + 1]) k++;
                        float u = Mathf.SmoothStep(0f, 1f, (ct - ChargeKeyTimes[k]) / (ChargeKeyTimes[k + 1] - ChargeKeyTimes[k]));
                        frame = k + 1;
                        pose = HandPose.Lerp(Assets.ShootKey(k + 1), Assets.ShootKey(k + 2), u);
                        // 꿈틀 구간부터 손이 미세하게 떨림
                        if (frame >= 3) { pose.dx += UnityEngine.Random.Range(-0.6f, 0.6f); pose.dy += UnityEngine.Random.Range(-0.6f, 0.6f); }
                        break;
                    }
                    case Shot.Fired:
                    {
                        float ft = shotTime[i];
                        int k = 0;
                        while (k < FiredKeyTimes.Length - 1 && ft >= FiredKeyTimes[k + 1]) k++;
                        frame = 5 + k;
                        if (k < FiredKeyTimes.Length - 1)
                        {
                            float u = Mathf.SmoothStep(0f, 1f, (ft - FiredKeyTimes[k]) / (FiredKeyTimes[k + 1] - FiredKeyTimes[k]));
                            pose = HandPose.Lerp(Assets.ShootKey(5 + k), Assets.ShootKey(6 + k), u);
                        }
                        else pose = Assets.ShootKey(8);
                        break;
                    }
                    case Shot.Retract:
                        frame = 9;
                        pose = HandPose.Lerp(Assets.ShootKey(9), HandPose.Identity, Mathf.SmoothStep(0f, 1f, shotTime[i] / RetractSeconds));
                        break;
                    default:
                        frame = 0;
                        pose = HandPose.Identity;
                        break;
                }
                HandOverlay.ShootFrame[i] = frame;
                HandOverlay.Pose[i] = pose;
            }
        }

        // 촉수 상태 동안 스태미나 +35% (사출 횟수를 다 쓰면 사라짐)
        void AddTentacleStamina(ENT_Player p)
        {
            if (p.curBuffs.HasBuffContainer(TentacleStaminaBuffId)) return;
            p.curBuffs.AddBuff(new BuffContainer
            {
                id = TentacleStaminaBuffId,
                desc = "",
                buffs = new List<BuffContainer.Buff> { new BuffContainer.Buff { id = "addStamina", maxAmount = Plugin.TentacleStamina.Value, amount = Plugin.TentacleStamina.Value } },
                loseOverTime = false,
                loseRate = 0f,
                buffTime = 1f,
                multiplier = 1f,
            });
        }

        void RemoveTentacleStamina(ENT_Player p)
        {
            if (p != null && p.curBuffs != null && p.curBuffs.HasBuffContainer(TentacleStaminaBuffId))
                p.curBuffs.RemoveBuffContainer(TentacleStaminaBuffId);
        }

        // 손 그림(손 본체 레이어)의 손바닥이 화면에 보이는 위치를, 메인 카메라에서 depth만큼 떨어진 월드 좌표로 바꾼다.
        // 손은 별도 카메라로 그려질 수 있어서 그 레이어를 그리는 카메라로 화면 좌표를 구한다
        Vector3? PalmPoint(int handId, float depth)
        {
            try
            {
                ENT_Player player = ENT_Player.GetPlayer();
                Camera main = Camera.main;
                if (player == null || main == null || handId < 0 || handId >= player.hands.Length) return null;
                SpriteRenderer sr = player.hands[handId].handSprite;
                if (sr == null || sr.sprite == null || !sr.enabled) return null;

                int layer = sr.gameObject.layer;
                Camera cam;
                if (!layerCamera.TryGetValue(layer, out cam) || cam == null)
                {
                    cam = null;
                    foreach (Camera c in Camera.allCameras)
                        if ((c.cullingMask & (1 << layer)) != 0 && (cam == null || c.depth > cam.depth)) cam = c;
                    layerCamera[layer] = cam;
                }
                if (cam == null) return null;

                // 손바닥 = 스프라이트 가운데에서 손목 쪽(스프라이트 아래)으로 조금 내려온 곳
                Bounds b = sr.bounds;
                Vector3 palm = b.center - sr.transform.up * (b.extents.y * 0.2f);
                Vector3 screen = cam.WorldToScreenPoint(palm);
                if (screen.z <= 0f) return null;
                return main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            }
            catch (Exception) { return null; }
        }

        // 점착성 표면: 지면에 5초 머무르면 굼벵이 효과 발현(시간 제한 없이 대기).
        // 발현 후 벽을 처음 짚는 순간부터 7초가 줄어들고, 끝나면 효과가 사라진다
        void UpdateSticky(ENT_Player player)
        {
            // 효과가 게임에서 제거됐으면(시간 종료 등) 처음 상태로
            if (sticky != Sticky.None && (stickyBuff == null || !player.curBuffs.currentBuffs.Contains(stickyBuff)))
            {
                sticky = Sticky.None;
                stickyBuff = null;
                groundTime = 0f;
                stickyNoDrainLeft = 0f;
            }

            if (sticky == Sticky.Armed)
            {
                UpdateStickyVignette(0.5f);
                if (player.IsHanging())
                {
                    sticky = Sticky.Counting;
                    stickyBuff.buffTime = 1f;
                    stickyBuff.loseOverTime = true; // 지금부터 7초
                    stickyNoDrainLeft = Plugin.StickyNoDrain.Value;
                    for (int i = 0; i < player.hands.Length && i < 2; i++) lastGrip[i] = player.hands[i].gripStrength;
                }
                return;
            }
            if (sticky == Sticky.Counting)
            {
                UpdateStickyVignette(0.5f * Mathf.Clamp01(stickyBuff.buffTime));
                // 벽을 짚은 뒤 5초 동안 스태미나가 줄지 않음
                if (stickyNoDrainLeft > 0f)
                {
                    stickyNoDrainLeft -= Time.deltaTime;
                    for (int i = 0; i < player.hands.Length && i < 2; i++)
                    {
                        ENT_Player.Hand h = player.hands[i];
                        if (h.gripStrength < lastGrip[i]) h.SetGripStrength(lastGrip[i]);
                        lastGrip[i] = h.gripStrength;
                    }
                }
                return;
            }
            UpdateStickyVignette(0f);

            if (player.IsGrounded(false) && !player.IsHanging()) groundTime += Time.deltaTime;
            else groundTime = 0f;
            if (groundTime < Plugin.StickyGroundSeconds.Value) return;

            groundTime = 0f;
            stickyBuff = MakeGrubBuff();
            if (stickyBuff == null) return;
            player.Buff(stickyBuff);
            // 같은 id가 이미 있으면 게임이 새 것을 넣지 않고 기존 것을 되살리므로, 실제로 들어간 것을 다시 가져온다
            stickyBuff = player.curBuffs.GetBuffContainer(stickyBuff.id) ?? stickyBuff;
            stickyBuff.loseOverTime = false;
            stickyBuff.buffTime = 1f;
            sticky = Sticky.Armed;
        }

        BuffContainer MakeGrubBuff()
        {
            if (!grubLoaded)
            {
                grubLoaded = true;
                try
                {
                    GameObject go = CL_AssetManager.GetAssetGameObject("Denizen_SlugGrub");
                    Item_Object io = go != null ? go.GetComponent<Item_Object>() : null;
                    var grub = io != null && io.itemData != null ? io.itemData.handItemAsset as HandItem_Buff : null;
                    if (grub != null && grub.buff != null && grub.buff.buffs != null)
                    {
                        grubTemplate = grub.buff;
                        var ids = new List<string>();
                        foreach (var x in grub.buff.buffs) ids.Add(x.id + "=" + x.maxAmount);
                        Plugin.Log.LogInfo("굼벵이 효과 로드: " + grub.buff.id + " [" + string.Join(", ", ids.ToArray()) + "]");
                    }
                }
                catch (Exception e) { Plugin.Log.LogWarning("굼벵이 효과 로드 실패: " + e.Message); }
                if (grubTemplate == null) Plugin.Log.LogWarning("굼벵이 효과를 찾지 못해 grabAnything으로 대체");
            }

            var c = new BuffContainer
            {
                id = "escapedSubject_sticky",
                desc = "",
                buffs = new List<BuffContainer.Buff>(),
                loseOverTime = false, // 벽을 짚기 전까지는 줄지 않음
                loseRateEffectedByPerks = false,
                loseRate = 1f / Mathf.Max(0.1f, Plugin.StickyDuration.Value),
            };
            if (grubTemplate != null)
                foreach (var x in grubTemplate.buffs)
                {
                    if (x.id == "gooped_screen") continue; // 노란 화면 대신 보라색 화면을 직접 그린다
                    c.buffs.Add(new BuffContainer.Buff { id = x.id, maxAmount = x.maxAmount, amount = x.maxAmount });
                }
            else
                c.buffs.Add(new BuffContainer.Buff { id = "grabAnything", maxAmount = 1f, amount = 1f });
            c.Initialize();
            return c;
        }

        // 굼벵이의 노란 화면(goop 비네트)과 같은 그림을 복제해서 보라색으로 칠한다
        void UpdateStickyVignette(float target)
        {
            stickyVignetteAlpha = Mathf.Lerp(stickyVignetteAlpha, target, Time.unscaledDeltaTime * 3f);
            if (stickyVignette == null)
            {
                if (stickyVignetteAlpha < 0.01f) return;
                try
                {
                    CL_UIManager ui = CL_UIManager.instance;
                    if (ui == null || ui.vignettes == null) return;
                    CL_UIManager.Vignette goop = ui.vignettes.Find(v => v.name == "goop");
                    if (goop == null || goop.image == null) return;
                    var go = Instantiate(goop.image.gameObject, goop.image.transform.parent);
                    go.name = "WKEscaped_StickyVignette";
                    go.transform.SetSiblingIndex(goop.image.transform.GetSiblingIndex() + 1);
                    stickyVignette = go.GetComponent<Image>();
                    Plugin.Log.LogInfo("보라색 화면 생성 (원본 goop 색: " + goop.color + ")");
                }
                catch (Exception e) { Plugin.Log.LogWarning("보라색 화면 생성 실패: " + e.Message); return; }
            }
            var c = StickyTint;
            c.a = stickyVignetteAlpha;
            stickyVignette.color = c;
            stickyVignette.enabled = stickyVignetteAlpha > 0.005f;
        }

        void ShowCounter(int remaining)
        {
            if (remaining <= 0)
            {
                if (counterImage != null) counterImage.enabled = false;
                return;
            }
            if (counterImage == null)
            {
                var go = new GameObject("WKEscaped_BarnacleCounter", typeof(Canvas));
                DontDestroyOnLoad(go);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 29000;
                var img = new GameObject("Counter", typeof(RectTransform), typeof(Image));
                img.transform.SetParent(go.transform, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-28f, 28f);
                counterImage = img.GetComponent<Image>();
                counterImage.raycastTarget = false;
            }
            Sprite sp = Assets.GetCounter(remaining);
            counterImage.enabled = sp != null;
            if (sp == null) return;
            counterImage.sprite = sp;
            ((RectTransform)counterImage.transform).sizeDelta = new Vector2(sp.rect.width, sp.rect.height) * Plugin.CounterScale.Value;
        }

        static bool KeyDown(KeyCode key)
        {
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (key == KeyCode.F6) return kb.f6Key.wasPressedThisFrame;
                    if (key == KeyCode.F7) return kb.f7Key.wasPressedThisFrame;
                }
            }
            catch (Exception) { }
            try { return Input.GetKeyDown(key); } catch (Exception) { return false; }
        }
    }
}

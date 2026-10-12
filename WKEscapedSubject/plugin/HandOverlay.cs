using UnityEngine;

namespace WKEscapedSubject
{
    // 손 전체의 움직임 (손 그림 기준: 크기, 화면 시계방향 회전(도), 오른쪽·아래 이동(px)). 손목을 중심으로 적용
    internal struct HandPose
    {
        public float scale, rot, dx, dy;
        public static readonly HandPose Identity = new HandPose { scale = 1f };
        public bool IsIdentity { get { return scale == 1f && rot == 0f && dx == 0f && dy == 0f; } }

        public static HandPose Lerp(HandPose a, HandPose b, float t)
        {
            return new HandPose
            {
                scale = Mathf.Lerp(a.scale, b.scale, t), rot = Mathf.Lerp(a.rot, b.rot, t),
                dx = Mathf.Lerp(a.dx, b.dx, t), dy = Mathf.Lerp(a.dy, b.dy, t),
            };
        }
    }

    // 손 스프라이트(본체/앞 손가락 레이어) 위에 촉수 덧그림을 한 겹 더 그리고, 손 전체를 떨림·반동으로 움직인다.
    // 게임이 손 포즈를 바꿀 때마다 같은 이름의 덧그림을 찾아 따라간다.
    // 사출 프레임 중 손가락을 굽히는 포즈(full)는 원래 손을 잠깐 숨기고 손 전체 그림으로 바꿔 그린다
    internal class HandOverlay : MonoBehaviour
    {
        public static readonly int[] Stage = new int[2];       // 손별 변이 프레임 (0 = 없음, 1~Assets.FrameCount)
        public static readonly int[] ShootFrame = new int[2];  // 손별 사출 프레임 (0·1 = 없음, 2~9)
        public static readonly HandPose[] Pose = { HandPose.Identity, HandPose.Identity };
        public static float Pulse;      // 0~1, 꿈틀거림(밝기 흔들림) 세기

        public static void SetStage(int s) { Stage[0] = Stage[1] = s; }

        SpriteRenderer target, bodySprite; // bodySprite: 이 손의 본체 레이어 (손목 위치 기준)
        SpriteRenderer overlay;
        Material overlayMat;
        int hand;
        Vector3 restPos, restScale;
        Quaternion restRot;
        bool moves;    // 이 레이어를 직접 움직이는지 (본체의 자식이면 본체를 따라가므로 움직이지 않음)
        bool hidBase;

        public static void Attach(SpriteRenderer target, int hand, SpriteRenderer body)
        {
            if (target == null || target.GetComponentInChildren<HandOverlay>(true) != null) return;
            var go = new GameObject("WKEscaped_TentacleOverlay");
            go.layer = target.gameObject.layer;
            go.transform.SetParent(target.transform, false);
            var o = go.AddComponent<HandOverlay>();
            o.target = target;
            o.bodySprite = body != null ? body : target;
            o.hand = Mathf.Clamp(hand, 0, 1);
            o.overlay = go.AddComponent<SpriteRenderer>();
            o.overlayMat = o.overlay.sharedMaterial;
            Transform t = target.transform;
            o.restPos = t.localPosition; o.restRot = t.localRotation; o.restScale = t.localScale;
            o.moves = target == o.bodySprite || !t.IsChildOf(o.bodySprite.transform);
        }

        void OnDestroy() { if (target != null && hidBase) target.forceRenderingOff = false; }

        void LateUpdate()
        {
            if (target == null) { Destroy(gameObject); return; }
            ApplyPose();

            bool visible = target.enabled && target.gameObject.activeInHierarchy;
            Sprite sp = null;
            bool full = false;
            if (visible && ShootFrame[hand] >= 2) sp = Assets.GetShoot(ShootFrame[hand], target.sprite, out full);
            if (visible && sp == null && Stage[hand] > 0) { sp = Assets.GetOverlay(Stage[hand], target.sprite); full = false; }

            // 손가락을 굽힌 손 전체 그림을 쓰는 동안은 원래 손을 숨긴다
            bool hide = sp != null && full;
            if (hide != hidBase) { target.forceRenderingOff = hide; hidBase = hide; }

            overlay.enabled = sp != null;
            if (sp == null) return;
            overlay.sprite = sp;
            overlay.flipX = target.flipX;
            overlay.flipY = target.flipY;
            overlay.sortingLayerID = target.sortingLayerID;
            overlay.sortingOrder = target.sortingOrder + 1;
            overlay.color = new Color(1f, 1f, 1f, target.color.a);
            // 손 전체 그림은 원래 손과 같은 재질로 그려 색감(부글거림·스태미나 표시)을 맞춘다
            overlay.sharedMaterial = full && target.sharedMaterial != null ? target.sharedMaterial : overlayMat;
            // 꿈틀거림: 덧그림만 아주 조금 부풀었다 줄었다
            float k = full ? 1f : 1f + Pulse * 0.025f * Mathf.Sin(Time.time * 16f);
            transform.localScale = new Vector3(k, 1f / k, 1f);
        }

        // 손목을 중심으로 크기·회전·이동. 게임은 손 그림 오브젝트 자체의 위치를 건드리지 않으므로 처음 위치를 기준으로 덮어쓴다
        void ApplyPose()
        {
            if (!moves) return;
            Transform t = target.transform;
            HandPose p = Pose[hand];
            Sprite body = bodySprite != null ? bodySprite.sprite : null;
            if (p.IsIdentity || body == null)
            {
                t.localPosition = restPos; t.localRotation = restRot; t.localScale = restScale;
                return;
            }
            Rect r = body.rect;
            float ppu = body.pixelsPerUnit, px = r.width / 128f; // 손 그림 기준 128px
            Vector2 wrist = Assets.GetWrist(body.name, r.width, r.height);
            float flip = bodySprite.flipX ? -1f : 1f;
            var w = new Vector3((wrist.x - body.pivot.x) / ppu * flip, ((r.height - wrist.y) - body.pivot.y) / ppu, 0f);
            Quaternion rot = Quaternion.Euler(0f, 0f, -p.rot * flip);
            Vector3 off = w - rot * (w * p.scale) + new Vector3(p.dx * px * flip / ppu, -p.dy * px / ppu, 0f);
            t.localPosition = restPos + restRot * Vector3.Scale(restScale, off);
            t.localRotation = restRot * rot;
            t.localScale = restScale * p.scale;
        }
    }
}

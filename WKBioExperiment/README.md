# 생체 실험된 (Biologically Experimented) - White Knuckle 장신구 모드

BepInEx 5 플러그인으로, 장신구 선택 화면에 새 장신구 "생체 실험된"을 추가합니다.

## 효과
- 스태미나 +30%, 오르기 속도 +15%, 추가 점프 +1, 중력 -20%, 파우치 +2
- 점수 -35%
- 배고픔이 4분 30초에 걸쳐 0%까지 줄어듦 (게임의 기존 배고픔 미터 사용)
- 회복: 음식 50%, 알약·주사기(Pillbottle, Injector, Inoculator) 100%
- 배고픔 50% 이하부터 화면이 점점 회색으로 변함
- 배고픔 15% 이하: 기동성 강화(스태미나, 오르기, 점프, 중력)가 꺼짐. 15%를 넘게 회복하면 다시 켜짐. 파우치는 유지
- 배고픔 0%: 체력이 닳아 방치하면 사망
- 아이콘은 `Icon\Biologically Experimented.png`를 빌드 시 플러그인 폴더로 복사해 사용
  (원본 `..\Image\Biologically Experimented(1).png`에서 흰 배경을 지운 것. 변환 스크립트: `Icon\RemoveWhiteBg.cs`)

## 빌드와 설치
```
dotnet build -c Release
```
빌드하면 `BepInEx\plugins\WKBioExperiment\WKBioExperiment.dll`로 자동 복사됩니다.
게임 경로가 다르면 `dotnet build -c Release -p:GameDir="게임 폴더"`로 지정하세요.

## 설정
게임을 한 번 실행하면 `BepInEx\config\LUNAEGO.BiologicallyExperimented.cfg`가 생깁니다.
모든 수치와 배고픔 속도, 회색 효과 방식(Auto/PostProcess/Overlay/Off), 테스트 키(F8 배고픔 0%, F9 가득)를 바꿀 수 있습니다.
비용·수치를 바꾼 뒤에는 게임을 재시작해야 적용됩니다.

## 참고
- v1.1.0부터 이 장신구를 낀 판은 리더보드에 제출하지 않습니다 (`LeaderboardBlock.cs`). 차단 전 버전은 `archive/`에 보관.
- 기본 언어는 영어, 설정 `Language = Korean`으로 한국어.
- 배고픔 수치는 층(레벨)을 저장/불러오면 가득 찬 상태로 초기화됩니다. 게임 기본 배고픔 모듈이 수치를 저장하지 않기 때문입니다.

# Unity-Fusion2-Eternal Return-Combat-System

---

작성자 : 오도경

궁금한 점 있을 시 Issue 남겨주시면 친절히 답변해드립니다!

# 목차

- [개요](#개요)
  * [작성 의의](#작성-의의)
  * [소개](#소개)
    + [함께 사용된 라이브러리](#함께-사용된-라이브러리)
    + [문서 내용](#문서-내용)
- [Part 1. 구현된 기능 소개](#part-1-구현된-기능-소개)
  * [매칭 시스템](#매칭-시스템)
  * [캐릭터 이동](#캐릭터-이동)
  * [스킬 Q — 직선 투사체](#스킬-q--직선-투사체)
  * [스킬 W — 범위 장판](#스킬-w--범위-장판)
  * [스킬 E — 이동속도 버프 + 추가 투사체](#스킬-e--이동속도-버프--추가-투사체)
  * [스킬 R — 지속 데미지 필드](#스킬-r--지속-데미지-필드)
  * [기본 공격](#기본-공격)
  * [Input Buffering & Animation Canceling](#input-buffering--animation-canceling)
- [Part 2. 프로젝트 구조](#part-2-프로젝트-구조)
  * [스크립트 계층](#스크립트-계층)
  * [상속 및 의존 관계](#상속-및-의존-관계)
- [Part 3. 네트워크 아키텍처](#part-3-네트워크-아키텍처)
  * [Server-Authoritative 모델](#server-authoritative-모델)
  * [Fusion 핵심 개념 정리](#fusion-핵심-개념-정리)
    + [NetworkBehaviour](#networkbehaviour)
    + [[Networked]](#networked)
    + [RPC](#rpc)
    + [TickTimer](#ticktimer)
  * [입력 전달 구조](#입력-전달-구조)
- [Part 4. 동기화 전략](#part-4-동기화-전략)
  * [TickTimer와 UniTask를 함께 사용한 이유](#ticktimer와-unitask를-함께-사용한-이유)
  * [애니메이션 동기화](#애니메이션-동기화)
- [Part 5. 전투 시스템 상세](#part-5-전투-시스템-상세)
  * [데미지 처리 — IDamageProcess](#데미지-처리--idamageprocess)
  * [스킬 시스템 구조](#스킬-시스템-구조)
  * [각 스킬 동작 분석](#각-스킬-동작-분석)
    + [Q — Eva_Q 투사체](#q--eva_q-투사체)
    + [W — Eva_W 장판](#w--eva_w-장판)
    + [R — Eva_R 지속 데미지](#r--eva_r-지속-데미지)
    + [E — Eva_VFLight 포물선 투사체](#e--eva_vflight-포물선-투사체)
    + [기본 공격 — 타겟팅 자동 공격](#기본-공격--타겟팅-자동-공격)
- [Part 6. 조작감 개선 시스템](#part-6-조작감-개선-시스템)
  * [Input Buffering](#input-buffering)
  * [Animation Canceling](#animation-canceling)
- [Part 7. 이동 시스템](#part-7-이동-시스템)
  * [NavMeshAgent만으로는 안 되는가](#navmeshagent만으로는-안-되는가)
  * [경로 계산과 이동](#경로-계산과-이동)
  * [회전 동기화](#회전-동기화)
  * [이동 속도와 틱 레이트 보정](#이동-속도와-틱-레이트-보정)
  * [위치 보간](#위치-보간)
  * [클릭 VFX](#클릭-vfx)
- [더 나아가서](#더-나아가서)
  * [실제 이터널 리턴과의 차이](#실제-이터널-리턴과의-차이)
  * [Dedicated Server](#dedicated-server)
  * [조작감](#조작감)
  * [Lag Compensation](#lag-compensation)

---

# 개요

## 작성 의의

Photon Fusion 2를 활용한 네트워크 전투 시스템의 구현 과정과 설계 방법을 공유한다.

## 소개

본 프로젝트는 이터널 리턴(Eternal Return)을 레퍼런스로 한 MOBA 전투 시스템이다.

"서버가 모든 판정을 담당한다"는 원칙 아래, 캐릭터 이동 / 스킬 4종(Q, W, E, R) / 기본 공격 / 데미지 처리 / 애니메이션 동기화를 구현하였으며, 거기에 더해 Input Buffering과 Animation Canceling을 통한 조작감 개선 시스템도 포함되어 있다.

### 함께 사용된 라이브러리

1. Photon Fusion 2
2. SimpleKCC (Fusion Addon)
3. UniTask (Cysharp)
4. NavMeshAgent (Unity AI)

### 문서 내용

Part 1. 구현된 기능 소개에서는 본 프로젝트에 구현된 기능들을 간단히 소개한다.

Part 2. 프로젝트 구조에서는 전투 관련 스크립트들이 어떤 계층으로 나뉘어 있고 서로 어떤 관계를 맺고 있는지를 통해 프로젝트의 전체 구조를 파악해본다.

Part 3. 네트워크 아키텍처에서는 우리가 채택한 Server-Authoritative 모델이 무엇이고, 왜 이 구조를 선택했는지를 Fusion 2의 핵심 개념들과 함께 소개한다. 네트워크 게임 개발이 처음이라면 이 파트부터 읽어보는 것을 추천한다.

Part 4. 동기화 전략에서는 왜 TickTimer만 쓰지 않고 UniTask를 함께 도입했는지, 그리고 애니메이션 동기화를 어떻게 처리했는지를 다룬다.

Part 5. 전투 시스템 상세에서는 데미지 인터페이스(IDamageProcess)의 설계 의도와 함께 각 스킬(Q, W, E, R) 및 기본 공격의 구현을 분석한다.

Part 6. 조작감 개선 시스템에서는 Input Buffering과 Animation Canceling이 왜 필요한지, 그리고 이를 어떻게 설계하고 구현했는지를 설명한다.

Part 7. 이동 시스템에서는 NavMeshAgent와 SimpleKCC를 왜 조합해서 사용했는지, 경로 계산과 이동 판정의 상세 흐름, 회전 동기화, 틱 레이트 보정, 그리고 다른 플레이어가 부드럽게 보이기 위한 위치 보간 처리까지를 다룬다.

---

# Part 1. 구현된 기능 소개

본 프로젝트에서 구현된 기능 목록이다.

## 매칭 시스템

로비에서 상대를 찾아 매칭이 완료되면 전투 씬으로 진입한다.

<!-- GIF 자리: 로비 → 매칭 → 전투 씬 진입 -->
![매칭 시스템](gif 경로)

## 캐릭터 이동

우클릭으로 이동 목표를 지정하면 서버에서 경로를 계산하고 캐릭터가 해당 위치로 이동한다. 모든 이동 판정은 서버에서 처리되며, 다른 플레이어 화면에서도 부드럽게 보간된다.

<!-- GIF 자리: 우클릭 이동 + 다른 플레이어 시점 -->
![캐릭터 이동](gif 경로)

## 스킬 Q — 직선 투사체

마우스 방향으로 투사체를 발사한다. 적에게 적중하면 데미지를 주고, 적중하지 못하면 일정 시간 후 사라진다.

<!-- GIF 자리: Q 스킬 발사 + 적중 -->
![스킬 Q](gif 경로)

## 스킬 W — 범위 장판

마우스 위치에 장판을 생성한다. 장판 위의 적은 이동속도가 느려지고(슬로우), 장판이 사라질 때 중심부의 적은 공중에 띄워진다(에어본).

<!-- GIF 자리: W 장판 생성 + 슬로우 + 에어본 -->
![스킬 W](gif 경로)

## 스킬 E — 이동속도 버프 + 추가 투사체

사용하면 일정 시간 동안 이동속도가 빨라지고 슬로우에 면역이 된다. E 활성화 중에 Q, W, R 스킬이 적중하면 보너스 포물선 투사체가 추가로 발사된다.

<!-- GIF 자리: E 버프 + Q 적중 시 추가 투사체 발동 -->
![스킬 E](gif 경로)

## 스킬 R — 지속 데미지 필드

토글 방식의 스킬이다. R을 누르면 전방에 데미지 필드가 생성되어 범위 안의 적에게 0.1초 간격으로 데미지를 준다. R을 다시 누르면 해제된다.

<!-- GIF 자리: R 활성화 → 틱 데미지 → R 비활성화 -->
![스킬 R](gif 경로)

## 기본 공격

적 캐릭터를 우클릭하면 자동으로 추적하여 사거리 안에 들어오면 공격한다. 스킬을 사용하면 기본 공격이 취소된다.

<!-- GIF 자리: 우클릭 타겟팅 → 추적 → 공격 -->
![기본 공격](gif 경로)

---

# Part 2. 프로젝트 구조

본 파트에서는 전투 관련 스크립트들이 어떤 계층으로 나뉘어 있고, 서로 어떤 의존 관계를 맺고 있는지를 살펴본다.

프로젝트 내에 어떤 스크립트가 있는지를 계층별로 훑어보고, 가장 핵심인 Eva_Skill이 어떤 파일들을 참조하는지부터 접근하면 보다 쉽게 구조를 파악할 수 있다.

## 스크립트 계층

전투 관련 스크립트는 크게 4개의 계층으로 나뉜다.

**Base Layer** — 모든 영웅이 공유하는 기반

| 파일 | 한줄 설명 |
|------|----------|
| `HeroInput.cs` | 네트워크로 보내는 입력 데이터 (어떤 키를 눌렀는지, 마우스 위치 등) |
| `HeroState.cs` | HP 관리 (서버에서 자동 동기화) |
| `HeroSkill.cs` | "모든 영웅은 Q/W/E/R을 구현해야 한다"는 규칙 |
| `HeroMovement.cs` | 클릭 이동, 회전, 위치 보간 |
| `HeroAnimationController.cs` | 걷기/달리기 애니메이션 동기화 |
| `IDamageProcess.cs` | "데미지를 받을 수 있는 대상"의 공통 규약 |

**Hero Layer** — Eva 캐릭터 전용

| 파일 | 한줄 설명 |
|------|----------|
| `Eva_Skill.cs` | Eva의 모든 전투 로직 (입력 처리, 스킬 실행, 버퍼링, 캔슬링) |
| `Eva_AnimationController.cs` | Eva 스킬/공격 애니메이션 RPC 9종 |

**Skill Layer** — 개별 스킬 오브젝트 (네트워크에 Spawn되어 독립 동작)

| 파일 | 스킬 타입 |
|------|----------|
| `Eva_Q.cs` | 직선 투사체 — 날아가다 적에게 적중하면 데미지 |
| `Eva_W.cs` | 범위 장판 — 슬로우 + 종료 시 에어본 |
| `Eva_R.cs` | 지속 데미지 필드 — 0.1초마다 범위 내 적에게 틱 데미지 |
| `Eva_VFLight.cs` | 포물선 추적 투사체 — E 스킬 보너스 데미지 |

**Control Layer** — 조작감 개선 시스템

| 파일 | 한줄 설명 |
|------|----------|
| `ControlSettingsConfig.cs` | 모든 기능의 ON/OFF 토글 (Inspector에서 조정) |
| `InputBuffer.cs` | 스킬 입력을 저장하는 큐 |
| `SkillCancelData.cs` | 스킬별 캔슬 가능 타이밍 데이터 |

## 상속 및 의존 관계

아래는 스크립트 간의 상속 구조이다. Fusion의 NetworkBehaviour를 기반으로 모든 전투 스크립트가 확장되어 있다.

```
NetworkBehaviour (Fusion 기반 클래스)
    ├── HeroState              → HP 관리
    ├── HeroSkill (abstract)   → Q/W/E/R 스킬 틀
    │       └── Eva_Skill      → 실제 구현 + 버퍼링 + 캔슬링
    ├── HeroMovement           → 이동 처리
    ├── HeroAnimationController → 걷기 애니메이션
    │       └── Eva_AnimationController → 스킬 애니메이션
    ├── Eva_Q / Eva_W / Eva_R  → 스킬 오브젝트
    └── Eva_VFLight            → E 추가 투사체
```

그리고 Eva_Skill은 프로젝트에서 가장 많은 의존성을 가진 파일이다. 이 파일이 참조하는 것들을 정리하면 다음과 같다.

```
Eva_Skill이 참조하는 것들:
    ├── HeroMovement              (스킬 시전 중 이동 정지, 방향 전환)
    ├── Eva_AnimationController   (스킬 애니메이션 RPC)
    ├── InputBuffer               (입력 버퍼링)
    ├── SkillCancelData           (캔슬 타이밍)
    └── Eva_Q / Eva_W / Eva_R    (스킬 오브젝트 Spawn)
```

---

# Part 3. 네트워크 아키텍처

본 프로젝트의 가장 핵심적인 설계 원칙은 Server-Authoritative(서버 권위) 모델이다.

고로 본 내용에 들어가기 전에 Server-Authoritative 모델이 무엇인지, 그리고 이를 구현하기 위해 사용한 Fusion 2의 핵심 개념들을 먼저 소개하고 넘어가고자 한다.

## Server-Authoritative 모델

한 문장으로 요약하면 **"모든 게임 판정은 서버가 한다"**이다.

여기서 드는 근본적인 질문은, 왜 굳이 서버가 모든 판정을 해야 하는가이다.

MOBA 같은 PvP 게임에서는 클라이언트를 신뢰할 수 없다. 만약 클라이언트가 직접 "내 투사체가 적에게 맞았다"라고 서버에 보고하는 구조라면, 이속핵이나 데미지핵 같은 치팅을 원천적으로 막을 수가 없다.

그래서 클라이언트는 "Q를 눌렀다"는 입력만 보내고, 실제로 투사체를 만들고 데미지를 계산하는 건 전부 서버가 처리하도록 설계한 것이다.

<!-- 이미지 자리: 서버 권위 모델 구조도 -->


```
플레이어 A                       서버                        플레이어 B
  "Q 눌렀어" ──────────→   투사체 생성.데미지 판정    <--------- "W 눌렀어"
       
                             
  ←── 결과 동기화 ────   [Networked] + RPC   ────→   결과 렌더링
  화면에 보여줌                                        화면에 보여줌
```

Fusion에서는 이 구조를 두 가지 **권한**으로 구분하고 있다.

| 권한 | 누가 가지고 있나 | 하는 일 |
|------|----------------|---------|
| `InputAuthority` | 해당 플레이어 본인 | 키보드/마우스 입력을 보냄 |
| `StateAuthority` | 서버 | 게임 로직 실행 (데미지, HP, 쿨타임 등) |

코드에서는 이렇게 구분하여 사용한다.

```csharp
// 내 화면에서만 실행할 것 (입력 감지, 로컬 이펙트)
if (HasInputAuthority) { ... }

// 서버에서만 실행할 것 (데미지 계산, HP 변경)
if (HasStateAuthority) { ... }
```

## Fusion 핵심 개념 정리

이제 Server-Authoritative 모델을 구현하기 위해 Fusion 2에서 제공하는 핵심 개념들을 차근차근 살펴보자.

### NetworkBehaviour

Unity의 `MonoBehaviour`를 네트워크용으로 확장한 것이다. 본 프로젝트의 모든 전투 스크립트가 이를 상속받고 있다.

평소에 `Start`, `Update`를 쓰던 것처럼, Fusion에서는 아래의 네트워크 전용 메서드를 대신 사용한다.

| 기존 (싱글플레이어) | Fusion (네트워크) | 언제 실행되나 |
|-------------------|-----------------|-------------|
| `Start()` | `Spawned()` | 네트워크 오브젝트 생성 시 |
| `OnDestroy()` | `Despawned()` | 네트워크 오브젝트 제거 시 |
| `FixedUpdate()` | `FixedUpdateNetwork()` | 매 네트워크 틱 (게임 로직) |
| `Update()` | `Render()` | 매 렌더 프레임 (비주얼) |

### [Networked]

변수 앞에 `[Networked]`를 붙이면, 서버에서 값을 바꿨을 때 모든 클라이언트에 자동으로 전달된다.

```csharp
[Networked] protected float CurrHealth { get; set; }  // 서버에서 HP를 깎으면 → 모든 화면에 반영
[Networked] private TickTimer DamageTimer { get; set; } // 타이머도 동기화 가능
```

별도로 "이 값이 바뀌었으니 보내줘"라는 코드를 작성할 필요가 없다. Fusion이 알아서 처리해준다.

### RPC

"서버에서 이 함수를 호출하면, 모든 클라이언트에서도 실행해줘"라는 기능이다. 스킬 애니메이션처럼 1회성 이벤트에 사용한다.

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void RPC_Multi_Skill_Q()
{
    animator.SetTrigger("tSkill01");  // 모든 화면에서 Q 애니메이션 재생
}
```

그렇다면 [Networked]와 RPC는 언제 어떤 것을 선택해야 할까?

| 상황 | 선택 | 이유 |
|------|------|------|
| HP, 위치, 쿨타임처럼 **계속 유지**되는 값 | `[Networked]` | 나중에 접속한 사람도 현재 값을 받아야 하므로 |
| 스킬 시전, 사망처럼 **한 번 발생**하는 이벤트 | `RPC` | 발생 시점만 알려주면 되므로 |

### TickTimer

`[Networked]`로 동기화되는 네트워크 타이머이다. 쿨다운, 지속시간, 틱 데미지 간격 등 게임 로직의 시간 관리에 사용한다.

```csharp
// 타이머 생성 (0.1초 후 만료)
DamageTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);

// 체크
DamageTimer.IsRunning       // 아직 돌고 있나?
DamageTimer.Expired(Runner) // 시간 다 됐나?
```

일반적인 `float timer -= deltaTime`과의 차이점은, 서버 롤백이 발생해도 자동으로 재계산된다는 것이다. 왜 이것이 중요한지는 Part 4에서 자세히 다룬다.

## 입력 전달 구조

플레이어의 키보드/마우스 입력은 `HeroInput`이라는 구조체에 담겨 매 틱마다 서버로 전송된다.

```csharp
public struct HeroInput : INetworkInput
{
    public NetworkButtons Buttons;           // Q, W, E, R, 좌클릭, 우클릭 (비트 플래그로 압축)
    public Vector3 HitPosition_RightClick;   // 우클릭한 바닥 위치
    public Vector3 HitPosition_Skill;        // 스킬 사용 시 마우스 위치
    public PlayerRef Owner;                  // 누구의 입력인지
    public Vector3 MousePosition;            // 현재 마우스 월드 좌표
    public NetworkId TargetNetworkId;        // 우클릭한 대상 (기본 공격용)
}
```

여기서 `NetworkButtons`는 6개 버튼의 on/off 상태를 비트 플래그로 압축한다. bool 6개(6바이트)를 보내는 대신 1바이트 미만으로 전송할 수 있어서 네트워크 대역폭을 아낄 수 있다.

```
비트:    5          4       3    2    1    0
버튼:  RightClick  Left     R    E    W    Q

예시:   0          0        1    0    0    1  → Q랑 R이 눌린 상태 (값: 9)
```

그리고 `WasPressed(이전 상태, 버튼)`을 사용하면 "이번 틱에 새로 눌린 버튼"만 감지할 수 있다. 이전 틱의 버튼 상태를 `ButtonsPrevious`에 저장해두는 이유가 바로 이것이다.

---

# Part 4. 동기화 전략

Fusion 2에서 시간 기반 로직을 처리하려면 기본적으로 TickTimer를 사용한다. 그런데 본 프로젝트에서는 TickTimer만 사용하지 않고 UniTask를 함께 사용하고 있다.

본 파트에서는 왜 TickTimer만으로 충분하지 않았는지, 그리고 UniTask를 함께 도입한 이유가 무엇인지를 실제 코드와 함께 설명한다.

## TickTimer와 UniTask를 함께 사용한 이유

### TickTimer만 쓰면 안 되는가?

결론부터 말하면, TickTimer만으로도 모든 시간 기반 로직을 처리할 수는 있다. 하지만 그렇게 했을 때 불필요한 복잡성이 생기는 영역이 있다.

TickTimer는 `[Networked]` 속성이므로 값이 변경될 때마다 서버와 모든 클라이언트 사이에 동기화가 발생한다. 데미지나 쿨다운처럼 게임의 승패에 영향을 주는 로직이라면 반드시 이 동기화가 필요하다. 하지만 "1초 후에 히트 이펙트를 지워라" 같은 연출 처리에도 TickTimer를 쓰게 되면 다음과 같은 문제가 생긴다.

1. **불필요한 네트워크 동기화** — 이펙트가 사라지는 타이밍은 게임 로직에 영향을 주지 않는다. 굳이 이 정보를 모든 클라이언트에 동기화할 이유가 없다.

2. **[Networked] 속성의 낭비** — TickTimer를 쓰려면 `[Networked] private TickTimer vfxTimer { get; set; }`처럼 네트워크 동기화 속성을 하나 더 선언해야 한다. 이펙트 종류가 늘어날 때마다 속성이 계속 추가되는 것이다.

3. **순차적 흐름의 표현이 어렵다** — TickTimer는 `FixedUpdateNetwork()`에서 매 틱마다 만료 여부를 확인하는 폴링 방식이다. "100ms 기다리고 → 버퍼를 확인하고 → 결과에 따라 30ms 더 기다리거나 즉시 캔슬" 같은 순차적 흐름을 표현하려면 상태 변수와 분기문이 필요해져서 코드가 복잡해진다.

### 그래서 어떤 기준으로 나눴는가

본 프로젝트에서는 **"이 로직이 [Networked] 게임 상태를 변경하는가?"**를 기준으로 나누고 있다.

- **게임 상태를 변경하는 로직 → TickTimer**

  HP 감소, 쿨다운, 버프 지속시간처럼 게임의 승패에 직접 영향을 주는 값을 건드리는 경우이다. 이런 로직은 서버의 네트워크 틱에 맞춰 실행되어야 하고, 롤백이 발생해도 자동으로 재계산되어야 한다.

- **게임 상태와 무관한 로직 → UniTask**

  히트 이펙트 제거, 애니메이션 캔슬 타이밍처럼 화면 연출에만 관여하는 경우이다. 게임 결과에 영향을 주지 않으므로 네트워크 동기화 없이 로컬에서 처리해도 충분하다.

### 실제 코드에서의 차이

**TickTimer — Eva_R 틱 데미지**

0.1초마다 범위 내 적에게 데미지를 주는 로직이다. HP를 변경하므로 반드시 서버 틱 기준으로 실행되어야 하고, 롤백 시 자동 재계산이 보장되어야 한다.

```csharp
[Networked] private TickTimer DamageTimer { get; set; }

public override void FixedUpdateNetwork()
{
    if (DamageTimer.Expired(Runner))
    {
        DamageTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
        DealDamageToAll();  // → OnTakeDamage() → [Networked] CurrHealth 감소
    }
}
```

**UniTask — 히트 이펙트 제거**

1초 후에 이펙트를 제거하는 로직이다. 이펙트가 0.9초에 사라지든 1.1초에 사라지든 게임 결과에는 아무런 영향이 없다. 이런 처리를 위해 `[Networked] TickTimer`를 선언하고 `FixedUpdateNetwork()`에서 폴링하는 것은 과하다.

```csharp
public async UniTaskVoid HitVFXDestroy(NetworkObject no)
{
    await UniTask.Delay(1000);
    Runner.Despawn(no);
}
```

**UniTask — Animation Canceling 순차 흐름**

UniTask를 도입한 가장 큰 이유가 이 부분이다. 스킬 시전 후 캔슬 판정은 "기다리고 → 확인하고 → 결과에 따라 분기"하는 순차적 흐름인데, async/await로 표현하면 자연스럽게 읽힌다.

```csharp
private async UniTask<bool> TryAnimationCancel(...)
{
    await UniTask.Delay(waitAfterDamageMs);       // 1. 데미지 후 최소 대기

    if (_inputBuffer.HasPendingInput())            // 2. 버퍼에 다음 스킬 있으면
    {
        animationController.RPC_Multi_CancelSkillAnimation();
        IsCasting = false;
        return true;                               //    → 즉시 캔슬
    }

    await UniTask.Delay(skippableDelayMs);         // 3. 없으면 → 후딜 전부 소화
    IsCasting = false;
    return false;
}
```

만약 이 흐름을 TickTimer로 구현한다면 아래와 같이 상태 변수와 분기문이 필요하다.

```csharp
// TickTimer만으로 구현할 경우 (비교용)
[Networked] private TickTimer CancelTimer { get; set; }
[Networked] private int CancelPhase { get; set; }  // 0=대기중, 1=버퍼확인, 2=후딜소화

public override void FixedUpdateNetwork()
{
    if (CancelPhase == 0 && CancelTimer.Expired(Runner))
    {
        if (_inputBuffer.HasPendingInput()) { /* 캔슬 */ }
        else { CancelTimer = TickTimer.CreateFromSeconds(Runner, ...); CancelPhase = 1; }
    }
    else if (CancelPhase == 1 && CancelTimer.Expired(Runner))
    {
        IsCasting = false; CancelPhase = 0;
    }
}
```

동일한 로직인데 상태 변수가 2개 추가되고, 흐름을 한눈에 파악하기 어려워진다. 게다가 캔슬 판정은 게임 상태([Networked])를 변경하는 로직이 아니므로, 이 복잡성을 감수할 이유가 없다.

## 애니메이션 동기화

애니메이션도 위와 마찬가지로 성격에 따라 두 가지 방식을 사용한다.

**걷기/달리기처럼 계속 바뀌는 것 → [Networked]**

```csharp
[Networked] protected int MoveVelocity { get; set; }  // 0=Idle, 1=Walk

// 서버에서 값 변경 → 자동으로 모든 클라에 전달
// 모든 클라이언트의 Render()에서:
animator.SetFloat("MoveSpeed", MoveVelocity);
```

[Networked]를 사용하면 나중에 접속한 플레이어도 현재 MoveVelocity 값을 받게 되므로, 접속 직후부터 올바른 애니메이션이 보인다.

**스킬 시전처럼 한 번 발생하는 것 → RPC**

```csharp
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
public void RPC_Multi_Skill_Q()
{
    animator.SetTrigger("tSkill01");  // "Q 시전!" → 모든 화면에서 재생
}
```

본 프로젝트에서는 Eva 캐릭터에 9개의 애니메이션 RPC가 정의되어 있다 (Q, W, E시작, E종료, R활성화, R비활성화, 기본공격, 공격취소, 스킬캔슬).

---

# Part 5. 전투 시스템 상세

이제 실제 전투가 어떻게 돌아가는지를 살펴볼 차례이다.

본 파트에서는 스킬 시스템 구조부터 시작해서, 각 스킬이 내부적으로 어떤 흐름으로 동작하는지를 분석한다.

## 스킬 시스템 구조

모든 영웅은 `HeroSkill`을 상속받아 Q/W/E/R 4개 스킬을 반드시 구현해야 한다.

```csharp
public abstract class HeroSkill : NetworkBehaviour
{
    protected abstract void Skill_Q();
    protected abstract void Skill_W();
    protected abstract void Skill_E();
    protected abstract void Skill_R();
}
```

이를 상속한 `Eva_Skill`이 Eva 캐릭터의 전체 전투를 담당한다. 매 네트워크 틱마다 입력을 확인하고, 현재 상태에 따라 분기하는 구조이다.

```
매 틱 (FixedUpdateNetwork)
  │
  ├── 에어본 중? → 입력 무시
  ├── R 활성화 중? → R 토글만 처리
  ├── 스킬 시전 중? → 입력을 버퍼에 저장 (Input Buffering)
  └── 대기 상태? → 버퍼 확인 → 직접 입력 처리 → 기본 공격 처리
```

## 각 스킬 동작 분석

### Q — Eva_Q 투사체

마우스 방향으로 투사체를 발사하는 스킬이다. 내부 흐름은 다음과 같다.

```
Skill_Q() 호출
  ├── 이동 정지 + 마우스 방향 회전
  ├── 시전 애니메이션 RPC
  └── Q_SpawnProcess() (async)
        ├── 시전 딜레이 대기 (100ms)
        ├── Runner.Spawn() → 투사체 생성
        └── TryAnimationCancel() → 후딜레이 스킵 가능 구간
```

투사체(Eva_Q) 자체의 로직은 단순하다. 매 틱 앞으로 이동하고, 적과 충돌하면 데미지를 주며 E 보너스 투사체를 발동시킨다. TickTimer가 만료되면 자동 제거된다.

```csharp
public override void FixedUpdateNetwork()
{
    if (life.Expired(Runner))
        Runner.Despawn(Object);
    else
        transform.position += ProjectileSpeed * transform.forward * Runner.DeltaTime;
}
```

### W — Eva_W 장판

마우스 위치에 장판을 깔아 진입한 적에게 슬로우를 걸고, 장판이 사라질 때 데미지와 에어본을 주는 스킬이다.

```
장판 생성                                          장판 소멸
  │                                                   │
  ├── 범위 내 적에게 즉시 데미지                       ├── 범위 내 적에게 종료 데미지
  │                                                   ├── 중심부 적 에어본
  │   적 진입 → 슬로우 적용 (속도 0.65배)             ├── 슬로우 전부 해제
  │   적 이탈 → 슬로우 해제                           └── 장판 제거
  │
  └── TickTimer(duration) ──────────────────────────→
```

범위 안에 있는 적을 추적하기 위해 `HashSet<HeroMovement>`을 사용하고 있다. HashSet은 중복 없이 O(1)으로 추가/제거가 가능하기 때문에, OnTriggerEnter/Exit에서 빈번하게 호출되는 상황에 적합하다.

### R — Eva_R 지속 데미지

토글 방식의 스킬이다. R을 누르면 활성화, 다시 누르면 비활성화된다.

활성화 중에는 `[Networked] TickTimer`로 0.1초마다 범위 내 모든 적에게 데미지를 준다.

```csharp
private readonly HashSet<Collider> _targetsInRange = new();
[Networked] private TickTimer DamageTimer { get; set; }

public override void FixedUpdateNetwork()
{
    if (_targetsInRange.Count == 0) return;

    if (DamageTimer.Expired(Runner))
    {
        DamageTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
        DealDamageToAll();  // 범위 내 전원에게 데미지
    }
}
```

적이 범위에 들어오면 HashSet에 추가, 나가면 제거하는 구조이다. 데미지는 HP를 변경하는 로직이므로 Part 4에서 설명한 기준에 따라 UniTask가 아닌 TickTimer를 사용한다.

### E — Eva_VFLight 포물선 투사체

E 스킬 자체는 자가 버프(이동속도 증가 + 슬로우 면역)이며, Q/W/R이 적중했을 때 보너스 투사체를 발사하는 구조이다.

이 보너스 투사체의 궤적은 포물선으로, 아래와 같이 계산된다.

```csharp
private Vector3 CalculateParabolicPosition(float t)
{
    Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);  // 직선 보간
    float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;            // 위로 볼록한 호
    return linearPos + new Vector3(0, arc, 0);
}
```

`[Networked] progress`로 궤적 진행도를 동기화하고, 모든 클라이언트의 `Render()`에서 위치를 계산하여 부드럽게 보이도록 처리하고 있다.

### 기본 공격 — 타겟팅 자동 공격

적 캐릭터를 우클릭하면 `CurrentTargetId`에 대상이 저장되고, 매 틱마다 거리를 계산하여 공격 여부를 판단한다.

```
우클릭(적) → CurrentTargetId 저장
  │
  매 틱:
  ├── 대상이 죽었거나 없어졌나? → 공격 취소
  ├── 사거리 밖? → 대상 쪽으로 이동
  └── 사거리 안 + 쿨타임 끝? → 공격 실행
        ├── 대상 방향으로 회전
        ├── 공격 애니메이션 RPC
        ├── OnTakeDamage(데미지)
        └── 쿨타임 타이머 시작
```

스킬을 사용하면 `CancelBasicAttack()`이 호출되어 추적과 공격이 즉시 중단된다.

---

# Part 6. 조작감 개선 시스템

실제로 캐릭터를 조작해보면, 스킬 간의 전환이 매끄럽지 않으면 상당히 답답하게 느껴진다.

본 파트에서는 이 문제를 해결하기 위해 도입한 Input Buffering과 Animation Canceling에 대해 다룬다. 두 기능 모두 `ControlSettingsConfig`에서 ON/OFF 토글이 가능해서 기능 비교 테스트도 간편하다.

## Input Buffering

**문제:** 스킬 시전 중에 다음 스킬을 누르면 입력이 무시된다.

```
버퍼링 없이:
  Q 시전 ─────────── 종료
           W 입력 ← 무시됨!  →  W를 다시 눌러야 함 (답답함)

버퍼링 있으면:
  Q 시전 ─────────── 종료 → 버퍼에서 W 꺼냄 → W 즉시 실행!
           W 입력 → 저장됨
```

**해결:** 시전 중의 입력을 `Queue<BufferedInput>`에 저장해두었다가, 시전이 끝나면 꺼내서 실행하는 방식으로 해결하였다.

```csharp
// 시전 중일 때
if (IsCasting)
    BufferSkillInputs(input);     // 버퍼에 저장

// 시전이 끝났을 때
else
{
    ProcessBufferedInputs();       // 버퍼에서 꺼내서 실행
    ProcessDirectSkillInputs();    // 버퍼가 비었으면 직접 입력 처리
}
```

다만 모든 입력을 무한히 저장하면 곤란하므로 유효 시간(0.3초)과 최대 크기(2개) 제한을 두어 너무 오래된 입력은 자동으로 버려지게 했다.

## Animation Canceling

**문제:** 스킬에는 "데미지가 나간 후에도 모션이 남아 있는 시간(후딜레이)"이 있다. 이 시간 동안 아무것도 못 하면 답답하다.

**해결:** 데미지가 이미 적용된 후, 버퍼에 다음 스킬이 대기 중이면 남은 후딜레이를 건너뛰도록 설계했다.

```
스킬 타임라인 (Q 기준):

[0ms]────────[100ms]─────[120ms]──────────────[150ms]
  │              │            │                    │
 시작        투사체 발사   캔슬 가능 시작         후딜 종료
                          (버퍼에 입력 있으면
                           여기서 즉시 다음 스킬!)
                           → 30ms 절약
```

Q와 W 스킬이 공유하는 캔슬 로직은 중복을 피하기 위해 `TryAnimationCancel()`이라는 공통 메서드로 추출되어 있다.

```csharp
private async UniTask<bool> TryAnimationCancel(...)
{
    await UniTask.Delay(waitAfterDamageMs);       // 데미지 후 최소 대기

    if (_inputBuffer.HasPendingInput())            // 버퍼에 다음 스킬 있음?
    {
        // 캔슬 성공! 후딜레이 스킵
        animationController.RPC_Multi_CancelSkillAnimation();
        IsCasting = false;
        return true;
    }

    await UniTask.Delay(skippableDelayMs);         // 캔슬 안 됨 → 후딜 전부 소화
    IsCasting = false;
    return false;
}
```

스킬별 캔슬 타이밍은 `SkillCancelData` ScriptableObject에 정의되어 있어서 코드 수정 없이 Inspector에서 조정할 수 있다.

---

# Part 7. 이동 시스템

MOBA의 "우클릭 이동"은 단순해 보이지만, 네트워크 환경에서는 생각보다 고려할 것이 많다. 경로를 계산해야 하고, 실제 이동은 네트워크와 호환되어야 하고, 회전도 동기화해야 하고, 다른 플레이어 화면에서도 부드럽게 보여야 한다.

본 파트에서는 이 문제들을 하나씩 어떻게 해결했는지를 다룬다.

## NavMeshAgent만으로는 안 되는가

Unity에서 클릭 이동을 구현하는 가장 일반적인 방법은 NavMeshAgent를 쓰는 것이다. `navMeshAgent.SetDestination(목표위치)`만 호출하면 경로 계산부터 이동, 장애물 회피까지 전부 알아서 처리해준다.

하지만 네트워크 게임에서는 이 방식을 그대로 쓸 수 없다. NavMeshAgent는 Unity의 로컬 물리 시스템 위에서 동작하기 때문에, Fusion의 네트워크 틱과 동기화되지 않는다. NavMeshAgent가 자체적으로 캐릭터를 이동시켜 버리면 서버와 클라이언트 사이에 위치가 어긋나게 된다.

그래서 본 프로젝트에서는 NavMeshAgent의 역할을 **경로 계산에만** 한정시키고, 실제 캐릭터 이동은 Fusion과 호환되는 **SimpleKCC**(Simple Kinematic Character Controller)가 담당하도록 분리하였다.

| 컴포넌트 | 하는 일 |
|----------|--------|
| NavMeshAgent | **경로 계산만** — 장애물을 피해 어디로 가야 하는지 계산 |
| SimpleKCC | **실제 이동** — 네트워크 틱에 맞춰 캐릭터를 물리적으로 이동 |

```
우클릭(바닥)
  → HeroInput에 목표 위치 담아서 서버로 전송
  → 서버: NavMeshAgent.CalculatePath() → 경유점(corners) 계산
  → 서버: SimpleKCC.Move(방향 * 속도) → 실제 이동
  → 서버: [Networked] NetworkedPosition 갱신
  → 클라이언트: 위치 보간으로 부드럽게 반영
```

NavMeshAgent의 자체 이동 기능(`updatePosition`, `updateRotation`)은 사용하지 않는다. `CalculatePath()`로 경로만 받아오고, 그 결과인 경유점(corners) 배열을 기반으로 SimpleKCC가 이동을 처리한다.

## 경로 계산과 이동

이동 로직의 핵심은 `PathCalculateAndMove()` 메서드이다. 매 네트워크 틱(`FixedUpdateNetwork`)마다 호출되며, 아래 순서로 동작한다.

```
매 틱 (FixedUpdateNetwork → PathCalculateAndMove)
  │
  ├── 사망 상태? → 속도 0으로 정지
  ├── 에어본 상태? → 이동 불가
  ├── NavMeshAgent 비활성? → return
  │
  ├── 입력에서 우클릭 위치 꺼내기 (lastPos 갱신)
  ├── NavMeshAgent.CalculatePath(lastPos, path) → 경유점 계산
  │
  ├── 경유점이 없으면 → 정지
  ├── 목적지에 도착했으면 → 정지
  ├── 스킬 시전 중 / 공격 중 / 에어본? → 속도 0으로 정지
  │
  └── 이동 가능 → SimpleKCC.Move(방향 * 속도)
               → 걷기 애니메이션 발동
               → 이동 방향으로 회전
```

경유점(corners) 배열에서 다음 목적지를 결정하는 방식을 좀 더 설명하면 다음과 같다. NavMeshAgent가 반환하는 `path.corners`는 시작점부터 도착점까지의 경유점 목록인데, `corners[0]`은 현재 위치(혹은 그 근처)이고 `corners[1]`이 다음으로 향해야 할 지점이다.

```csharp
// 경유점이 1개면 = 이미 도착점 근처
// 경유점이 2개 이상이면 = corners[1]을 향해 이동
Vector3 nextWaypoint = path.corners.Length == 1
    ? path.corners[0]
    : path.corners[1];

var dist = Vector3.Distance(kcc.Position, nextWaypoint);

// 도착 판정: 다음 경유점까지의 거리가 stoppingDistance 이하이고,
// 남은 경유점이 2개 이하(= 마지막 구간)이면 정지
if (dist <= navMeshAgent.stoppingDistance && path.corners.Length <= 2)
{
    return;  // 도착 완료
}
```

이동이 차단되는 상황은 4가지이다.

| 상태 | 설정하는 곳 | 해제하는 곳 |
|------|-----------|-----------|
| `IsCastingSkill` | 스킬 시전 시작 시 (Eva_Skill) | 스킬 시전 완료 시 |
| `IsAttacking` | 기본 공격 시작 시 (Eva_Skill) | 공격 완료 또는 캔슬 시 |
| `IsAirborne` | W 장판 에어본 피격 시 (HeroMovement) | 에어본 지속시간 종료 시 |
| `IsDeath` | HP가 0 이하가 될 때 (HeroState) | — |

이 중 어느 하나라도 true이면 `kcc.ResetVelocity()`로 즉시 정지시키고 걷기 애니메이션도 해제한다.

## 회전 동기화

캐릭터의 회전(바라보는 방향)도 위치와 마찬가지로 네트워크를 통해 동기화되어야 한다. 본 프로젝트에서는 `[Networked] CurrentYaw` 변수로 Y축 회전값을 동기화하고 있다.

이동 중에는 진행 방향으로 부드럽게 회전한다.

```csharp
// 서버: 이동 방향으로 목표 각도 계산
float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

// 즉시 돌지 않고, 초당 720도 속도로 부드럽게 회전
CurrentYaw = Mathf.MoveTowardsAngle(CurrentYaw, targetYaw, rotationDegreesPerSecond * Runner.DeltaTime);

kcc.SetLookRotation(currentPitch, CurrentYaw);
```

스킬 시전 시에는 마우스 방향으로 즉시 회전시키는 별도의 메서드가 있다. Eva_Skill에서 스킬을 발동할 때 `SetLookDirection()` 또는 `SetLookRotationFromQuaternion()`을 호출하여 캐릭터가 마우스 방향을 바로 바라보게 한다.

```csharp
// 이동 중: 부드럽게 회전 (MoveTowardsAngle)
// 스킬 시전: 즉시 회전 (SetLookDirection)
```

`CurrentYaw`가 `[Networked]`이므로 서버에서 값을 변경하면 클라이언트에 자동으로 전달된다. 클라이언트의 `Render()`에서는 전달받은 `CurrentYaw`를 적용하여 회전을 반영한다.

```csharp
public override void Render()
{
    if (HasStateAuthority) return;

    // 위치 보간 (아래 섹션에서 설명)
    // ...

    // 회전 적용 — 서버에서 받은 CurrentYaw를 그대로 반영
    float currentPitch = kcc.GetLookRotation(true, false).x;
    kcc.SetLookRotation(currentPitch, CurrentYaw);
}
```

## 이동 속도와 틱 레이트 보정

이동 속도를 계산할 때, 단순히 `baseSpeed * SpeedMultiplier`만 사용하지 않고 틱 레이트 보정을 곱하고 있다.

```csharp
var speed = baseSpeed * SpeedMultiplier * (TickRateBase / Runner.TickRate);
```

`TickRateBase`는 60(기준 틱 레이트)이다. 왜 이 보정이 필요한가?

SimpleKCC의 `Move()`는 매 틱마다 호출되고, 내부적으로 `Runner.DeltaTime`을 사용하여 이동량을 계산한다. 문제는 Fusion의 틱 레이트가 서버 설정에 따라 달라질 수 있다는 점이다. 틱 레이트가 30Hz로 내려가면 `Runner.DeltaTime`이 두 배로 커져서 Move가 호출되는 횟수는 줄지만 한 번에 이동하는 거리가 늘어난다.

이론적으로는 이것만으로도 총 이동 거리가 같아야 하지만, SimpleKCC 내부의 물리 처리나 충돌 판정이 틱 레이트에 따라 미세하게 달라질 수 있다. `TickRateBase / Runner.TickRate` 보정은 기준 틱 레이트(60Hz)를 기준으로 속도를 정규화하여, 틱 레이트가 변해도 체감 속도가 일정하도록 맞춰주는 역할을 한다.

```
틱 레이트 60Hz → TickRateBase / Runner.TickRate = 60/60 = 1.0 (보정 없음)
틱 레이트 30Hz → TickRateBase / Runner.TickRate = 60/30 = 2.0 (속도 2배 보정)
```

## 위치 보간

### 왜 직접 보간을 해야 하는가

SimpleKCC는 로컬 플레이어(본인)의 이동에 대해서는 별도의 보간 처리가 필요 없다. 서버에서 `kcc.Move()`를 호출하면 KCC가 내부적으로 네트워크 틱 사이의 렌더 프레임에서 위치를 보간해주기 때문이다. 내 캐릭터는 아무런 추가 작업 없이 부드럽게 움직인다.

문제는 상대방 플레이어이다. 상대방의 위치는 서버에서 `[Networked] NetworkedPosition`을 통해 전달받는데, 이 값은 네트워크 틱(60Hz) 단위로만 갱신된다. 렌더 프레임이 144fps라면 틱 사이에 2~3프레임이 끼어 있는 셈인데, 이 사이에는 새로운 위치 정보가 없으므로 캐릭터가 한 자리에 멈춰 있다가 다음 틱에 갑자기 이동하게 된다.

```
로컬 플레이어 (나):
  SimpleKCC가 내부적으로 보간 → 부드러움 ✓

원격 플레이어 (상대방):
  [Networked] 값은 틱 단위로만 갱신 → 끊겨 보임 ✗
  → 직접 Render()에서 Lerp 처리가 필요
```

즉 **클라이언트 → 네트워크** 방향(내가 움직이는 것)은 SimpleKCC가 알아서 처리해주지만, **네트워크 → 클라이언트** 방향(상대방이 움직이는 것을 내 화면에서 보여주는 것)은 우리가 직접 보간을 구현해야 한다.

### 보간 구현

이를 해결하기 위해 클라이언트에서는 `Render()` (매 렌더 프레임)마다 현재 위치와 서버 위치 사이를 보간(Lerp)하고 있다.

```csharp
// 서버: 매 네트워크 틱마다 현재 위치를 [Networked] 변수에 저장
public override void FixedUpdateNetwork()
{
    PathCalculateAndMove();
    NetworkedPosition = kcc.Position;
}

// 클라이언트: 매 렌더 프레임마다 부드럽게 따라감
public override void Render()
{
    if (HasStateAuthority) return;  // 서버는 보간 불필요
    if (IsAirborne) return;         // 에어본 중에는 별도 처리

    Vector3 smoothedPosition = Vector3.Lerp(
        kcc.Position,           // 현재 클라이언트에서 보이는 위치
        NetworkedPosition,      // 서버가 알려준 최신 위치
        positionLerpSpeed * Time.deltaTime
    );
    kcc.SetPosition(smoothedPosition);
}
```

여기서 `positionLerpSpeed`는 15로 설정되어 있다. 이 값이 너무 낮으면 캐릭터가 서버 위치를 늦게 따라가서 지연이 느껴지고, 너무 높으면 위치가 순간이동처럼 튀어 보인다.

---

# 대화 시스템 적용 가이드

## 📋 목차
1. [UI 씬 설정](#1-ui-씬-설정)
2. [DialogueManager 설정](#2-dialoguemanager-설정)
3. [NPC 설정 (자율적 대화)](#3-npc-설정-자율적-대화)
4. [대화 데이터 작성](#4-대화-데이터-작성)
5. [스토리 대화 설정](#5-스토리-대화-설정)
6. [폰 UI 설정 (언어 전환)](#6-폰-ui-설정-언어-전환)

---

## 1. UI 씬 설정

### 1-1. 대화 말풍선 UI 만들기

1. **Canvas 생성**
   - Hierarchy에서 우클릭 → UI → Canvas
   - Canvas Scaler 설정: Scale With Screen Size

2. **말풍선 패널 만들기**
   ```
   Canvas
   └── DialogueBubble (GameObject)
       ├── Background (Image) - 말풍선 배경
       ├── NameText (TextMeshProUGUI) - 캐릭터 이름
       └── DialogueText (TextMeshProUGUI) - 대사 텍스트
   ```

3. **선택지 패널 만들기**
   ```
   Canvas
   └── ChoicePanel (GameObject)
       ├── ChoiceArrow (Image) - 화살표
       ├── ChoiceButton1 (Button + TextMeshProUGUI)
       ├── ChoiceButton2 (Button + TextMeshProUGUI)
       ├── ChoiceButton3 (Button + TextMeshProUGUI)
       └── ChoiceButton4 (Button + TextMeshProUGUI)
   ```

### 1-2. 각 UI 요소 설정

**DialogueBubble (GameObject)**
- 처음에는 비활성화 상태 (체크 해제)

**NameText (TextMeshProUGUI)**
- 폰트: 원하는 폰트 설정
- 정렬: 왼쪽 정렬

**DialogueText (TextMeshProUGUI)**
- 폰트: 원하는 폰트 설정
- 정렬: 왼쪽 정렬
- Wrapping: Enabled

**ChoicePanel (GameObject)**
- 처음에는 비활성화 상태 (체크 해제)

**ChoiceButton (Button)**
- 각 버튼에 TextMeshProUGUI 자식으로 추가
- 버튼 클릭은 코드에서 처리하므로 OnClick 비워두기

**ChoiceArrow (Image)**
- 화살표 스프라이트 설정
- 처음에는 비활성화 가능

---

## 2. DialogueManager 설정

### 2-1. DialogueManager 오브젝트 생성

1. **빈 GameObject 생성**
   - Hierarchy에서 우클릭 → Create Empty
   - 이름: "DialogueManager"

2. **DialogueManager 스크립트 추가**
   - Inspector에서 Add Component → DialogueManager 스크립트 추가

3. **AudioSource 추가**
   - Add Component → Audio Source
   - Play On Awake: 체크 해제

### 2-2. Inspector에서 연결하기

**UI Elements (TextMeshPro)**
- `Speech Bubble Object`: DialogueBubble GameObject 드래그
- `Name Text`: NameText TextMeshProUGUI 드래그
- `Dialogue Text`: DialogueText TextMeshProUGUI 드래그
- `Typing Speed`: 0.03 (원하는 속도로 조절)
- `World Offset`: (0, 2, 0) - 말풍선이 캐릭터 위에 표시될 위치
- `Player Controller`: Player GameObject의 PlayerController 드래그
- `Next Sentence Key`: E (기본값)

**선택지 UI**
- `Choice Panel`: ChoicePanel GameObject 드래그
- `Choice Buttons`: 배열 크기 4로 설정 후 각 버튼 드래그
  - Element 0: ChoiceButton1
  - Element 1: ChoiceButton2
  - Element 2: ChoiceButton3
  - Element 3: ChoiceButton4
- `Choice Texts`: 배열 크기 4로 설정 후 각 버튼의 TextMeshProUGUI 드래그
  - Element 0: ChoiceButton1의 TextMeshProUGUI
  - Element 1: ChoiceButton2의 TextMeshProUGUI
  - Element 2: ChoiceButton3의 TextMeshProUGUI
  - Element 3: ChoiceButton4의 TextMeshProUGUI
- `Choice Arrow`: ChoiceArrow Image 드래그

**오디오**
- `Audio Source`: 같은 GameObject의 AudioSource 드래그

---

## 3. NPC 설정 (자율적 대화)

### 3-1. NPC GameObject 준비

1. **NPC 스프라이트 추가**
   - NPC GameObject에 SpriteRenderer 추가
   - 원하는 스프라이트 설정

2. **CharacterIdentifier 추가**
   - Add Component → CharacterIdentifier
   - `Character ID`: "NPC_NAME" (예: "FRIEND_A", "TEACHER")

3. **Collider2D 추가**
   - Add Component → Box Collider 2D (또는 Circle Collider 2D)
   - **Is Trigger**: ✅ 체크 (중요!)
   - 크기를 적절히 조절

4. **DialogueTrigger 추가**
   - Add Component → DialogueTrigger
   - `Interact Key`: E (기본값)
   - `Interact Prompt`: 상호작용 프롬프트 UI (선택사항)
   - `Default Conversation ID`: 기본 대화 ID 입력

### 3-2. DialogueTrigger 설정

**Contextual Dialogues (상황별 대화)**

1. `Contextual Dialogues` 리스트 크기 설정 (예: 2)

2. **Element 0 설정:**
   - `Day`: 1
   - `Specific State`: Lunch_FreeTime
   - `Behavior`: Repeatable
   - `Conversation ID`: "NPC_DAY1_LUNCH"

3. **Element 1 설정 (Random 예시):**
   - `Day`: 1
   - `Specific State`: AfterSchool
   - `Behavior`: Random
   - `Conversation ID`: (비워두기)
   - `Random Conversation IDs`: 배열 크기 3
     - Element 0: "NPC_DAY1_AFTERSCHOOL_1"
     - Element 1: "NPC_DAY1_AFTERSCHOOL_2"
     - Element 2: "NPC_DAY1_AFTERSCHOOL_3"

### 3-3. NPC 애니메이션 설정 (선택사항)

1. **Animator 추가**
   - NPC GameObject에 Animator 컴포넌트 추가
   - Animator Controller 할당

2. **애니메이션 트리거 설정**
   - Animator Controller에서 트리거 파라미터 추가 (예: "Talk", "Happy")
   - CSV에서 대화 작성 시 애니메이션 트리거 이름 사용

---

## 4. 대화 데이터 작성

### 4-1. Localization.csv 작성

**형식:**
```
ID,KOR,ENG
NAME_PLAYER,로봇,Robot
NAME_NPC,친구A,FriendA
LINE_INTRO_01,안녕! 만나서 반가워,Hello! Nice to meet you
LINE_CHOICE_01,밥 먹으러 갈까?,Want to eat?
LINE_CHOICE_02,산책하러 갈까?,Want to take a walk?
```

**규칙:**
- `NAME_` 접두사: 캐릭터 이름
- `LINE_` 접두사: 대사
- 쉼표가 포함된 텍스트는 따옴표로 감싸기

### 4-2. Conversations.csv 작성

**형식:**
```
Conversation_ID,Order,Speaker_ID,Line_ID
NPC_DAY1_LUNCH,1,NAME_NPC,LINE_INTRO_01
NPC_DAY1_LUNCH,2,NAME_PLAYER,LINE_RESPONSE_01
```

**규칙:**
- `Conversation_ID`: 대화 그룹 ID
- `Order`: 대화 순서 (현재는 자동 정렬됨)
- `Speaker_ID`: 화자 ID (NAME_ 접두사)
- `Line_ID`: 대사 ID (LINE_ 접두사)

### 4-3. 고급 기능 사용 (Inspector에서 설정)

**CSV만으로는 부족한 고급 기능은 Unity Inspector에서 설정:**

1. **LocalizationManager가 대화를 로드한 후**
2. **Runtime에 DialogueLine의 추가 속성 설정**

하지만 더 쉬운 방법은 **대화 작성 에디터 스크립트**를 만들거나,
**대화를 JSON 파일로 작성**하는 것입니다.

**현재는 CSV로 기본 대화만 작성하고,**
**선택지/애니메이션/이펙트는 코드에서 추가하거나**
**별도 설정 파일을 만드는 것을 권장합니다.**

---

## 5. 스토리 대화 설정

### 5-1. GameManager 설정

1. **GameManager GameObject 확인**
   - DialogueManager가 할당되어 있는지 확인

2. **GameManager.cs의 ChangeState() 확인**
   - 각 상태에서 `dialogueManager.StartDialogue()` 호출 확인
   - 예: `dialogueManager.StartDialogue("ASSEMBLY_DAY1", null);`

### 5-2. 스토리 대화 작성

1. **Conversations.csv에 대화 추가**
   ```
   ASSEMBLY_DAY1,1,NAME_TEACHER,LINE_ASSEMBLY_INTRO
   ASSEMBLY_DAY1,2,NAME_PLAYER,LINE_RESPONSE
   ```

2. **Localization.csv에 대사 추가**
   ```
   NAME_TEACHER,선생님,Teacher
   LINE_ASSEMBLY_INTRO,좋은 아침입니다,Good morning
   ```

---

## 6. 폰 UI 설정 (언어 전환)

### 6-1. 폰 UI 만들기

1. **폰 패널 생성**
   ```
   Canvas
   └── PhonePanel (GameObject)
       ├── LanguageButton (Button)
       │   └── LanguageButtonText (TextMeshProUGUI)
       └── (기타 폰 UI 요소들)
   ```

2. **PhonePanel 비활성화** (처음에는 닫혀있음)

### 6-2. PhoneManager 설정

1. **PhoneManager GameObject 생성**
   - 빈 GameObject 생성
   - PhoneManager 스크립트 추가

2. **Inspector에서 연결**
   - `Phone UI Panel`: PhonePanel 드래그
   - `Phone Key`: Tab (기본값)
   - `Language Toggle Button`: LanguageButton 드래그
   - `Language Button Text`: LanguageButtonText 드래그

---

## 7. 소리 이펙트 설정

### 7-1. Resources 폴더 구조

```
Assets
└── Resources
    ├── Localization.csv
    ├── Conversations.csv
    └── Sounds
        ├── dialogue_click.wav
        ├── npc_talk.wav
        └── ...
```

### 7-2. 대화에서 소리 재생

**CSV로는 설정 불가능하므로,**
**Runtime에 DialogueLine을 수정하거나**
**별도 설정 시스템이 필요합니다.**

**임시 해결책:**
- 대화 시작 시 기본 소리 재생
- 또는 대화 ID에 따라 소리 매핑 테이블 사용

---

## 8. 선택지 사용 방법

### 8-1. CSV에서 선택지 대화 작성

```
Conversation_ID,Order,Speaker_ID,Line_ID
CHOICE_EXAMPLE,1,NAME_NPC,LINE_QUESTION
CHOICE_EXAMPLE_OPTION1,1,NAME_PLAYER,LINE_CHOICE_01
CHOICE_EXAMPLE_OPTION2,1,NAME_PLAYER,LINE_CHOICE_02
```

### 8-2. Runtime에 선택지 추가

**현재는 CSV만으로는 선택지를 설정할 수 없으므로,**
**코드에서 직접 DialogueLine을 수정해야 합니다.**

**추천 방법:**
1. 대화 시작 전에 DialogueLine에 선택지 추가
2. 또는 선택지 전용 대화 ID를 만들고 분기 처리

---

## 9. 테스트 방법

### 9-1. 자율적 대화 테스트

1. Play 모드 실행
2. Player를 NPC에게 이동
3. 상호작용 프롬프트 확인
4. E키 누르기
5. 대화 확인

### 9-2. 스토리 대화 테스트

1. GameManager의 `Current State` 설정
2. Play 모드 실행
3. 자동으로 대화 시작 확인

### 9-3. 언어 전환 테스트

1. Play 모드 실행
2. Tab 키로 폰 열기
3. 언어 버튼 클릭
4. 대화가 언어가 바뀌는지 확인

---

## 10. 문제 해결

### 대화가 안 나와요
- DialogueManager의 UI 요소들이 모두 연결되어 있는지 확인
- CSV 파일이 Resources 폴더에 있는지 확인
- Conversation ID가 정확한지 확인

### 선택지가 안 보여요
- ChoicePanel이 활성화되어 있는지 확인
- ChoiceButtons 배열이 올바르게 설정되어 있는지 확인

### 애니메이션이 안 나와요
- NPC에 Animator 컴포넌트가 있는지 확인
- Animator Controller에 트리거가 설정되어 있는지 확인
- 트리거 이름이 정확한지 확인

### 소리가 안 나와요
- AudioSource가 DialogueManager에 연결되어 있는지 확인
- Resources/Sounds 폴더에 파일이 있는지 확인
- 파일명이 정확한지 확인

---

## 추가 팁

1. **대화 작성 시 ID 명명 규칙**
   - `CONVERSATION_DAY1_NPC_LUNCH` 형식으로 명명
   - 일관성 있게 작성하면 관리가 쉬움

2. **선택지 분기**
   - 선택지마다 다른 대화 ID 사용
   - 또는 씬 이동/상태 변경으로 분기

3. **성능 최적화**
   - CharacterIdentifier는 자동으로 캐싱됨
   - 많은 NPC가 있으면 씬 시작 시 한 번만 캐싱

4. **디버깅**
   - Console에서 에러 메시지 확인
   - DialogueManager의 Debug.Log 확인





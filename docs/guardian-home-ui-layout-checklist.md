# NexusWorks.Guardian Home UI 레이아웃 점검 체크리스트

> 작성일: 2026-03-13
> 대상: `src/NexusWorks.Guardian.UI`
> 범위: Home 화면, 윈도우 크기 정책, 반응형 레이아웃, 스크롤 구조

## 한줄 결론

현재 Home 화면은 데스크톱 워크벤치 구조, 폭/높이 반응형 정책, compact 테이블 전략, hotkey map/dispatch 및 viewport smoke 테스트까지 정리됐고, macOS에서는 `MacCatalyst` 네이티브 빌드/실행까지 확인했습니다. 레포 기준 남은 구현 작업은 없습니다. 다만 Windows 런타임에서의 실제 창 리사이즈/실사용 QA는 별도 환경에서 확인해야 합니다.

## 현재 구조 평가

- 장점
  - 실행 설정, 실행 요약, 결과 큐, 상세 Inspector의 4영역 정보 구조는 명확합니다.
  - Tailwind 토큰과 `gw-*` 재사용 클래스가 있어 레이아웃 정리 기반은 있습니다.
  - 검색, 이력, 상세 패널, 단축키까지 한 화면 워크벤치 방향은 좋습니다.
  - 상단 입력/요약 영역과 하단 데이터 작업영역의 최대 폭이 분리되어 초광폭 모니터 활용도가 좋아졌습니다.

- 한계
  - `900~1199px` 구간은 1차 정책이 반영됐고, `899px 이하`에서도 bulk action, report action, 테이블 컬럼 축소가 들어갔습니다.
  - 낮은 높이 대응은 밀도 보정, shortcut hint 축소, `960/820/720px` viewport smoke 테스트까지 반영했습니다.
  - 입력행, 결과 툴바, Inspector compact와 키보드 포커스 흐름은 hotkey map/dispatch 자동 회귀까지 반영했습니다.
  - MacCatalyst에서는 native build/launch까지 확인했습니다. 다만 Windows 런타임에서의 실제 창 리사이즈와 실사용 QA는 별도 환경에서 확인해야 합니다.

## 1차 반영 상태

- [x] Windows 기본 창 크기 및 최소 크기 추가
- [x] 메인 컨테이너 세로 스크롤 구조 1차 정리
- [x] 경로 입력 행 줄바꿈 대응
- [x] 실행 액션 영역 2단 재배치
- [x] 결과 툴바를 액션/필터/검색으로 분리
- [x] 결과 테이블 최소 폭 확보
- [x] Inspector compact 모드 전환
- [x] Inspector 상세 섹션 접기 구조 추가
- [x] 초광폭 화면에서 상단/하단 폭 정책 분리
- [x] `820px`, `720px` 높이 대응 1차 밀도 보정
- [x] 저높이 shortcut hint 밀도 축소
- [x] hotkey map/dispatch 자동 회귀 테스트 추가
- [x] `960px`, `820px`, `720px` 높이 viewport smoke 테스트 추가
- [x] MacCatalyst 네이티브 빌드/실행 확인
- [x] `900~1199px` 중간 폭 Queue/Details 전환 정책 추가
- [x] `899px 이하` bulk action drawer 추가
- [x] `899px 이하` report action drawer 추가
- [x] 결과 테이블 compact 컬럼 전략 추가
- [x] 레포 기준 UI 구현 및 자동 회귀 정리 완료

## 우선순위 체크리스트

- [x] Windows 초기 크기와 최소 크기 정책을 정의한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Platforms/Windows/App.xaml.cs`, `src/NexusWorks.Guardian.UI/MainPage.xaml.cs`
  - 현재 기본 크기 `1440x960`, 최소 크기 `1100x760` 기준이 반영되어 있습니다.
  - 권장 기준:
    - 초기 크기: `1440x960` 전후
    - 최소 크기: 현재 구조 유지 시 `1100x760` 이상
    - 만약 `900px`대까지 지원하려면 compact 레이아웃을 별도로 설계해야 합니다.

- [x] 세로 스크롤 소유권을 한 번 더 설계한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/wwwroot/css/app.css:49`, `src/NexusWorks.Guardian.UI/Components/Layout/MainLayout.razor:30`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:12`
  - 현재는 페이지 세로 스크롤을 `main`이 소유하고, 결과 테이블과 Inspector만 내부 스크롤을 가지는 구조로 1차 정리되어 있습니다.
  - 점검 기준:
    - 페이지 전체 세로 스크롤을 누가 소유하는지 명확할 것
    - 결과 테이블과 Inspector만 내부 스크롤을 가질지, 페이지 자체도 스크롤할지 결정할 것
    - `800px` 전후 높이에서 내용 손실 없이 접근 가능할 것

- [x] `xl` 외의 중간 폭 레이아웃 규칙을 정의한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:13`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:476`
  - 현재 `900px+`에서는 폼/실행 액션이 2열로 정리되고, `1100~1279px`에서는 상단 `Execution Setup | Run Summary`가 다시 2단으로 올라갑니다. `xl` 미만 workspace는 `Queue / Details` 전환 모드로 동작합니다.
  - 최소한 아래 4단계는 문서화하는 것이 좋습니다.
    - `1440px+`: 현재와 유사한 와이드 워크벤치
    - `1200px~1439px`: 상단 2단 유지, 하단은 큐 우선 + Inspector 축소
    - `900px~1199px`: 큐/Inspector를 탭 또는 토글 방식으로 전환
    - `899px 이하`: 완전 단일 컬럼 + 2차 액션 축소

- [x] 경로 입력 행에 줄바꿈 전략을 넣는다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:52`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:73`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:94`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:115`
  - 현재는 `Input`과 액션 그룹이 분리되어 좁아지면 버튼/배지가 다음 줄로 내려갈 수 있습니다.
  - 점검 기준:
    - `Browse/Open` 버튼은 좁아지면 다음 줄로 내려갈 수 있을 것
    - Badge는 입력행 우측 고정 대신 하단 보조행으로 이동 가능할 것
    - 경로 텍스트가 버튼 때문에 지나치게 좁아지지 않을 것

- [x] 결과 툴바를 1행 집합이 아니라 2단 구조로 나눈다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:484`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:515`
  - 현재 bulk action, 필터 칩, 검색창은 분리된 클러스터로 정리되어 있습니다.
  - 권장 분리:
    - 1행: 선택/벌크 액션
    - 2행: 필터 칩 + 검색
  - `1200px` 이하에서는 2차 액션을 메뉴 또는 접힘 영역으로 보내는 것이 안전합니다.

- [x] 결과 테이블은 "축소"가 아니라 "의도된 가로 스크롤"로 읽기성을 지킨다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/tailwind/input.css:36`, `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:532`
  - 현재 `gw-table` 최소 폭을 유지하되, `899px 이하`에서는 `Type`, `Rule` 컬럼을 숨기고 해당 정보를 `Path` 셀 안 compact 메타로 옮겨 읽기성을 유지합니다.
  - 점검 기준:
    - 테이블 최소 폭을 명시할 것
    - `Path`, `Status`, `Severity`, `Rule`, `Summary`의 우선순위를 정할 것
    - 좁은 폭에서는 가로 스크롤이 생겨도 텍스트는 읽을 수 있을 것

- [x] Inspector는 compact 폭에서 별도 모드로 전환한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:577`
  - 현재는 `xl` 미만에서 결과 큐 아래에 Inspector가 그대로 길게 이어집니다.
  - 점검 기준:
    - `1200px` 이하에서는 탭, drawer, accordion 중 하나로 전환할 것
    - JAR/XML/YAML 상세는 모두 한 번에 펼치기보다 섹션 접기 전략을 둘 것
    - 선택된 항목의 핵심 정보만 먼저 보여주고 상세 diff는 단계적으로 노출할 것

- [x] 초광폭 화면에서는 상단 입력 영역과 하단 데이터 영역의 폭 정책을 분리한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Components/Layout/MainLayout.razor:6`, `src/NexusWorks.Guardian.UI/Components/Layout/MainLayout.razor:31`
  - 현재 상단 입력/요약 영역은 `1600px`, 하단 queue/inspector 작업영역은 `1840px` 기준으로 분리되어 있습니다.
  - 권장 방향:
    - 상단 hero/form 영역은 최대 폭 유지
    - 하단 queue/inspector 작업 영역은 더 넓게 사용
    - 데이터 워크벤치와 마케팅성 상단 레이아웃을 같은 폭 정책으로 묶지 않을 것

- [x] 높이 축소 시나리오를 viewport smoke test로 검증한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/wwwroot/css/app.css:223`, `src/NexusWorks.Guardian.UI/tests/guardian-layout-viewport.test.js`
  - `960px`, `820px`, `720px` 높이에서 shortcut secondary badge 노출, gap, toolbar padding, workspace/inspector 최소 높이를 자동 검증합니다.

- [x] 반응형 변경 이후 키보드 플로우 핵심 경로를 자동 회귀로 고정한다.
  - 근거 코드: `src/NexusWorks.Guardian.UI/Components/Pages/Home.razor:928`, `src/NexusWorks.Guardian.UI/tests/guardian-hotkey-map.test.js`, `src/NexusWorks.Guardian.UI/tests/guardian-hotkeys.test.js`
  - `/`, `?`, `Esc`, 결과 검색 포커스, keydown dispatch, 선택 row/history scroll helper를 자동 회귀로 확인합니다.

## 환경 제약으로 남는 런타임 QA

- [x] MacCatalyst 네이티브 빌드/실행을 확인한다.
  - `maui-maccatalyst` workload 설치 후 `dotnet clean` + `dotnet build -f net8.0-maccatalyst`로 재생성한 앱 번들을 실행했고, UIKit event log가 들어오는 상태까지 확인했습니다.
  - workload 설치 전 산출물이 남아 있으면 `Failed to load AOT module 'System.Private.CoreLib' while running in aot-only mode: doesn't match assembly.`로 즉시 종료될 수 있습니다.

- [ ] Windows 네이티브 런타임에서 실제 창 리사이즈를 확인한다.
  - 권장 확인 높이:
    - `960px`
    - `820px`
    - `720px`

- [ ] 실행 중인 앱에서 실제 사용자 키보드 플로우를 한번 더 확인한다.
  - 자동 회귀는 추가했고 MacCatalyst 앱 launch도 확인했지만, 최종 확인은 실행 중인 앱에서 `/`, `J/K`, `N/P`, `O`, `Shift+O`, `?`, `Esc`를 직접 점검하는 것이 안전합니다.

## 권장 레이아웃 고도화 방향

### 1. Wide Workspace

- 상단: `Execution Setup | Run Summary`
- 하단: `Review Queue | Inspector`
- 현재 구조를 유지하되 창 기본 크기와 최소 크기를 명시합니다.

### 2. Standard Desktop

- 상단: `Execution Setup`
- 중단: `Run Summary`
- 하단: `Review Queue | Inspector`
- 상단 정보량을 줄이고 하단 검토 영역 우선으로 재배치합니다.

### 3. Compact Desktop

- 상단: `Execution Setup`
- 하단: `Review Queue`
- Inspector는 우측 고정 패널이 아니라 `Details` 토글 또는 탭으로 전환합니다.

### 4. Narrow Window

- 완전 단일 컬럼
- 결과 액션은 최소 버튼만 노출
- 나머지 액션은 메뉴 또는 접힘 패널로 이동
- Inspector는 inline 확장형으로 전환

## 최종 판단

- 지금 Home 화면은 "와이드 데스크톱 프로토타입"을 넘어, 반응형 정책과 자동 회귀까지 포함한 워크벤치 화면으로 정리됐습니다.
- 레포 기준 후속 구현은 마감해도 됩니다.
- 남는 것은 코드 작업이 아니라, Windows 쪽 네이티브 최종 QA입니다.

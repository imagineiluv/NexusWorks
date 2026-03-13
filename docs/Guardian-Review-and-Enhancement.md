# NexusWorks.Guardian 프로젝트 종합 리뷰 및 고도화 검토

> 작성일: 2026-03-12 | 보완일: 2026-03-13
> 대상: `src/NexusWorks.Guardian`, `src/NexusWorks.Guardian.Cli`, `src/NexusWorks.Guardian.UI`, `src/NexusWorks.Guardian.Tests`
> 기준 문서: `docs/nexusworks-guardian-design-plan.md`
> 버전: v0.1.0 (Phase 4 완료)

---

## 1. 프로젝트 개요

### 1.1 솔루션 구조

| 프로젝트 | 타입 | 역할 |
|---------|------|------|
| **NexusWorks.Guardian** | .NET 8 Class Library | 핵심 비교 엔진, 베이스라인 룰 해석, 리포트 생성 |
| **NexusWorks.Guardian.Cli** | .NET 8 Console App | Headless CLI 러너, 샘플 데이터셋 실행, 자동화 연동 |
| **NexusWorks.Guardian.UI** | .NET 8 MAUI + Blazor | 크로스플랫폼 데스크톱 UI (Windows/macOS) |
| **NexusWorks.Guardian.Tests** | .NET 8 xUnit | 단위/통합 테스트 |

### 1.2 핵심 기능

NexusWorks.Guardian는 **운영본과 패치본 두 디렉토리 트리를 베이스라인 룰(baseline.xlsx) 기반으로 비교**하여 필수 파일 누락, 파일 변경, JAR 내부 엔트리 차이, XML/YAML 구조 차이를 자동 검증하고, HTML 대시보드와 Excel/JSON 리포트로 결과를 제공하는 데스크톱 중심 패치 검수 프로그램입니다.

### 1.2.1 현재 Phase 진행 상태

| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 0 | 프로젝트 정리, Tailwind 파이프라인, 배포 스크립트 | ✅ 완료 |
| Phase 1 | 비교 엔진 MVP (Baseline/Inventory/Rule/Compare/Status) | ✅ 완료 |
| Phase 2 | 리포트 MVP (HTML/Excel/출력 폴더) | ✅ 완료 |
| Phase 3 | 데스크톱 UI (비교 실행/대시보드/결과 목록/상세 패널) | ✅ 완료 |
| Phase 4 | 고도화 (YAML diff, XPath 상세, JAR class 요약, 성능, 이력) | ✅ 완료 |

### 1.3 아키텍처 레이어

```
Guardian Core Library
├── Models/          → 도메인 모델 (sealed record, enum)
├── Infrastructure/  → 경로 정규화, 성능 튜닝
├── Baseline/        → Excel 베이스라인 읽기/검증
├── Inventory/       → 파일 시스템 스캔 및 해싱
├── RuleResolution/  → 룰 매칭 (정규식 패턴 포함)
├── Comparison/      → Hash/JAR/XML/YAML 비교기
├── Evaluation/      → 상태/심각도 판정
├── Orchestration/   → 비교 워크플로우 오케스트레이션
├── Reporting/       → HTML/Excel/JSON 리포트 생성
└── Preferences/     → 최근 경로 영속화
```

---

## 2. NexusWorks.Guardian (Core Library) 리뷰

### 2.1 강점

- **Immutable 모델 설계**: `sealed record` 사용으로 값 기반 동등성과 불변성 확보
- **인터페이스 기반 DI**: `IBaselineReader`, `IInventoryScanner`, `IHashProvider`, `IFileComparer` 등 추상화 우수
- **병렬 처리**: `Parallel.ForEach`를 활용한 인벤토리 스캔 및 비교 실행
- **다중 비교 포맷**: Hash, JAR, XML, YAML 구조적 비교 지원
- **CancellationToken 지원**: 오케스트레이션 레이어에서 취소 토큰 전파

### 2.2 코드 품질 이슈

#### 심각도: 높음

| # | 이슈 | 위치 | 설명 |
|---|------|------|------|
| 1 | **SRP 위반** | `GuardianFileComparer` | 비교 방식 선택, 누락 파일 처리, 상태/심각도 평가, 에러 포매팅, 결과 생성 등 5개 이상 책임 혼재 |
| 2 | **SRP 위반** | `StaticHtmlReportWriter` | ~450줄에 데이터 집계, HTML 포매팅, JS 코드 생성, 카드/테이블 빌딩 등 혼재 |
| 3 | **Generic Exception 처리** | `ExecutionHistoryStore.Load()` | 모든 예외를 무시하고 `null` 반환 → 데이터 손상/파일 시스템 오류 미감지 |

#### 심각도: 중간

| # | 이슈 | 위치 | 설명 |
|---|------|------|------|
| 4 | **매직 스트링** | `JarComparer`, `RuleResolutionServices` | "BOOT-INF/classes/", "WEB-INF/classes/", "AUTO" 등 하드코딩 |
| 5 | **OCP 위반** | `ResolvedRuleFactory.EnsureCompareMode()` | 3가지 파일 타입 하드코딩 → 새 포맷 추가 시 코드 수정 필요 |
| 6 | **Regex 재컴파일** | `RuleResolutionServices` | 패턴 룰 매칭 시 매번 Regex 객체 생성 → 대량 파일 시 성능 저하 |
| 7 | **정적 유틸리티** | `GuardianPerformanceTuning` | static 클래스로 테스트 어려움 → DI 가능한 인터페이스 필요 |
| 8 | **불완전한 에러 컨텍스트** | `GuardianFileComparer` | 비교 실패 시 `ex.Message`만 캡처 → 스택 트레이스 유실 |

### 2.3 보안 이슈

#### 심각도: 높음

| # | 이슈 | 위험 | 권장 조치 |
|---|------|------|----------|
| 1 | **경로 순회(Path Traversal)** | `RecentPathStore.NormalizePath()`에서 `..` 세그먼트 허용 | 허용 범위 검증 또는 화이트리스트 적용 |
| 2 | **Silent Exception** | `ExecutionHistoryStore`에서 보안 관련 실패 미로깅 | 예외 로깅 및 심각도별 처리 추가 |

#### 심각도: 중간

| # | 이슈 | 위험 | 권장 조치 |
|---|------|------|----------|
| 3 | **Zip Bomb** | `JarComparer.ReadEntries()`에서 엔트리 수/크기 미검증 | 최대 엔트리 수 및 크기 제한 적용 |
| 4 | **YAML 역직렬화** | `YamlDotNet` 기본 설정으로 임의 타입 인스턴스화 가능 | `SafeDeserializer` 또는 타입 화이트리스트 적용 |
| 5 | **정보 노출** | HTML 리포트에 전체 파일 경로 포함 | 경로 마스킹 옵션 추가 |
| 6 | **입력 경로 미검증** | `GuardianComparisonEngine.Execute()`에서 임의 경로 허용 | 경로 화이트리스트 또는 승인 로직 추가 |

### 2.4 성능 이슈

| # | 이슈 | 영향 | 권장 조치 |
|---|------|------|----------|
| 1 | **중복 경로 정규화** | 다수 위치에서 `NormalizeRelativePath()` 반복 호출 | 인벤토리 스캔 시 1회 정규화 후 저장 |
| 2 | **Regex 재컴파일** | 1,000 파일 × 100 패턴 = 100,000회 Regex 매칭 | 패턴 사전 컴파일 및 캐싱 |
| 3 | **XML 비교 확장성** | 불균형 트리에서 O(n²) 동작 가능 | 정렬된 병합 알고리즘 적용 |
| 4 | **HTML 리포트 JSON 인라인** | 10,000+ 항목 시 20MB+ HTML 생성 | 페이지네이션 또는 AJAX 로딩 |
| 5 | **메모리 효율** | `EnumerateFiles().ToArray()`로 전체 파일 목록 메모리 로드 | 배치 처리 또는 스트리밍 방식 |

### 2.5 누락 기능

| # | 기능 | 우선순위 | 설명 |
|---|------|----------|------|
| 1 | **전체 취소 토큰 전파** | 높음 | 베이스라인 읽기/검증에 CancellationToken 미전파 |
| 2 | **로깅 인프라** | 높음 | 실행 요약만 기록, 항목별 상세 로그 미지원 |
| 3 | **증분 비교 모드** | 중간 | 항상 전체 트리 재스캔 → 타임스탬프/해시 캐시 기반 증분 지원 필요 |
| 4 | **리포트 필터링** | 중간 | 상태/심각도/경로 패턴별 사전 필터링 미지원 |
| 5 | **베이스라인 템플릿 생성기** | 중간 | 기존 파일 트리에서 베이스라인 Excel 자동 생성 기능 |
| 6 | **Dry-Run 모드** | 낮음 | 실제 비교 없이 룰 매칭 미리보기 |
| 7 | **타임아웃 메커니즘** | 중간 | 개별 비교 작업의 타임아웃 미지원 |

---

## 3. NexusWorks.Guardian.Cli 리뷰

### 3.1 개요

- **역할**: headless CLI 진입점으로, UI 없이 비교 실행 후 HTML/Excel/JSON/Log 생성
- **참조**: Guardian Core Library만 참조 (`NexusWorks.Guardian.csproj`)
- **기능**: `--sample` 모드(샘플 데이터셋 자동 탐색), 명시적 경로 모드, `--help`

### 3.2 강점

- **깔끔한 CLI 파싱**: `GuardianCliOptions` sealed record + `Parse()` 메서드로 커맨드라인 인자 처리
- **샘플 데이터셋 자동 탐색**: `AppContext.BaseDirectory`에서 상위로 재귀 탐색하여 `sample/guardian` 자동 발견
- **적절한 에러 처리**: 최상위 try-catch로 실패 시 stderr 출력 및 exit code 1 반환
- **Composition Root 명시**: `CreateRunner()`에서 모든 의존성을 수동 조합 → DI 컨테이너 없이 명확한 구성

### 3.3 이슈

| # | 이슈 | 심각도 | 설명 |
|---|------|--------|------|
| 1 | **UseAppHost=false** | 중간 | self-contained 배포 불가 → `dotnet run`으로만 실행 가능. 스크립트 연동 시 불편 |
| 2 | **비동기 미지원** | 낮음 | `Main()`이 동기 → 대용량 비교 시 스레드 블로킹. `async Main` 전환 권장 |
| 3 | **CancellationToken 미전파** | 중간 | `Ctrl+C` 시그널 처리 없음 → 장시간 실행 중 graceful shutdown 불가 |
| 4 | **출력 진행 상태 미표시** | 낮음 | 비교 중 아무 출력 없음 → `IProgress<T>` 또는 단계별 콘솔 메시지 추가 권장 |
| 5 | **인자 검증 위치** | 낮음 | `ResolveRequest()`에서 개별 throw → 여러 필드 동시 누락 시 첫 번째만 보고 |
| 6 | **로깅 미연동** | 중간 | 에러 시 `ex.Message`만 출력 → 상세 로그 파일 생성 미지원 |

### 3.4 권장 개선

```
[ ] UseAppHost=true 전환 (self-contained 배포)
[ ] async Main + Console.CancelKeyPress → CancellationToken 전파
[ ] 단계별 진행 상태 콘솔 출력 (Baseline loading... → Scanning... → Comparing... → Done)
[ ] --verbose / --quiet 플래그 추가
[ ] 복수 인자 오류 일괄 보고 (ValidationResult 패턴)
```

---

## 4. NexusWorks.Guardian.UI (MAUI + Blazor) 리뷰

### 4.1 아키텍처

- **프레임워크**: .NET 8 MAUI + Blazor WebView 하이브리드
- **플랫폼**: Windows (WinUI 3) / macOS (Catalyst) 주력, iOS/Android/Tizen 최소 지원
- **UI 스택**: Blazor Razor 컴포넌트 + Tailwind CSS + Material Symbols (Outlined)
- **폰트**: Inter (본문) + JetBrains Mono (코드/경로/해시)

### 4.2 강점

- **모던 UI 디자인**: Tailwind CSS 기반, Guardian 전용 테마 토큰 (`guardian-ink`, `guardian-primary` 등) 정의
- **서비스 기반 DI**: `GuardianWorkbenchService`, `PlatformPathSelectionService` 의존성 주입
- **플랫폼 추상화**: Windows FileOpenPicker / macOS UIDocumentPickerViewController 분리
- **실시간 피드백**: 경로 유효성 Badge, 베이스라인 미리보기 통계
- **Recent Paths**: 필드별 최근 6건 경로 드롭다운
- **포괄적 키보드 단축키**: 28개+ 단축키 구현 (`guardian-hotkey-map.js`)
- **디자인 시스템**: `gw-*` 컴포넌트 클래스 16종 정의 (`gw-shell`, `gw-toolbar`, `gw-panel`, `gw-badge` 등)
- **4영역 레이아웃**: 상단 툴바 + 실행 설정/KPI + 결과 테이블 + 상세 패널

### 4.3 화면 구현 현황 (디자인 문서 대비)

| 화면 | 디자인 문서 | 구현 상태 | 비고 |
|------|-----------|----------|------|
| **화면 1: 비교 실행** | 섹션 9.3 | ✅ 완전 구현 | 경로 입력, 브라우즈, 최근 경로, 유효성 배지, Run Readiness |
| **화면 2: 요약 대시보드** | 섹션 9.4 | ⚠️ 대부분 구현 | KPI 카드, 성능 텔레메트리 표시. **차트 라이브러리 미적용** |
| **화면 3: 결과 목록** | 섹션 9.5 | ✅ 완전 구현 | 필터, 검색, 정렬, 체크박스 다중 선택, 벌크 작업 |
| **화면 4: 상세 패널** | 섹션 9.6 | ⚠️ 기능적 완전 | JAR/XML/YAML 섹션 조건부 표시. **탭 UI 미적용** (인라인 섹션 방식) |

### 4.4 키보드 단축키 구현 현황

디자인 문서 섹션 9.3에서 명시한 단축키가 **모두 구현**되어 있습니다.

| 단축키 | 동작 | 구현 |
|--------|------|------|
| `Ctrl/Cmd+Enter` | 비교 실행 | ✅ |
| `Ctrl/Cmd+Shift+Enter` | 재실행 | ✅ |
| `Alt+Shift+H` | 이력 새로고침 | ✅ |
| `Alt+Shift+S` | 샘플 경로 로드 | ✅ |
| `[` / `]` | 이력 선택 이동 | ✅ |
| `M` | 선택된 이력 불러오기 | ✅ |
| `/` | 결과 검색창 포커스 | ✅ |
| `1`~`5` | 상태 필터 전환 | ✅ |
| `J/K`, `N/P` | 결과 목록 이동 | ✅ |
| `X` | bulk selection 토글 | ✅ |
| `Alt+Shift+A/R/C` | visible/review/clear 선택 | ✅ |
| `O` / `Shift+O` | current/patch 파일 열기 | ✅ |
| `Alt+Shift+O/P` | bulk current/patch 열기 | ✅ |
| `H/E/D/L/U` | HTML/Excel/JSON/Log/Output 열기 | ✅ |
| `?` / `Esc` | 단축키 도움말 열기/닫기 | ✅ |

### 4.5 Tailwind 컴포넌트 클래스 현황

| 클래스 | 용도 | 구현 |
|--------|------|------|
| `gw-shell` | 앱 프레임 | ✅ |
| `gw-toolbar` | 상단 바 | ✅ |
| `gw-panel` | 카드/패널 | ✅ |
| `gw-kpi-card` | KPI 카드 | ✅ |
| `gw-badge` | 상태 배지 | ✅ |
| `gw-code` | 모노스페이스 표시 | ✅ |
| `gw-table` | 결과 테이블 | ✅ |
| `gw-input` | 폼 입력 | ✅ |
| `gw-button` / `gw-button-secondary` | 버튼 | ✅ |
| `gw-chip` / `gw-chip-active` | 필터 칩 | ✅ |
| `gw-row` / `gw-row-active` | 테이블 행 | ✅ |
| `gw-inspector` | 상세 패널 | ❌ 미정의 (인라인 Tailwind 사용 중) |

### 4.6 코드 품질 이슈

#### 심각도: 높음

| # | 이슈 | 설명 |
|---|------|------|
| 1 | **MVVM 미구현** | `Home.razor`에 ~1,200줄의 로직 혼재 (폼, 검증, 파일 작업, 상태 관리). ViewModel 분리 필요 |
| 2 | **비동기 에러 처리 부재** | `RunCompareAsync` 등 비동기 작업에 try-catch 미비 |
| 3 | **플랫폼 조건부 컴파일 과다** | `PathSelectionService`에 `#if` 블록 산재 → 팩토리 패턴 또는 구현체 분리 필요 |
| 4 | **취소 UI 미지원** | CancellationToken 전달하지만 UI에서 취소 트리거 불가 |

#### 심각도: 중간

| # | 이슈 | 설명 |
|---|------|------|
| 5 | **하드코딩 문자열** | "Recent current roots" 등 지역화 불가능한 문자열 |
| 6 | **매직 넘버** | `maxCount = 8` 등 상수 미추출 |
| 7 | **로깅 미구현** | 서비스 레이어에 로깅 프레임워크 미연동 |
| 8 | **macOS NSUrl 리소스 누수** | 보안 스코프 리소스 참조 정리 전략 미문서화 |
| 9 | **상태 색상 토큰 불일치** | `guardian-success/warning/danger` 정의했으나 배지에서 `emerald-700/amber-600` 등 직접 사용 |

### 4.7 MVVM 패턴 준수도: 4/10

| 항목 | 상태 | 비고 |
|------|------|------|
| DI 컨테이너 | ✅ | MauiProgram.cs에서 서비스 등록 |
| 서비스 레이어 | ✅ | GuardianWorkbenchService 분리 |
| ViewModel 클래스 | ❌ | 미구현 → Home.razor 코드비하인드에 로직 혼재 |
| INotifyPropertyChanged | ❌ | 옵저버블 패턴 미사용 |
| ICommand | ❌ | 커맨드 패턴 미사용 |
| 데이터 바인딩 일관성 | ⚠️ | @bind-Value, 수동 이벤트 핸들러 혼용 |

### 4.8 접근성 평가: 6/10

| 항목 | 상태 | 비고 |
|------|------|------|
| 시맨틱 HTML | ✅ | label 연결, 폼 요소 적절 |
| ARIA 기본 | ⚠️ | 아이콘 버튼 aria-label 누락 |
| 키보드 내비게이션 | ✅ | 28개+ 전역 단축키 구현, 입력 필드 포커스 인식 |
| 포커스 관리 | ✅ | 폼 요소 포커스 상태 지원 |
| 색상 대비 | ⚠️ | 수동 검증 필요 |
| 스크린 리더 | ❌ | aria-live, role 선언 부재 |

### 4.9 디자인 대비 미완성 UI 기능

| # | 기능 | 우선순위 | 디자인 문서 참조 | 설명 |
|---|------|----------|-----------------|------|
| 1 | **위험도/유형 분포 차트** | 중간 | 섹션 9.4 | KPI 카드만 있고 Chart.js 등 시각화 라이브러리 미적용 |
| 2 | **상세 패널 탭 UI** | 낮음 | 섹션 9.6 | Summary/Hash/JAR/XML/YAML/Rule 탭 → 현재 인라인 섹션으로 대체 |
| 3 | **gw-inspector 클래스** | 낮음 | 섹션 9.2 | 상세 패널 전용 컴포넌트 클래스 미정의 |
| 4 | **진행률 표시** | 높음 | - | 비교 실행 중 단계별 진행 상태 미표시 |
| 5 | **취소 버튼** | 높음 | - | 장시간 실행 중 취소 불가 |
| 6 | **설정 패널** | 중간 | - | 출력 폴더, 리포트 포맷, 경로 보관 수 등 |
| 7 | **다크 모드** | 중간 | - | 라이트 모드 전용 → `prefers-color-scheme` 연동 필요 |
| 8 | **드래그 앤 드롭** | 중간 | - | 경로 입력에 파일/폴더 드래그 미지원 |
| 9 | **다국어 지원** | 낮음 | - | 영어 하드코딩, 리소스 파일 미분리 |

---

## 5. NexusWorks.Guardian.Tests 리뷰

### 5.1 테스트 프레임워크

| 구성 요소 | 버전 | 역할 |
|-----------|------|------|
| xUnit | 2.5.3 | 테스트 러너 |
| FluentAssertions | 6.12.0 | 가독성 높은 어설션 |
| Microsoft.NET.Test.Sdk | 17.8.0 | 테스트 SDK |
| coverlet.collector | 6.0.0 | 코드 커버리지 |

### 5.2 테스트 커버리지 현황

| 테스트 파일 | 테스트 수 | 커버 영역 | 평가 |
|------------|----------|----------|------|
| GuardianAssemblyMarkerTests | 1 | 어셈블리 마커 | 최소 |
| StatusEvaluatorTests | 3 | 상태 평가 로직 | 기본 |
| BaselineReaderTests | 2 | Excel 베이스라인 읽기/검증 | 기본 |
| RuleResolverTests | 3 | 룰 해석 우선순위/기본값 | 양호 |
| ComparersTests | 5 | XML/JAR/YAML 비교기 | **우수** |
| GuardianComparisonEngineTests | 1 | E2E 비교 워크플로우 | 기본 |
| ReportGenerationTests | 1 | HTML/Excel/JSON/Log 생성 | 양호 |
| ExecutionHistoryStoreTests | 1 | 이력 저장/조회 | 기본 |
| PerformanceTelemetryTests | 1 | 성능 메트릭 수집 | 양호 |
| RecentPathStoreTests | 2 | MRU 경로 추적 | 양호 |
| BaselinePreviewServiceTests | 1 | 베이스라인 통계 | 기본 |
| **합계** | **21** | | |

### 5.3 강점

- **명확한 네이밍**: `Should_` 패턴으로 조건과 예상 결과 명시
- **AAA 패턴 준수**: Arrange-Act-Assert 구분 명확
- **테스트 격리**: `TestArtifactFactory`로 GUID 기반 임시 디렉토리 생성/정리
- **FluentAssertions 일관 사용**: 가독성 높은 어설션 체인
- **TestArtifactFactory**: 텍스트/JAR/Excel 파일 생성 유틸리티 우수

### 5.4 커버리지 갭 분석

#### 심각도: 높음 (프로덕션 필수)

| # | 누락 영역 | 설명 |
|---|----------|------|
| 1 | **에러 핸들링 테스트** | 손상된 베이스라인, 누락 디렉토리, 파일 권한 오류, 잘못된 경로 테스트 전무 |
| 2 | **Null/Edge Case** | null 입력, 빈 파일, 특수문자 파일명, 잘못된 Enum 값 미테스트 |
| 3 | **음성 테스트 케이스 부족** | 양성 ~80% / 음성 ~20% 비율 불균형 |

#### 심각도: 중간

| # | 누락 영역 | 설명 |
|---|----------|------|
| 4 | **베이스라인 기능 조합** | Required/Exclude 플래그 조합, 패턴 우선순위 타이 |
| 5 | **동시성 시나리오** | 병렬 비교, 동시 파일 읽기/쓰기 |
| 6 | **대규모 성능 테스트** | 1,000+ 파일, 100MB+ XML 문서 |
| 7 | **인코딩 Edge Case** | BOM 마커, UTF-8 vs UTF-16, 바이너리 혼합 |

#### 심각도: 낮음

| # | 누락 영역 | 설명 |
|---|----------|------|
| 8 | **UI 테스트** | Guardian.UI 프로젝트 테스트 코드 전무 (0%) |
| 9 | **테스트 카테고리 분류** | `[Trait]` 미사용 → Integration/Unit/Performance 구분 불가 |
| 10 | **파라미터화 테스트** | `[Theory]`/`[InlineData]` 미활용 → 조합 테스트 확장 어려움 |

### 5.5 추정 커버리지: **~45-50%** (프로덕션 권장: 80%+)

---

## 6. 디자인 문서 대비 갭 분석

`docs/nexusworks-guardian-design-plan.md` 기준으로 실제 구현과의 차이를 분석합니다.

### 6.1 도메인 모델 갭 (섹션 7.1)

| 디자인 모델 | 구현 상태 | 갭 |
|------------|----------|-----|
| `BaselineRule` | ✅ 완전 구현 | - |
| `FileInventory` (paired model) | ⚠️ 부분 | `FileInventoryEntry`로 단일 파일만 표현. 디자인의 Current/Patch 쌍 모델 부재 |
| `CompareResult` | ✅ `ComparisonItemResult`로 구현 | 이름만 다름, 필수 필드 완비 |
| `JarDetail` | ✅ `JarCompareDetail`로 확장 구현 | 패키지 요약 등 추가 필드 포함 |
| `XmlDetail` | ✅ `XmlCompareDetail`로 확장 구현 | Changes 리스트 추가 |
| `YamlDetail` | ✅ `YamlCompareDetail`로 구현 | - |
| **`ExecutionContext`** | ❌ **미구현** | Options(해시 알고리즘, 상세 비교, 제외 규칙, 출력 폴더) 구조체 부재 |

### 6.2 실행 옵션 갭 (섹션 6.1)

디자인 문서는 실행 입력에 다음 옵션을 명시하나 `ComparisonExecutionRequest`에 미포함:

| 옵션 | 디자인 요구 | 구현 |
|------|-----------|------|
| 상세 비교 사용 여부 | 섹션 6.1 | ❌ 룰 레벨에만 존재, 실행 전역 옵션 없음 |
| 제외 규칙 적용 여부 | 섹션 6.1 | ❌ 실행 전역 on/off 없음 |
| 해시 알고리즘 선택 | 섹션 6.1 | ❌ SHA-256 하드코딩 |
| 출력 폴더 지정 | 섹션 6.1 | ⚠️ 별도 파라미터로 전달 (Request에 미포함) |

### 6.3 베이스라인 검증 갭 (섹션 6.3)

| 검증 규칙 | 디자인 요구 | 구현 |
|----------|-----------|------|
| `rule_id` 중복 불가 | ✅ | `BaselineValidator`에서 검증 |
| `relative_path`와 `pattern` 둘 다 빈값 불가 | ✅ | 검증 구현됨 |
| `required`, `detail_compare`, `exclude` 허용값 검증 | ✅ | bool 파싱으로 처리 |
| **`file_type`과 `compare_mode` 조합 허용 목록** | ❌ **미구현** | YAML+jar-entry 같은 무효 조합 미차단 |

### 6.4 리포트 품질 갭 (섹션 10.3, 13.1)

| 요구사항 | 구현 |
|---------|------|
| HTML/Excel 총 건수 동일 | ✅ |
| MISSING_REQUIRED/ERROR 미숨김 | ✅ |
| 열 이름 일치 | ✅ 대부분 일치 |
| **성능 메트릭 4곳 공통 노출** (results.json, report.html, report.xlsx, execution.log) | ⚠️ HTML에는 포함, **Excel SUMMARY에 성능 테이블 분리**, Log에는 요약만 |
| **실행 로그에 예외 메시지+파일 경로** (섹션 13.2) | ❌ 요약 메트릭만 기록, 항목별 실패 미기록 |

### 6.5 추가 시트 갭 (섹션 6.2)

디자인 문서에서 권장하는 추가 베이스라인 시트:

| 시트 | 디자인 | 구현 |
|------|--------|------|
| `RULES` (필수) | 섹션 6.2 | ✅ |
| `SETTINGS` (권장) | 섹션 6.2 | ❌ 미구현 (기본 해시, 리포트 제목, 프로젝트명 등) |
| `EXCLUDES` (권장) | 섹션 6.2 | ❌ 미구현 (공통 제외 패턴, 임시 파일 패턴 등) |
| `SEVERITY_MAP` (권장) | 섹션 6.2 | ❌ 미구현 (파일 유형×상태 조합별 위험도 재정의) |

### 6.6 UI 화면 갭 요약

| 항목 | 디자인 | 구현 | 심각도 |
|------|--------|------|--------|
| 4영역 레이아웃 | 섹션 9.2 | ✅ | - |
| 28개+ 키보드 단축키 | 섹션 9.3 | ✅ 완전 구현 | - |
| KPI 카드 | 섹션 9.4 | ✅ | - |
| 위험도/유형 분포 **차트** | 섹션 9.4 | ❌ 카드만, 차트 없음 | 중간 |
| 결과 테이블 + 필터/검색 | 섹션 9.5 | ✅ | - |
| 체크박스 다중 선택 + 벌크 작업 | 섹션 9.5 | ✅ | - |
| 실행 이력 카드 (Load/Open/Delete) | 섹션 9.5 | ✅ | - |
| 상세 패널 **탭 UI** | 섹션 9.6 | ❌ 인라인 섹션 방식 | 낮음 |
| `gw-inspector` 클래스 | 섹션 9.2 | ❌ 미정의 | 낮음 |
| 상태 색상 guardian 토큰 사용 | 섹션 9.1 | ⚠️ 직접 Tailwind 색상 사용 | 낮음 |

---

## 7. 고도화 로드맵

### Phase 1: 안정성 확보 및 디자인 갭 해소 (즉시)

#### 7.1.1 보안 강화

```
[ ] 경로 순회 방어: NormalizePath()에 허용 범위 검증 추가
[ ] Silent Exception 제거: 모든 catch 블록에 로깅 추가
[ ] Zip Bomb 방어: JarComparer에 최대 엔트리/크기 제한
[ ] YAML 안전 역직렬화: SafeDeserializer 적용
[ ] 입력 경로 검증: 시스템 경로 차단 로직 추가
```

#### 7.1.2 에러 처리 개선

```
[ ] IComparisonLogger 인터페이스 도입
[ ] 전체 예외 컨텍스트 보존 (StackTrace 포함)
[ ] CancellationToken 전체 파이프라인 전파 (Baseline 읽기/검증 포함)
[ ] 타임아웃 메커니즘 추가
[ ] 실행 로그에 항목별 실패 정보 기록 (예외 메시지 + 파일 경로)
```

#### 7.1.3 디자인 갭 해소 (핵심)

```
[ ] ExecutionContext/ComparisonOptions 도입 (해시 알고리즘, 상세 비교 on/off, 제외 규칙 on/off)
[ ] BaselineValidator에 file_type+compare_mode 조합 검증 추가
[ ] Cli: UseAppHost=true + async Main + Ctrl+C graceful shutdown
[ ] Cli: 단계별 진행 상태 콘솔 출력
```

#### 7.1.4 테스트 확대

```
[ ] 에러 핸들링 테스트 20+ 케이스 추가
[ ] Null/Edge Case 테스트 15+ 케이스 추가
[ ] file_type+compare_mode 무효 조합 검증 테스트
[ ] Cli 인자 파싱 및 에러 시나리오 테스트
[ ] [Trait] 기반 테스트 카테고리 분류 (Unit/Integration/Performance)
[ ] [Theory]/[InlineData] 파라미터화 테스트 전환
[ ] 목표 커버리지: 80%+
```

### Phase 2: 아키텍처 개선 (1~2주)

#### 7.2.1 Core Library 리팩토링

```
[ ] GuardianFileComparer → ComparisonOrchestrator + ComparisonResultBuilder 분리
[ ] StaticHtmlReportWriter → StyleGenerator + DataConverter + JsGenerator 분리
[ ] 매직 스트링 → GuardianConstants 클래스로 추출
[ ] ResolvedRuleFactory → Dictionary 기반 디스패치 패턴 전환
[ ] GuardianPerformanceTuning → IPerformanceTuningStrategy 인터페이스화
[ ] ComparisonOptions record 도입 (MaxParallelism, Timeout, RedactPaths, HashAlgorithm)
```

#### 7.2.2 UI MVVM 리팩토링

```
[ ] HomeViewModel 클래스 추출 (폼 모델, 상태, 커맨드)
[ ] ViewModelBase 구현 (INotifyPropertyChanged)
[ ] ICommand 패턴 적용 (버튼 액션)
[ ] 재사용 가능 Razor 컴포넌트 추출:
    - PathInputComponent (텍스트 + 브라우즈 버튼)
    - RecentPathSelector
    - StatusBadge
[ ] 플랫폼 조건부 컴파일 → 팩토리 패턴 전환
[ ] 상태 배지 색상을 guardian 토큰으로 통일
[ ] gw-inspector 컴포넌트 클래스 정의
```

#### 7.2.3 베이스라인 확장 (디자인 권장 시트)

```
[ ] SETTINGS 시트 지원 (기본 해시 알고리즘, 리포트 제목, 프로젝트명)
[ ] EXCLUDES 시트 지원 (공통 제외 패턴, 임시 파일 패턴)
[ ] SEVERITY_MAP 시트 지원 (파일 유형×상태 조합별 위험도 재정의)
```

### Phase 3: 성능 최적화 (2~3주)

```
[ ] Regex 사전 컴파일 및 PreparedRuleSet 캐싱
[ ] 경로 정규화 1회 수행 후 결과 저장
[ ] XML 비교 정렬된 병합 알고리즘 적용
[ ] HTML 리포트 페이지네이션/AJAX 로딩
[ ] 인벤토리 스캔 배치 처리 (스트리밍)
[ ] 증분 비교 모드 (타임스탬프/해시 캐시 기반)
```

### Phase 4: UX 고도화 (3~4주)

```
[ ] 진행률 표시바 (단계별 진행 상태)
[ ] 취소 버튼 (CancellationTokenSource UI 노출)
[ ] 위험도/유형 분포 차트 (Chart.js 또는 경량 시각화 라이브러리)
[ ] 상세 패널 탭 UI 전환 (Summary/Hash/JAR/XML/YAML/Rule)
[ ] 설정 패널 (출력 폴더, 리포트 포맷, 경로 보관 수)
[ ] 다크 모드 (prefers-color-scheme 연동)
[ ] 드래그 앤 드롭 경로 입력
[ ] 접근성 강화 (aria-label, aria-live, role 선언)
```

### Phase 5: 확장 기능 (4주+)

```
[ ] 베이스라인 템플릿 자동 생성기
[ ] 리포트 필터링 (상태/심각도/경로 패턴)
[ ] Dry-Run 모드 (룰 매칭 미리보기)
[ ] 베이스라인 상속/머지 (base.xlsx → project.xlsx)
[ ] DB 기반 실행 이력 저장 (대규모 리포트 관리)
[ ] Side-by-side Diff UI
[ ] 다국어 지원 (리소스 파일 분리)
[ ] REST API + 웹 대시보드 (트렌드 분석)
[ ] Cli --verbose/--quiet 플래그 추가
```

---

## 8. 종합 평가

### 8.1 프로젝트 성숙도

| 영역 | 점수 | 평가 |
|------|------|------|
| **아키텍처 설계** | 7/10 | 레이어 분리 우수, 인터페이스 기반 DI 적용, 디자인 문서와 95%+ 일치 |
| **코드 품질** | 6/10 | Record/Immutable 설계 양호, SRP 위반 개선 필요 |
| **보안** | 4/10 | 경로 순회, Zip Bomb, Silent Exception 등 취약점 존재 |
| **성능** | 6/10 | 병렬 처리 지원, Regex/XML 최적화 필요 |
| **테스트** | 5/10 | 기본 경로 커버, 에러/엣지 케이스 대폭 부족 |
| **UI/UX** | 7/10 | 4영역 레이아웃, 28개+ 단축키, gw-* 컴포넌트 시스템 구축. MVVM 미구현 |
| **CLI** | 6/10 | 기본 기능 동작, 비동기/취소/진행 표시 부재 |
| **디자인 준수** | 8/10 | Phase 0~4 완료, 핵심 기능 모두 구현. ExecutionContext/차트/탭 UI 미구현 |
| **접근성** | 6/10 | 키보드 단축키 우수, ARIA 패턴 보강 필요 |
| **문서화** | 5/10 | 설계 문서 충실, 코드 레벨 XML 주석 부재 |

### 8.2 이슈 요약 통계

| 카테고리 | 높음 | 중간 | 낮음 | 합계 |
|----------|------|------|------|------|
| 보안 | 2 | 4 | 0 | **6** |
| 성능 | 3 | 4 | 0 | **7** |
| 코드 품질 (Core) | 3 | 5 | 0 | **8** |
| 코드 품질 (UI) | 4 | 5 | 0 | **9** |
| 코드 품질 (Cli) | 0 | 3 | 3 | **6** |
| 테스트 커버리지 | 3 | 4 | 3 | **10** |
| 디자인 갭 | 2 | 5 | 3 | **10** |
| **합계** | **17** | **30** | **9** | **56** |

### 8.3 디자인 준수 요약

```
✅ 완전 구현: 비교 엔진 (Phase 1~4), 리포트 생성, 4영역 UI, 키보드 단축키,
             Tailwind 컴포넌트 시스템, 샘플 데이터셋, 배포 스크립트, CI/CD
⚠️ 부분 구현: 실행 옵션 구조, 리포트 성능 메트릭 일관성, 상태 색상 토큰 사용
❌ 미구현:    ExecutionContext 모델, file_type/compare_mode 조합 검증,
             SETTINGS/EXCLUDES/SEVERITY_MAP 시트, 차트 시각화, 상세 패널 탭 UI
```

### 8.4 최종 판정

> **현재 상태**: Phase 4 완료, Pre-Release (v0.1.0) — 디자인 문서 기준 핵심 기능은 **95%+ 구현 완료**. 비교 엔진, 리포트 생성, 데스크톱 UI, CLI 러너 모두 동작. **보안 강화, 테스트 확대, 디자인 갭 해소**가 프로덕션 배포 전 필수.

**즉시 착수 권장 항목** (우선순위):
1. **보안**: 경로 순회 방어, Zip Bomb 제한, YAML SafeDeserializer
2. **디자인 갭**: `ExecutionContext`/`ComparisonOptions` 도입, `file_type`+`compare_mode` 조합 검증
3. **에러 처리**: Silent Exception 제거, 로깅 인프라 도입, 실행 로그 항목별 실패 기록
4. **테스트**: 커버리지 45% → 80% (에러/엣지 케이스 + Cli 테스트)
5. **UI 아키텍처**: Home.razor MVVM 분리 (1,200줄 → ViewModel + Components)
6. **Cli**: async Main + Ctrl+C graceful shutdown + 진행 상태 출력

---

*이 문서는 NexusWorks.Guardian 솔루션의 전체 코드베이스와 `docs/nexusworks-guardian-design-plan.md` 설계 문서를 대조 분석하여 작성되었습니다.*
*최초 작성: 2026-03-12 | 보완: 2026-03-13 (Cli 프로젝트 추가, 디자인 갭 분석, UI 구현 현황 정정)*

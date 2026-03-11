# NexusWorks.Guardian 프로젝트 종합 리뷰 및 고도화 검토

> 작성일: 2026-03-12
> 대상: `src/NexusWorks.Guardian`, `src/NexusWorks.Guardian.UI`, `src/NexusWorks.Guardian.Tests`
> 버전: v0.1.0 (Phase 3 UI)

---

## 1. 프로젝트 개요

### 1.1 솔루션 구조

| 프로젝트 | 타입 | 역할 |
|---------|------|------|
| **NexusWorks.Guardian** | .NET 8 Class Library | 핵심 비교 엔진, 베이스라인 룰 해석, 리포트 생성 |
| **NexusWorks.Guardian.UI** | .NET 8 MAUI + Blazor | 크로스플랫폼 데스크톱 UI (Windows/macOS) |
| **NexusWorks.Guardian.Tests** | .NET 8 xUnit | 단위/통합 테스트 |

### 1.2 핵심 기능

NexusWorks.Guardian는 **두 디렉토리 트리(Current Root vs Patch Root)를 베이스라인 룰 기반으로 비교**하여 차이점을 분석하고, HTML/Excel/JSON 형태의 리포트를 생성하는 준수성 검사 도구입니다.

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

## 3. NexusWorks.Guardian.UI (MAUI + Blazor) 리뷰

### 3.1 아키텍처

- **프레임워크**: .NET 8 MAUI + Blazor WebView 하이브리드
- **플랫폼**: Windows (WinUI 3) / macOS (Catalyst) 주력, iOS/Android/Tizen 최소 지원
- **UI 스택**: Blazor Razor 컴포넌트 + Tailwind CSS + Material Symbols

### 3.2 강점

- **모던 UI 디자인**: Tailwind CSS 기반, Inter/JetBrains Mono 폰트, 일관된 색상 팔레트
- **서비스 기반 DI**: `GuardianWorkbenchService`, `PlatformPathSelectionService` 의존성 주입
- **플랫폼 추상화**: Windows FileOpenPicker / macOS UIDocumentPickerViewController 분리
- **실시간 피드백**: 경로 유효성 Badge, 베이스라인 미리보기 통계
- **Recent Paths**: 최근 사용 경로 빠른 접근 드롭다운

### 3.3 코드 품질 이슈

#### 심각도: 높음

| # | 이슈 | 설명 |
|---|------|------|
| 1 | **MVVM 미구현** | `Home.razor`에 ~1,200줄의 로직 혼재 (폼, 검증, 파일 작업, 상태 관리). ViewModel 분리 필요 |
| 2 | **비동기 에러 처리 부재** | `RunCompareAsync` 등 비동기 작업에 try-catch 미비 |
| 3 | **플랫폼 조건부 컴파일 과다** | `#if` 블록 산재 → 팩토리 패턴 또는 구현체 분리 필요 |
| 4 | **취소 UI 미지원** | CancellationToken 전달하지만 UI에서 취소 트리거 불가 |

#### 심각도: 중간

| # | 이슈 | 설명 |
|---|------|------|
| 5 | **하드코딩 문자열** | "Recent current roots" 등 지역화 불가능한 문자열 |
| 6 | **매직 넘버** | `maxCount = 8` 등 상수 미추출 |
| 7 | **로깅 미구현** | 서비스 레이어에 로깅 프레임워크 미연동 |
| 8 | **macOS NSUrl 리소스 누수** | 보안 스코프 리소스 참조 정리 전략 미문서화 |
| 9 | **CSS 최적화 부재** | Tailwind CSS 71KB+ 미니파이 → 미사용 유틸리티 포함 |

### 3.4 MVVM 패턴 준수도: 4/10

| 항목 | 상태 | 비고 |
|------|------|------|
| DI 컨테이너 | ✅ | MauiProgram.cs에서 서비스 등록 |
| 서비스 레이어 | ✅ | GuardianWorkbenchService 분리 |
| ViewModel 클래스 | ❌ | 미구현 → Home.razor 코드비하인드에 로직 혼재 |
| INotifyPropertyChanged | ❌ | 옵저버블 패턴 미사용 |
| ICommand | ❌ | 커맨드 패턴 미사용 |
| 데이터 바인딩 일관성 | ⚠️ | @bind-Value, 수동 이벤트 핸들러 혼용 |

### 3.5 접근성 평가: 6/10

| 항목 | 상태 | 비고 |
|------|------|------|
| 시맨틱 HTML | ✅ | label 연결, 폼 요소 적절 |
| ARIA 기본 | ⚠️ | 아이콘 버튼 aria-label 누락 |
| 키보드 내비게이션 | ⚠️ | 탭 순서 기본 지원, 단축키 미구현 |
| 포커스 관리 | ✅ | 폼 요소 포커스 상태 지원 |
| 색상 대비 | ⚠️ | 수동 검증 필요 |
| 스크린 리더 | ❌ | aria-live, role 선언 부재 |

### 3.6 누락 UI 기능

| # | 기능 | 우선순위 | 설명 |
|---|------|----------|------|
| 1 | **진행률 표시** | 높음 | 비교 실행 중 진행 바/퍼센트 미표시 |
| 2 | **취소 버튼** | 높음 | 장시간 실행 중 취소 불가 |
| 3 | **인앱 리포트 뷰어** | 높음 | 외부 HTML/Excel 열기만 가능, 내장 미리보기 필요 |
| 4 | **실행 이력 패널** | 중간 | 과거 실행 목록/타임스탬프/성공 여부 표시 필요 |
| 5 | **설정 패널** | 중간 | 기본 출력 폴더, 리포트 포맷, 경로 보관 수 등 설정 |
| 6 | **다크 모드** | 중간 | 라이트 모드 전용 → 시스템 테마 연동 필요 |
| 7 | **드래그 앤 드롭** | 중간 | 경로 입력에 파일/폴더 드래그 미지원 |
| 8 | **키보드 단축키** | 낮음 | Ctrl+R (Rerun), Ctrl+L (Open HTML) 등 미구현 |
| 9 | **비교 Diff UI** | 낮음 | Side-by-side 비교 디스플레이 미구현 |
| 10 | **다국어 지원** | 낮음 | 영어 하드코딩, 리소스 파일 미분리 |

---

## 4. NexusWorks.Guardian.Tests 리뷰

### 4.1 테스트 프레임워크

| 구성 요소 | 버전 | 역할 |
|-----------|------|------|
| xUnit | 2.5.3 | 테스트 러너 |
| FluentAssertions | 6.12.0 | 가독성 높은 어설션 |
| Microsoft.NET.Test.Sdk | 17.8.0 | 테스트 SDK |
| coverlet.collector | 6.0.0 | 코드 커버리지 |

### 4.2 테스트 커버리지 현황

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

### 4.3 강점

- **명확한 네이밍**: `Should_` 패턴으로 조건과 예상 결과 명시
- **AAA 패턴 준수**: Arrange-Act-Assert 구분 명확
- **테스트 격리**: `TestArtifactFactory`로 GUID 기반 임시 디렉토리 생성/정리
- **FluentAssertions 일관 사용**: 가독성 높은 어설션 체인
- **TestArtifactFactory**: 텍스트/JAR/Excel 파일 생성 유틸리티 우수

### 4.4 커버리지 갭 분석

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

### 4.5 추정 커버리지: **~45-50%** (프로덕션 권장: 80%+)

---

## 5. 고도화 로드맵

### Phase 1: 안정성 확보 (즉시)

#### 5.1.1 보안 강화

```
[ ] 경로 순회 방어: NormalizePath()에 허용 범위 검증 추가
[ ] Silent Exception 제거: 모든 catch 블록에 로깅 추가
[ ] Zip Bomb 방어: JarComparer에 최대 엔트리/크기 제한
[ ] YAML 안전 역직렬화: SafeDeserializer 적용
[ ] 입력 경로 검증: 시스템 경로 차단 로직 추가
```

#### 5.1.2 에러 처리 개선

```
[ ] IComparisonLogger 인터페이스 도입
[ ] 전체 예외 컨텍스트 보존 (StackTrace 포함)
[ ] CancellationToken 전체 파이프라인 전파
[ ] 타임아웃 메커니즘 추가
```

#### 5.1.3 테스트 확대

```
[ ] 에러 핸들링 테스트 20+ 케이스 추가
[ ] Null/Edge Case 테스트 15+ 케이스 추가
[ ] [Trait] 기반 테스트 카테고리 분류
[ ] [Theory]/[InlineData] 파라미터화 테스트 전환
[ ] 목표 커버리지: 80%+
```

### Phase 2: 아키텍처 개선 (1~2주)

#### 5.2.1 Core Library 리팩토링

```
[ ] GuardianFileComparer → ComparisonOrchestrator + ComparisonResultBuilder 분리
[ ] StaticHtmlReportWriter → StyleGenerator + DataConverter + JsGenerator 분리
[ ] 매직 스트링 → GuardianConstants 클래스로 추출
[ ] ResolvedRuleFactory → Dictionary 기반 디스패치 패턴 전환
[ ] GuardianPerformanceTuning → IPerformanceTuningStrategy 인터페이스화
[ ] ComparisonOptions record 도입 (MaxParallelism, Timeout, RedactPaths 등)
```

#### 5.2.2 UI MVVM 리팩토링

```
[ ] HomeViewModel 클래스 추출 (폼 모델, 상태, 커맨드)
[ ] ViewModelBase 구현 (INotifyPropertyChanged)
[ ] ICommand 패턴 적용 (버튼 액션)
[ ] 재사용 가능 Razor 컴포넌트 추출:
    - PathInputComponent (텍스트 + 브라우즈 버튼)
    - RecentPathSelector
    - StatusBadge
[ ] 플랫폼 조건부 컴파일 → 팩토리 패턴 전환
```

### Phase 3: 성능 최적화 (2~3주)

```
[ ] Regex 사전 컴파일 및 PreparedRuleSet 캐싱
[ ] 경로 정규화 1회 수행 후 결과 저장
[ ] XML 비교 정렬된 병합 알고리즘 적용
[ ] HTML 리포트 페이지네이션/AJAX 로딩
[ ] 인벤토리 스캔 배치 처리 (스트리밍)
[ ] 증분 비교 모드 (타임스탬프/해시 캐시 기반)
[ ] Tailwind CSS 퍼지 설정 (미사용 클래스 제거)
```

### Phase 4: UX 고도화 (3~4주)

```
[ ] 진행률 표시바 (단계별 진행 상태)
[ ] 취소 버튼 (CancellationTokenSource UI 노출)
[ ] 인앱 리포트 뷰어 (사이드바 미리보기)
[ ] 실행 이력 패널 (타임스탬프, 소요 시간, 성공/실패)
[ ] 설정 패널 (출력 폴더, 리포트 포맷, 경로 보관 수)
[ ] 다크 모드 (prefers-color-scheme 연동)
[ ] 드래그 앤 드롭 경로 입력
[ ] 키보드 단축키 (Ctrl+R, Ctrl+L 등)
[ ] 접근성 개선 (aria-label, aria-live, role 선언)
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
```

---

## 6. 종합 평가

### 6.1 프로젝트 성숙도

| 영역 | 점수 | 평가 |
|------|------|------|
| **아키텍처 설계** | 7/10 | 레이어 분리 우수, 인터페이스 기반 DI 적용 |
| **코드 품질** | 6/10 | Record/Immutable 설계 양호, SRP 위반 개선 필요 |
| **보안** | 4/10 | 경로 순회, Zip Bomb, Silent Exception 등 취약점 존재 |
| **성능** | 6/10 | 병렬 처리 지원, Regex/XML 최적화 필요 |
| **테스트** | 5/10 | 기본 경로 커버, 에러/엣지 케이스 대폭 부족 |
| **UI/UX** | 6/10 | 모던 디자인, MVVM 미구현 및 기능 누락 |
| **접근성** | 5/10 | 기본 지원, ARIA 패턴 보강 필요 |
| **문서화** | 3/10 | XML 주석/아키텍처 문서 부재 |

### 6.2 이슈 요약 통계

| 카테고리 | 높음 | 중간 | 낮음 | 합계 |
|----------|------|------|------|------|
| 보안 | 2 | 4 | 0 | **6** |
| 성능 | 3 | 4 | 0 | **7** |
| 코드 품질 | 3 | 5 | 0 | **8** |
| 테스트 커버리지 | 3 | 4 | 3 | **10** |
| UI/UX 기능 | 3 | 4 | 3 | **10** |
| **합계** | **14** | **21** | **6** | **41** |

### 6.3 최종 판정

> **현재 상태**: Pre-Release (v0.1.0) — 핵심 비교 엔진은 기능적으로 동작하나, **보안 강화, 테스트 확대, MVVM 리팩토링**이 프로덕션 배포 전 필수적으로 완료되어야 합니다.

**즉시 착수 권장 항목**:
1. 보안 취약점 패치 (경로 순회, Zip Bomb, YAML 역직렬화)
2. Silent Exception 제거 및 로깅 인프라 도입
3. 에러 핸들링 테스트 확대 (커버리지 45% → 80%)
4. Home.razor MVVM 분리 (1,200줄 → ViewModel + Components)

---

*이 문서는 NexusWorks.Guardian 솔루션의 전체 코드베이스를 분석하여 작성되었습니다.*

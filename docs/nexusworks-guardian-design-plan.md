# NexusWorks.Guardian 설계 및 구현 계획

## 1. 문서 목적

이 문서는 `/Users/imagineiluv/Downloads/patch_compare_design_spec_ko.pdf`를 기준으로 `NexusWorks.Guardian` 프로그램을 만들기 위한 설계 방향과 구현 계획을 정리한다.

Guardian은 현재 운영 배포본과 패치 배포본을 비교해 다음을 자동 검증하는 범용 패치 검수 프로그램을 목표로 한다.

- 필수 파일 누락 여부
- 파일 변경 여부
- JAR 내부 엔트리 차이
- XML 구조 차이
- YAML 구조 차이
- 기준 문서(Baseline) 준수 여부
- 결과 리포트 및 대시보드 제공

## 2. 제품 정의

### 2.1 한 줄 정의

`NexusWorks.Guardian`은 운영본과 패치본의 차이를 기준 문서 기반으로 판정하고, HTML 대시보드와 Excel 리포트로 결과를 제공하는 데스크톱 중심 검수 프로그램이다.

### 2.2 주요 사용자

- 운영팀: 배포 전후 파일 검수
- QA: 패치 반영 범위 점검
- 개발팀: JAR/XML/YAML 변경 상세 추적
- 릴리즈 관리자: 배포 승인 판단 근거 확보

### 2.3 핵심 목표

- 비교 기준을 코드가 아니라 `baseline.xlsx`로 외부화한다.
- 상대 경로 기준으로 운영본과 패치본을 표준 방식으로 병합 비교한다.
- JAR/XML/YAML은 일반 파일보다 깊게 비교한다.
- 결과를 사람이 바로 검토할 수 있는 UI와 문서로 제공한다.
- 여러 프로젝트에 재사용 가능한 범용 검수 플랫폼 구조로 만든다.

### 2.4 비목표

- 배포 자동 수행 도구 자체를 만드는 것
- 모든 바이너리 포맷에 대한 1차 릴리즈 지원
- CI/CD 승인 워크플로우 완전 자동화
- 디컴파일 수준의 고급 JAR diff를 1차 MVP에 포함하는 것

## 3. 범위

### 3.1 1차 범위

- 운영본 루트 경로 입력
- 패치본 루트 경로 입력
- 기준 문서 경로 입력
- 파일 인벤토리 수집
- 상대 경로 기준 병합
- 공통 파일 비교
- JAR 엔트리 비교
- XML 정규화 후 구조 비교
- 상태 및 위험도 판정
- HTML 대시보드 생성
- Excel 리포트 생성

### 3.2 이후 확장 범위

- XPath before/after 상세 diff
- JAR class 수준 요약
- JSON/YAML/DLL/WAR 확장 비교기
- 배포 승인 워크플로우
- CI/CD 파이프라인 연동

## 4. 권장 솔루션 구조

현재 저장소가 .NET과 MAUI 기반이므로 Guardian도 같은 계열로 맞추는 것이 현실적이다. PDF의 요구사항은 유지하되 구현은 C# 기반으로 정리한다.

### 4.1 권장 프로젝트 구성

```text
src/
  NexusWorks.Guardian.sln
  NexusWorks.Guardian/
    NexusWorks.Guardian.csproj
  NexusWorks.Guardian.Cli/
    NexusWorks.Guardian.Cli.csproj
  NexusWorks.Guardian.UI/
    NexusWorks.Guardian.UI.csproj
  NexusWorks.Guardian.Tests/
    NexusWorks.Guardian.Tests.csproj
```

### 4.2 프로젝트 역할

- `NexusWorks.Guardian`
  - 핵심 도메인 모델
  - 기준 문서 로더
  - 파일 인벤토리 수집기
  - 룰 매핑 엔진
  - 비교 엔진
  - 상태 판정 엔진
  - 리포트 생성 서비스

- `NexusWorks.Guardian.Cli`
  - headless 실행 진입점
  - 샘플 데이터셋 실행
  - UI 없이 HTML/Excel/JSON/로그 생성 검증
  - 스크립트 및 자동화 연동용 러너

- `NexusWorks.Guardian.UI`
  - MAUI Blazor Hybrid 기반 데스크톱 UI
  - `SuperTutty.UI`를 기준으로 한 Tailwind CSS 자산 파이프라인
  - 비교 실행 화면
  - 요약 대시보드
  - 결과 목록과 상세 패널
  - 리포트 열기/저장/재실행 흐름

- `NexusWorks.Guardian.Tests`
  - 룰 매핑 테스트
  - XML 정규화/비교 테스트
  - YAML 정규화/비교 테스트
  - JAR 엔트리 비교 테스트
  - 상태 판정 테스트
  - 리포트 생성 회귀 테스트

### 4.3 NexusWorks.Guardian.UI Tailwind 기준

`NexusWorks.Guardian.UI`는 `src/SuperTutty.UI`의 자산 구조를 그대로 참고해 Tailwind를 기본 스타일 레이어로 사용한다. 새 UI는 Bootstrap 위에 Tailwind를 얹는 방식이 아니라, Tailwind를 주 스타일 시스템으로 두고 필요한 최소 글로벌 CSS만 `app.css`에 유지한다.

권장 파일 구조:

```text
NexusWorks.Guardian.UI/
  package.json
  tailwind.config.cjs
  postcss.config.cjs
  tailwind/
    input.css
  wwwroot/
    index.html
    css/
      tailwind.css
      app.css
    fonts/
      InterVariable.ttf
      InterVariable-Italic.ttf
      JetBrainsMono-Variable.ttf
      JetBrainsMono-VariableItalic.ttf
    vendor/
      material-symbols/
```

기준 원칙:

- `package.json`은 `SuperTutty.UI`처럼 `tailwindcss`, `@tailwindcss/forms`, `@tailwindcss/typography`, `@tailwindcss/container-queries`, `postcss`, `autoprefixer`를 사용한다.
- 빌드 스크립트는 `npm run tailwind:build` 단일 명령으로 유지한다.
- `tailwind.config.cjs`의 `content` 범위는 `./**/*.razor`, `./**/*.cshtml`, `./wwwroot/index.html`을 기본으로 한다.
- `tailwind/input.css`에는 `@tailwind base`, `@tailwind components`, `@tailwind utilities`를 두고, Guardian 전용 재사용 클래스는 `@layer components`에 정의한다.
- `wwwroot/css/app.css`에는 폰트 선언, WebView 전역 보정, 스크롤바/포커스/에러 영역 같은 예외 스타일만 둔다.
- `wwwroot/index.html`은 `tailwind.css`를 먼저, `app.css`를 그 다음에 로드한다.
- 새 Razor 컴포넌트는 Bootstrap 클래스를 기본으로 사용하지 않는다. Bootstrap은 초기 호환성 때문에 남길 수는 있지만 Guardian 신규 화면은 Tailwind만으로 작성한다.

### 4.4 Guardian.UI 테마 확장 기준

`SuperTutty.UI`의 Tailwind 기본 설정은 비어 있으므로, Guardian에서는 `theme.extend`에 명시적으로 토큰을 추가한다.

권장 확장 항목:

- `fontFamily`
  - `sans`: `Inter`
  - `mono`: `JetBrains Mono`
- `colors`
  - `guardian-ink`
  - `guardian-panel`
  - `guardian-canvas`
  - `guardian-line`
  - `guardian-primary`
  - `guardian-success`
  - `guardian-warning`
  - `guardian-danger`
- `boxShadow`
  - 카드 그림자
  - 플로팅 패널 그림자
- `borderRadius`
  - `xl`, `2xl` 중심
- `maxWidth`
  - 대시보드와 상세 패널 폭 토큰
- `gridTemplateColumns`
  - 목록/상세 2단 레이아웃용 토큰

## 5. 핵심 아키텍처

PDF 요구사항을 기준으로 Guardian 내부 모듈은 아래 순서로 분리한다.

| 순서 | 모듈 | 역할 |
|---|---|---|
| 1 | Baseline Loader | `baseline.xlsx`를 읽어 비교 규칙을 메모리에 적재 |
| 2 | Inventory Scanner | 운영본/패치본을 재귀 스캔하고 메타데이터를 수집 |
| 3 | Rule Resolver | 경로, 패턴, 확장자 기반으로 파일별 적용 규칙 결정 |
| 4 | Compare Engine | 공통, JAR, XML, YAML, Binary/Text 비교 수행 |
| 5 | Status Evaluator | 최종 상태와 위험도를 판정 |
| 6 | Result Aggregator | 화면 표시용 결과와 리포트용 데이터셋 구성 |
| 7 | Report Generator | HTML 대시보드와 Excel 파일 생성 |

### 5.1 권장 내부 폴더 구조

```text
NexusWorks.Guardian/
  Abstractions/
  Baseline/
  Inventory/
  RuleResolution/
  Comparison/
    Common/
    Jar/
    Xml/
  Evaluation/
  Reporting/
    Html/
    Excel/
  Models/
  Orchestration/
```

### 5.2 핵심 서비스 계약

구현 단계에서 모듈 결합도를 낮추기 위해 주요 책임은 인터페이스 기준으로 분리한다.

| 인터페이스 | 책임 |
|---|---|
| `IBaselineReader` | Excel 기준 문서를 읽고 `BaselineRule` 집합으로 변환 |
| `IBaselineValidator` | 기준 문서의 형식, 필수 컬럼, 값 유효성 검증 |
| `IInventoryScanner` | 루트 경로를 재귀 스캔해 `FileInventory` 후보 생성 |
| `IRuleResolver` | 파일별 적용 규칙 계산 |
| `IHashProvider` | SHA-256 등 해시 계산 |
| `IFileComparer` | 공통 비교 진입점 |
| `IJarComparer` | JAR 엔트리 상세 비교 |
| `IXmlComparer` | XML 정규화 및 구조 비교 |
| `IYamlComparer` | YAML 정규화 및 구조 비교 |
| `IStatusEvaluator` | 상태와 위험도 판정 |
| `IResultAggregator` | UI/리포트용 집계 데이터 생성 |
| `IHtmlReportWriter` | HTML 대시보드 출력 |
| `IExcelReportWriter` | Excel 리포트 출력 |
| `IExecutionHistoryStore` | 실행 이력 저장 및 재조회 |

권장 규칙:

- UI는 `NexusWorks.Guardian`의 서비스만 호출하고 직접 파일 시스템 비교 로직을 가지지 않는다.
- 비교 엔진은 UI 프레임워크 참조 없이 순수 라이브러리로 유지한다.
- 리포트 생성기는 `CompareResult`와 상세 모델만 받아 동작해야 한다.

## 6. 기능 설계

### 6.1 입력

- Current root path
- Patch root path
- Baseline path
- 옵션
  - 상세 비교 사용 여부
  - 제외 규칙 적용 여부
  - 해시 알고리즘
  - 출력 폴더

### 6.2 기준 문서 설계

기준 문서는 `baseline.xlsx`를 기본 포맷으로 한다. 최소 `RULES` 시트를 포함한다.

| 컬럼명 | 필수 | 설명 |
|---|---|---|
| `rule_id` | Y | 규칙 식별자 |
| `relative_path` | N | 정확한 비교 경로 |
| `pattern` | N | 와일드카드 경로 규칙 |
| `file_type` | Y | `JAR`, `XML`, `YAML`, `AUTO` |
| `required` | Y | 필수 파일 여부 |
| `compare_mode` | Y | `hash`, `xml-structure`, `yaml-structure`, `jar-entry` 등 |
| `detail_compare` | N | 상세 비교 여부 |
| `exclude` | N | 비교 제외 여부 |
| `priority` | N | 정책 우선순위 |
| `notes` | N | 운영 메모 |

권장 추가 시트:

- `SETTINGS`
  - 기본 해시 알고리즘
  - 리포트 제목
  - 프로젝트명
  - 기본 제외 정책
- `EXCLUDES`
  - 공통 제외 패턴
  - 임시 파일 패턴
  - 로그/캐시 폴더 규칙
- `SEVERITY_MAP`
  - 파일 유형과 상태 조합별 위험도 재정의

### 6.3 룰 적용 우선순위

1. `relative_path` 정확 일치
2. `pattern` 일치
3. 확장자 기반 기본 룰
4. 시스템 기본값

Baseline 유효성 규칙:

- `rule_id`는 중복되면 안 된다.
- `relative_path`와 `pattern`은 둘 다 비워둘 수 없다.
- `required`, `detail_compare`, `exclude`는 허용된 값만 사용한다.
- `file_type`과 `compare_mode` 조합은 미리 정의한 허용 목록 안에 있어야 한다.
- 로더는 오류가 있는 기준 문서를 경고만 하고 진행하지 않는다. 실행 전 단계에서 실패 처리한다.

### 6.4 파일 유형별 비교

#### 공통 비교

- 존재 여부
- 파일 크기
- 수정 시각
- SHA-256 해시

#### JAR 비교

- 1단계: 존재 여부, 크기, 해시 비교
- 2단계: zip 엔트리 목록 비교
- 3단계: `META-INF`, class, resource 분리 집계
- 4단계: 추후 플러그인 방식으로 class/public API diff 확장

#### XML 비교

- 원문 해시 비교
- XML 파싱 가능 여부 확인
- 공백, 주석, 속성 순서 정규화
- 노드 추가/삭제/속성/텍스트 변경 추적
- 주요 XPath 목록 생성

#### YAML 비교

- 원문 해시 비교
- YAML 파싱 가능 여부 확인
- 공백, 주석, 들여쓰기 차이로 인한 노이즈 제거
- key 순서 정규화 후 map/list/scalar 단위 비교
- 추가/삭제/변경 path 추적
- path 표기는 `root.services[0].name` 형태를 기본으로 사용
- YAML 비교 기능은 현재 완료 범위가 아니라 `Phase 4`에서 구현한다.

### 6.5 실행 산출 데이터 규약

앱 내부와 리포트 사이의 데이터 이동은 화면용 임시 객체가 아니라 명시적 실행 결과 모델로 통일한다.

권장 결과 집합:

- `ExecutionSummary`
  - 실행 ID
  - 시작/종료 시각
  - 총 파일 수
  - 상태별 건수
  - 위험도별 건수
- `ExecutionItem`
  - 상대 경로
  - 파일 유형
  - 적용 규칙
  - 상태
  - 위험도
  - 요약 메시지
- `ExecutionArtifacts`
  - HTML 경로
  - Excel 경로
  - 로그 경로
  - JSON 캐시 경로

### 6.6 출력 폴더 및 파일명 규약

실행 결과는 사람이 찾기 쉬운 구조로 저장한다.

```text
output/guardian/
  20260311-213000/
    report.html
    report.xlsx
    results.json
    logs/
      execution.log
```

규칙:

- 실행 폴더명은 `yyyyMMdd-HHmmss` 형식을 기본으로 한다.
- 동일 시각 충돌 방지를 위해 필요 시 실행 ID를 뒤에 붙인다.
- UI는 최근 실행 폴더를 바로 열 수 있어야 한다.
- `results.json`은 추후 UI 재열기, 실행 이력, 회귀 테스트의 기준 데이터로 활용한다.

## 7. 도메인 모델

### 7.1 주요 모델

| 모델 | 핵심 필드 |
|---|---|
| `BaselineRule` | `RuleId`, `RelativePath`, `Pattern`, `FileType`, `Required`, `CompareMode`, `DetailCompare`, `Exclude` |
| `FileInventory` | `RelativePath`, `CurrentExists`, `PatchExists`, `CurrentHash`, `PatchHash`, `CurrentSize`, `PatchSize` |
| `CompareResult` | `RelativePath`, `Status`, `Severity`, `CompareMode`, `ChangedSummary`, `Messages` |
| `JarDetail` | `AddedEntries`, `RemovedEntries`, `ChangedEntries`, `ManifestChanged` |
| `XmlDetail` | `ChangedXPaths`, `ChangedNodeCount`, `AddedNodes`, `RemovedNodes` |
| `YamlDetail` | `ChangedPaths`, `ChangedNodeCount`, `AddedKeys`, `RemovedKeys` |
| `ExecutionContext` | `CurrentRoot`, `PatchRoot`, `BaselinePath`, `OutputPath`, `Options` |

### 7.2 상태 모델

| 상태 | 위험도 | 설명 |
|---|---|---|
| `OK` | `LOW` | 양쪽 존재하고 비교 결과 동일 |
| `CHANGED` | `MEDIUM` or `HIGH` | 양쪽 존재하나 차이 존재 |
| `ADDED` | `LOW` | 패치본에만 존재 |
| `REMOVED` | `MEDIUM` | 운영본에만 존재 |
| `MISSING_REQUIRED` | `CRITICAL` | 기준상 필수 파일이 누락 |
| `ERROR` | `HIGH` | 비교 중 예외 발생 |

## 8. 처리 흐름

1. 사용자가 비교 실행 화면에서 경로와 옵션을 입력한다.
2. Baseline Loader가 기준 문서를 읽고 유효성을 검증한다.
3. Inventory Scanner가 운영본/패치본을 재귀 스캔한다.
4. 상대 경로 기준으로 두 인벤토리를 병합한다.
5. Rule Resolver가 각 파일에 적용할 정책을 결정한다.
6. Compare Engine이 파일 유형에 맞는 비교를 수행한다.
7. Status Evaluator가 최종 상태와 위험도를 계산한다.
8. Result Aggregator가 UI 표시용 데이터를 집계한다.
9. Report Generator가 HTML/Excel 산출물을 만든다.
10. UI가 요약, 목록, 상세 패널을 표시한다.

## 9. UI 설계 방향

PDF의 화면 예상안을 제품 흐름으로 반영하되, 실제 앱 스타일은 `SuperTutty.UI`의 Tailwind 자산 구조를 계승한 Guardian 전용 검수 콘솔로 설계한다.

### 9.1 Guardian 시각 방향

Guardian은 단순 문서형 화면이 아니라 "패치 검수 관제 콘솔"처럼 보여야 한다. 따라서 PDF의 표 기반 정보 구조와 `SuperTutty.UI`의 앱 셸 감각을 결합한다.

시각 원칙:

- 상단 바와 보조 내비게이션은 짙은 슬레이트 계열로 처리한다.
- 실제 데이터가 보이는 메인 캔버스는 밝은 회색 또는 백색 패널로 구성한다.
- 상태 색은 의미를 분명히 구분한다.
  - `OK`: emerald
  - `CHANGED`: amber
  - `ADDED`: blue
  - `REMOVED`: slate
  - `MISSING_REQUIRED`: red
  - `ERROR`: magenta-red
- 일반 텍스트는 `Inter`, 파일 경로/해시/XPath/엔트리명은 `JetBrains Mono`를 사용한다.
- 아이콘은 `SuperTutty.UI`와 동일하게 `Material Symbols Outlined`를 사용한다.
- 카드, 테이블, 상세 패널은 둥근 모서리와 얇은 보더를 사용하되 과한 글래스모피즘은 피한다.

권장 색상 토큰 예시:

| 토큰 | 권장값 | 용도 |
|---|---|---|
| `guardian-ink` | `#0F172A` | 상단 바, 강조 텍스트 |
| `guardian-canvas` | `#F3F5F7` | 앱 배경 |
| `guardian-panel` | `#FFFFFF` | 카드/패널 배경 |
| `guardian-line` | `#D7DEE7` | 패널 경계선 |
| `guardian-primary` | `#245DFF` | 실행 버튼, 선택 상태 |
| `guardian-success` | `#1F9D55` | 정상 상태 |
| `guardian-warning` | `#C98300` | 변경 경고 |
| `guardian-danger` | `#C93B3B` | 치명 이슈 |

### 9.2 Tailwind 컴포넌트 규칙

Guardian은 Tailwind 유틸리티만 남발하지 않고, 자주 반복되는 패턴을 `@layer components`로 묶는다.

권장 재사용 클래스:

- `gw-shell`
  - 전체 앱 프레임
- `gw-toolbar`
  - 상단 실행/필터 바
- `gw-panel`
  - 공통 카드/패널
- `gw-kpi-card`
  - 요약 대시보드 카드
- `gw-badge`
  - 상태/유형 배지
- `gw-table`
  - 결과 목록 테이블
- `gw-inspector`
  - 상세 패널
- `gw-code`
  - 파일 경로, 해시, XPath, 엔트리명 표시

레이아웃 규칙:

- 데스크톱 기본 레이아웃은 `상단 실행 바 + 요약 KPI + 결과 목록 + 우측 상세 패널` 4영역으로 고정한다.
- `@tailwindcss/container-queries`를 활용해 카드 영역과 우측 상세 패널 내부 레이아웃을 반응형으로 조정한다.
- 폭이 좁아지면 우측 상세 패널은 슬라이드오버 또는 하단 드로어로 전환한다.
- 모바일 퍼스트보다는 데스크톱 퍼스트로 설계하되, 최소 폭에서도 사용 불가능한 고정 폭은 피한다.

### 9.3 화면 1: 비교 실행

- 운영본 경로 입력
- 패치본 경로 입력
- 기준 문서 경로 입력
- 옵션 선택
- 실행 버튼
- 최근 실행 이력

추가 UI 규칙:

- 세 입력값은 단순 텍스트박스가 아니라 큰 입력 카드로 묶는다.
- 경로 선택 버튼, 최근 경로 드롭다운, 유효성 상태를 한 줄에 배치한다.
- 경로 선택 버튼은 macOS/Windows 네이티브 폴더/파일 picker와 연결한다.
- 각 경로 필드는 유효한 경우 바로 `Open` 액션으로 Finder/Explorer에서 열 수 있어야 한다.
- 최근 경로는 필드별로 최신순 6건까지 저장하고 재선택할 수 있어야 한다.
- 유효성 상태는 폴더/파일 존재 여부와 Baseline 파싱 성공 여부를 구분해 표시한다.
- 출력 경로는 부모 폴더가 존재하면 `Will create` 상태로 통과시키고, 실행 버튼도 이 상태를 허용한다.
- Baseline 파일이 로드되면 규칙 수, 필수 파일 수, 제외 규칙 수를 미리 보여준다.
- 실행 버튼은 보고서 제목, 운영본, 패치본, Baseline, 출력 경로가 모두 준비되기 전까지 비활성화한다.
- 실행 전 `Run Readiness` 카드에서 막힌 입력 항목을 직접 안내한다.
- 개발 워크스페이스에서 `sample/guardian`가 존재하면 `Load sample data`와 `Open sample guide` 액션을 노출한다.
- `scripts/run-guardian-sample.sh`, `scripts/run-guardian-sample.ps1`는 `NexusWorks.Guardian.Cli`를 통해 샘플 실행 산출물을 만든다.
- 실행, 샘플 로드, 이력 새로고침/불러오기/삭제, 다중 파일 열기 같은 수동 액션은 에러와 분리된 성공/정보 배너로 결과를 즉시 피드백하고, 배너는 수동 닫기와 자동 소거를 모두 지원한다.
- 실행 화면은 전역 단축키를 제공한다. `Ctrl/Cmd+Enter`는 실행, `Ctrl/Cmd+Shift+Enter`는 재실행, `Alt+Shift+H`는 이력 새로고침, `Alt+Shift+S`는 샘플 경로 로드를 호출한다. `[`와 `]`는 실행 이력 선택을 이동시키고 `M`은 선택된 이력 항목을 불러온다. `/`는 결과 검색창에 포커스를 준다. `1`~`5`는 상태 필터를 전환하고, `J/K`와 `N/P`는 필터링된 결과 목록에서 현재 선택을 이동시킨다. `X`는 현재 선택 행의 bulk selection을 토글하고, `Alt+Shift+A/R/C`는 visible 선택, review set 선택, bulk selection 초기화를 수행한다. `O`와 `Shift+O`는 선택된 항목의 current/patch 파일을 열고, `Alt+Shift+O/P`는 bulk-selected current/patch 파일을 연다. `H/E/D/L/U`는 현재 실행의 HTML, Excel, JSON, Log, Output 산출물을 연다. `?`는 단축키 도움말 오버레이를 열고 `Esc`는 오버레이를 닫는다.

### 9.4 화면 2: 요약 대시보드

- 전체 파일 수
- 상태별 건수 카드
- 위험도 분포 차트
- 파일 유형별 분포 차트
- 필수 파일 누락 경고 영역

추가 UI 규칙:

- KPI 카드는 단순 숫자만 두지 말고 직전 실행 대비 증감, 위험도 강조선, 상태 아이콘을 포함한다.
- `MISSING_REQUIRED`와 `ERROR`는 상단 고정 경고 영역에 다시 노출한다.
- 차트 영역은 밝은 카드 안에 넣고, 카드 헤더에 바로 필터 연결 버튼을 둔다.

### 9.5 화면 3: 결과 목록

- 상태 필터
- 위험도 필터
- 파일 유형 필터
- 검색
- 정렬
- 상세 보기 진입

추가 UI 규칙:

- 목록은 밀도 높은 테이블과 카드형 리스트를 모두 고려하되, 데스크톱 기본값은 테이블로 둔다.
- 각 행에는 상대 경로, 상태 배지, 파일 유형, 기준 규칙, 요약 메시지를 노출한다.
- 결과 목록은 체크박스 다중 선택을 지원하고, 선택된 항목의 현재본/패치본 파일을 일괄로 열 수 있어야 한다.
- 행 선택 시 우측 상세 패널이 즉시 갱신되며, 더블클릭 없이 한 번의 선택만으로 검토 가능해야 한다.
- 실행 이력 카드에서는 이전 실행을 `Load`할 뿐 아니라 HTML, Excel, 출력 폴더를 바로 열 수 있어야 한다.
- 실행 이력 카드에서는 JSON 결과와 실행 로그도 바로 열 수 있어야 한다.
- 실행 이력 삭제는 2단계 확인 후 해당 실행 산출 폴더만 제거해야 한다.

### 9.6 화면 4: 상세 패널

- 기본 메타데이터
- 해시 값 비교
- 적용된 기준 규칙
- JAR 엔트리 차이
- XML XPath 차이
- YAML path 차이
- 판정 메시지

추가 UI 규칙:

- 상단에는 상태 배지, 위험도, 상대 경로를 고정한다.
- 중단에는 비교 결과 탭을 둔다.
  - `Summary`
  - `Hash`
  - `JAR`
  - `XML`
  - `YAML`
  - `Rule`
- 해시, XPath, YAML path, 엔트리 목록은 전부 `font-mono`와 복사 버튼을 제공한다.
- before/after 성격의 값은 2열 비교 레이아웃을 사용한다.
- 상세 패널에서는 선택 항목의 현재본 파일과 패치본 파일을 각각 바로 열 수 있어야 한다.

## 10. 리포트 산출물 설계

### 10.1 HTML 대시보드

- 요약 카드
- 상태/위험도 차트
- 검색 가능한 결과 표
- 선택 파일 상세 패널
- Guardian UI와 동일한 색상 토큰 및 상태 배지 체계 재사용
- 단순 HTML 테이블 나열이 아니라 운영 검토용 정적 검수 페이지 형태로 생성

### 10.2 Excel 산출물

- `SUMMARY`
  - 전체 건수
  - 상태별 건수
  - 유형별 건수
- `DETAIL`
  - 파일별 결과
  - 상태
  - 비교 방식
  - 메시지
- `JAR_DETAIL`
  - 추가/삭제/변경 엔트리
  - Manifest 변경 여부
- `XML_DETAIL`
  - 변경 XPath
  - 추가/삭제 노드 수
  - 변경 노드 수
- `YAML_DETAIL`
  - 변경 path
  - 추가/삭제 key 수
  - 변경 node 수

### 10.3 리포트 품질 기준

- HTML과 Excel의 총 건수는 항상 동일해야 한다.
- `MISSING_REQUIRED`와 `ERROR`는 어느 산출물에서도 숨기지 않는다.
- HTML 표와 Excel 시트의 열 이름은 최대한 일치시켜 운영자가 문서를 오가며 혼동하지 않게 한다.
- 상대 경로, 상태, 위험도, 규칙 ID는 모든 산출물에서 공통 열로 유지한다.
- JAR/XML/YAML 상세 정보가 존재할 때 상세 산출 형식은 일관된 열 구조를 유지한다.
- 리포트 생성 실패는 부분 성공으로 처리하지 않고 실행 실패로 기록한다.

## 11. 구현 계획

### 11.1 반복 개발 프로토콜

모든 계획 항목은 아래 순서를 강제한다.

1. 고도화 체크
2. 개발
3. 테스트
4. 리뷰
5. 완료 체크 후 다음 항목 진행

운영 규칙:

- 각 Phase는 먼저 "고도화 체크"를 수행해 설계 누락, 의존성, 영향 범위, 테스트 가능성을 다시 확인한다.
- 고도화 체크에서 빠진 설계가 발견되면 코드를 먼저 작성하지 않고 본 문서를 먼저 갱신한다.
- 개발이 끝나면 해당 Phase 범위에 맞는 테스트를 반드시 수행한다.
- 테스트가 끝나면 변경 파일, 동작, 회귀 가능성을 리뷰한다.
- 테스트 또는 리뷰에서 문제를 발견하면 해당 Phase 안에서 다시 `개발 -> 테스트 -> 리뷰`를 반복한다.
- 테스트와 리뷰에서 모두 문제 없을 때만 체크박스를 완료 처리한다.
- 상위 체크가 끝나지 않은 다음 Phase는 진행하지 않는다.

체크 상태 규칙:

- `[ ]` 미완료
- `[x]` 완료

### 11.2 Phase 0 - 프로젝트 정리

- `SuperTutty` 기반 이름을 `NexusWorks.Guardian` 체계로 정리
- 새 솔루션과 프로젝트 골격 생성
- 공통 빌드/테스트 설정 정리
- `SuperTutty.UI` 기준 Tailwind 자산 파이프라인 이식
- macOS/Windows용 build-publish 스크립트 추가

완료 기준:

- `src/NexusWorks.Guardian.sln` 생성
- 핵심 프로젝트와 테스트 프로젝트가 빌드 가능
- `NexusWorks.Guardian.UI`에서 `npm run tailwind:build`로 CSS 산출 가능
- `scripts/publish-mac.sh`, `scripts/publish-windows.ps1`로 플랫폼별 배포 아티팩트 생성 가능

진행 체크리스트:

- [x] Phase 0 / 고도화 체크
- [x] Phase 0 / 개발
- [x] Phase 0 / 테스트
- [x] Phase 0 / 리뷰
- [x] Phase 0 / 완료 체크

### 11.3 Phase 1 - 비교 엔진 MVP

- Baseline Loader 구현
- 파일 인벤토리 수집기 구현
- Rule Resolver 구현
- 공통 비교, JAR 엔트리 비교, XML 정규화 비교 구현
- 상태 판정 구현

완료 기준:

- 샘플 데이터셋으로 `OK`, `CHANGED`, `ADDED`, `REMOVED`, `MISSING_REQUIRED` 판정 가능
- 테스트 프로젝트에서 핵심 로직 검증 가능

진행 체크리스트:

- [x] Phase 1 / 고도화 체크
- [x] Phase 1 / 개발
- [x] Phase 1 / 테스트
- [x] Phase 1 / 리뷰
- [x] Phase 1 / 완료 체크

### 11.4 Phase 2 - 리포트 MVP

- HTML 대시보드 생성기 구현
- Excel 생성기 구현
- 출력 폴더 구조 정리

완료 기준:

- 단일 실행으로 HTML과 Excel 둘 다 생성
- 운영자가 결과를 파일로 검토 가능

진행 체크리스트:

- [x] Phase 2 / 고도화 체크
- [x] Phase 2 / 개발
- [x] Phase 2 / 테스트
- [x] Phase 2 / 리뷰
- [x] Phase 2 / 완료 체크

### 11.5 Phase 3 - 데스크톱 UI

- 비교 실행 화면 구성
- 대시보드/결과 목록/상세 패널 연결
- 리포트 열기 및 재실행 UX 정리
- Guardian 전용 Tailwind 토큰, 재사용 컴포넌트 클래스, 상태 배지 체계 정리

완료 기준:

- UI에서 경로 입력부터 결과 확인까지 한 흐름으로 수행 가능
- 신규 Razor 화면이 Bootstrap 의존 없이 Tailwind 중심으로 작성됨

진행 체크리스트:

- [x] Phase 3 / 고도화 체크
- [x] Phase 3 / 개발
- [x] Phase 3 / 테스트
- [x] Phase 3 / 리뷰
- [x] Phase 3 / 완료 체크

### 11.6 Phase 4 - 고도화

- XPath 상세 diff
- YAML path 상세 diff
- JAR class 수준 요약
- 추가 포맷 확장
- 성능 개선
- 실행 이력 관리

완료 기준:

- 고급 diff와 확장 포맷이 기존 리포트 구조를 깨지 않고 통합됨
- 성능 개선 결과가 측정값으로 확인됨
- 실행 이력 조회가 UI와 산출물 구조에 반영됨

진행 체크리스트:

- [x] Phase 4 / 고도화 체크
- [x] Phase 4 / 개발
- [x] Phase 4 / 테스트
- [x] Phase 4 / 리뷰
- [x] Phase 4 / 완료 체크

세부 진행 메모:

- [x] YAML 구조 비교기와 path 추적 1차 구현
- [x] YAML 리포트 상세 산출물과 UI 상세 패널 연결
- [x] XPath 상세 diff 고도화
- [x] JAR class 수준 요약
- [x] 추가 포맷 확장
- [x] 성능 개선
- [x] 실행 이력 관리

## 12. 기술 선택 권장안

### 12.1 백엔드/엔진

- .NET 8
- C#
- `System.IO.Compression` 기반 JAR(zip) 엔트리 분석
- XML 비교는 `XDocument` 또는 `XmlDocument` 기반 정규화 후 비교
- YAML 비교는 `YamlDotNet` 기반 파싱 후 정규화 비교 권장
- Excel은 `ClosedXML` 또는 `EPPlus` 계열 검토

### 12.2 UI

- 기존 저장소 흐름을 고려해 `net8.0` 기반 MAUI Blazor Hybrid 권장
- 스타일 시스템은 `SuperTutty.UI`와 같은 Tailwind CSS 3.4 계열 파이프라인 사용
- `Inter` + `JetBrains Mono` + `Material Symbols` 조합을 기본 자산으로 사용
- 초기에는 데스크톱 전용으로 시작
- 필요 시 추후 웹 뷰어 분리 가능

### 12.3 리포트

- HTML: 정적 템플릿 기반 생성
- Excel: 시트 분리와 필터, 조건부 서식 포함

### 12.4 권장 패키지 검토 목록

- 핵심
  - `ClosedXML` 또는 `EPPlus`
  - `DocumentFormat.OpenXml`
  - `YamlDotNet`
- 테스트
  - `xunit`
  - `FluentAssertions`
  - `Verify.Xunit` 또는 스냅샷 비교 도구
- UI
  - Tailwind CSS 3.4 계열
  - `@tailwindcss/forms`
  - `@tailwindcss/typography`
  - `@tailwindcss/container-queries`

## 13. 비기능 요구사항

### 13.1 성능

- 1차 MVP는 "수천 개 파일, 수백 개 JAR/XML" 규모에서도 실무 사용이 가능해야 한다.
- YAML 비교가 추가되는 시점에도 기존 JAR/XML 처리량을 크게 훼손하지 않아야 한다.
- 전체 파일에 대해 무조건 상세 비교하지 않고, 존재 여부와 해시 차이가 있는 파일만 2차 상세 비교한다.
- UI 스레드를 막는 동기 비교는 금지하고 백그라운드 작업으로 실행한다.
- `Current Inventory Scan`, `Patch Inventory Scan`, `Candidate Compare`, `Missing Required Backfill` 단계의 처리 시간과 처리량을 기록한다.
- 성능 측정값은 `results.json`, `report.html`, `report.xlsx`, `logs/execution.log`에 공통 노출한다.

### 13.2 안정성

- 단일 파일 비교 실패가 전체 프로세스를 바로 중단시키지 않도록 item 단위 예외를 수집한다.
- 다만 기준 문서 로딩 실패, 출력 폴더 생성 실패, 리포트 생성 실패는 실행 자체 실패로 처리한다.
- 실행 로그는 예외 메시지와 대상 파일 경로를 반드시 포함한다.

### 13.3 관측성

- 최소 로그 레벨은 `Information`으로 유지한다.
- 주요 이벤트는 `ExecutionStarted`, `BaselineLoaded`, `ComparisonCompleted`, `ReportGenerated`, `ExecutionFailed` 단위로 남긴다.
- UI에서도 최근 실행 결과와 실패 원인을 볼 수 있어야 한다.

### 13.4 사용성

- 운영자 기준으로 첫 실행부터 결과 확인까지 3단계 이내 흐름을 유지한다.
- 파일 경로, 해시, XPath, YAML path는 복사 가능해야 한다.
- 위험도 높은 항목은 목록과 상세 패널에서 모두 중복 강조한다.

### 13.5 호환성

- 현재 저장소 기준으로 `net8.0`과 MAUI Blazor Hybrid를 우선 지원한다.
- 파일 경로 처리는 Windows와 macOS를 모두 고려한다.
- 경로 구분자 차이와 대소문자 민감도 차이를 테스트 케이스에 포함한다.

## 14. 테스트 전략

- 단위 테스트
  - 룰 우선순위
  - XML 정규화
  - YAML 정규화
  - JAR 엔트리 변경 탐지
  - 상태 판정
- 통합 테스트
  - 샘플 운영본/패치본/기준 문서 조합 실행
  - HTML/Excel 산출물 생성 확인
- 회귀 테스트
  - 실제 운영 샘플셋 기준 결과 스냅샷 비교

샘플 데이터셋:

- `sample/guardian/current`
- `sample/guardian/patch`
- `sample/guardian/baseline.xlsx`
- `sample/guardian/output`
- `sample/guardian/README.md`

샘플 데이터셋은 XML, YAML, JAR, 일반 텍스트, 필수 누락, 추가/삭제 파일 시나리오를 모두 포함하고 테스트 코드에서 직접 참조한다.

추가 권장 테스트:

- Baseline 오류 문서 테스트
- 경로 충돌 및 대소문자 차이 테스트
- 빈 폴더, 권한 오류, 잠긴 파일 테스트
- 대용량 샘플셋 성능 스모크 테스트

최근 완료 메모:

- 인벤토리 스캔은 병렬 해시 계산으로 최적화했다.
- 룰 해석은 exact/pattern 캐시로 반복 정규화 비용을 줄였다.
- 비교 엔진은 정렬된 후보 경로를 병렬 비교하면서 결과 순서를 유지한다.
- 성능 스모크 테스트로 단계별 메트릭 생성과 처리량 기록을 검증했다.
- `sample/guardian` 데이터셋과 연동된 통합 테스트로 대표 상태값과 정규화 비교 결과를 고정했다.
- 샘플 데이터셋 기반 리포트 생성 테스트로 HTML, Excel, JSON, 로그 산출까지 고정했다.
- `src/NexusWorks.Guardian.UI/tests/guardian-hotkey-map.test.js`와 `npm run test:hotkeys`로 전역 단축키 매핑 회귀를 자동 검증한다.
- `scripts/publish-mac.sh`와 `scripts/publish-windows.ps1`는 publish 전에 `npm run test:hotkeys`를 실행해 UI 단축키 회귀를 막는다.
- `scripts/publish-mac.sh`는 `.NET 8.x` SDK를 전제로 `maccatalyst-arm64` publish와 launch smoke test를 수행해 깨진 macOS release 산출물을 조기에 차단한다.
- `scripts/publish-windows.ps1`는 `.NET 8.x` SDK guard와 커스텀 `dotnet` 경로(`-DotnetExe`)를 지원한다.
- `.github/workflows/guardian-release-validation.yml`는 `macos-14`와 `windows-latest` runner에서 release wrapper를 실제 실행해 로컬 OS 제약으로 남던 검증 공백을 메운다.
- 같은 workflow는 Apple signing secret이 준비되면 signed/notarized macOS release도 같은 래퍼 경로로 검증한다.
- 저장소 루트 `global.json`은 macOS 배포용 SDK를 `8.0.416`으로 고정한다.
- `scripts/check-mac-dotnet8-prereqs.sh`는 `.NET 8` `maccatalyst` workload 설치 여부를 사전 점검한다.
- `scripts/check-windows-dotnet8-prereqs.ps1`는 `.NET 8` `maui` workload 설치 여부를 사전 점검한다.
- `scripts/install-local-dotnet8-maccatalyst.sh`는 시스템 SDK 권한이 없을 때 사용자 로컬 `.NET 8` + `maccatalyst` toolchain을 준비한다.
- `scripts/sign-mac-artifacts.sh`와 `scripts/notarize-mac-artifacts.sh`는 Developer ID 서명과 Apple notarization 단계를 분리해 배포 파이프라인에 연결할 수 있게 한다.
- `scripts/check-mac-signing-prereqs.sh`와 `scripts/release-mac.sh`는 macOS 서명 자격증명 검증과 end-to-end 릴리스 실행을 담당한다.
- `scripts/setup-mac-notary-profile.sh`는 `notarytool store-credentials`를 자동화해 머신별 notarization profile 준비 시간을 줄인다.
- `scripts/release-windows.ps1`와 `scripts/write-release-manifest.mjs`는 Windows/macOS 릴리스 산출물에 checksum manifest를 남겨 배포 검증과 전달 추적을 단순화한다.
- `scripts/verify-release-manifest.mjs`는 전달 직전 checksum manifest를 재검증해 릴리스 폴더 손상이나 변경을 잡는다.
- `scripts/tests/release-scripts.test.mjs`는 publish 전에 release-helper 회귀를 먼저 검증한다.
- `scripts/import-mac-signing-certificate.sh`는 CI에서 base64 `.p12`와 임시 keychain을 사용해 Developer ID 인증서를 주입한다.
- GitHub Actions의 macOS job은 headless runner 특성상 `SKIP_LAUNCH_SMOKE_TEST=1`로 실행하고, 로컬 macOS publish에서는 launch smoke test를 기본 유지한다.

## 15. 리스크 및 대응

| 리스크 | 설명 | 대응 |
|---|---|---|
| 기준 문서 품질 | 잘못된 baseline은 잘못된 판정을 만든다 | 로더 단계에서 유효성 검증 강화 |
| 대용량 JAR 처리 | 파일 수와 엔트리 수가 많으면 시간이 길어진다 | 단계형 비교와 캐시 도입 |
| XML 노이즈 | 공백/주석/순서 차이로 오탐 가능 | 정규화 규칙을 명시적으로 고정 |
| YAML 표현식 노이즈 | 들여쓰기, key 순서, anchor/alias 차이로 오탐 가능 | 정규화 규칙과 지원 범위를 먼저 고정 |
| 결과 과다 | 차이가 많으면 UI와 리포트가 복잡해진다 | 요약 카드, 필터, 상위 위험도 우선 표시 |
| 프로젝트 명 변경 작업 | 기존 `SuperTutty` 기반 흔적이 많을 수 있다 | Phase 0에서 이름 변경 범위를 먼저 고정 |
| SDK/workload 불일치 | `net8.0-maccatalyst` 앱을 다른 major SDK/workload로 publish 하면 launch 불능 산출물이 나올 수 있다 | mac publish는 `.NET 8.x` guard와 launch smoke test를 기본 적용하고, 배포용 서명 전 로컬 실행 검증을 통과시킨다 |

## 16. 바로 실행할 작업 목록

1. `NexusWorks.Guardian` 솔루션/프로젝트 골격 생성
2. `baseline.xlsx` 샘플 템플릿 정의
3. 샘플 운영본/패치본 데이터셋 준비
4. 비교 엔진 MVP부터 구현
5. HTML/Excel 리포트 MVP 구현
6. MAUI Blazor UI 연결

## 17. 최종 제안

가장 현실적인 시작점은 아래 순서다.

1. `NexusWorks.Guardian` 핵심 엔진 라이브러리부터 만든다.
2. Baseline 기반 비교와 상태 판정까지 먼저 완성한다.
3. HTML/Excel 리포트를 붙여 운영 검수 흐름을 완성한다.
4. 마지막에 `NexusWorks.Guardian.UI`로 실행 화면과 대시보드를 얹는다.

이 방식이면 PDF에서 요구한 범용 검수 체계를 유지하면서도, 현재 저장소의 .NET 기반과 자연스럽게 연결할 수 있다.

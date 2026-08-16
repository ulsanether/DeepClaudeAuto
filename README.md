# DeepClaudeAuto

> **DeepClaude 자동 구축 도구** — WPF MVVM 기반 설치 자동화 프로그램

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [주요 기능](#2-주요-기능)
3. [시스템 요구사항](#3-시스템-요구사항)
4. [아키텍처 설계 (WPF MVVM)](#4-아키텍처-설계-wpf-mvvm)
5. [화면 구성 및 워크플로우](#5-화면-구성-및-워크플로우)
6. [설치 검증 단계 상세](#6-설치-검증-단계-상세)
7. [프로젝트 구조](#7-프로젝트-구조)
8. [개발 환경 설정](#8-개발-환경-설정)
9. [빌드 및 실행](#9-빌드-및-실행)
10. [로드맵](#10-로드맵)

---

## 1. 프로젝트 개요

**DeepClaudeAuto**는 [DeepClaude](https://github.com/getasterisk/deepclaude) 서버를 Windows 환경에서 자동으로 설치·구성·검증하는 데스크톱 애플리케이션입니다.

사용자가 복잡한 CLI 명령어나 환경 설정 없이, 단계별 마법사(Wizard) UI를 통해 DeepClaude를 완전히 구축할 수 있도록 안내합니다.

---

## 2. 주요 기능

| 기능 | 설명 |
|------|------|
| **설치 검증** | Python, pip, Git, Docker 등 필수 의존성 자동 감지 및 상태 표시 |
| **자동 설치** | 누락된 구성 요소를 자동으로 다운로드·설치 |
| **환경 구성** | `.env` 파일 생성 및 API 키(Anthropic, DeepSeek 등) 설정 |
| **서버 빌드** | DeepClaude 소스 클론 및 의존성 설치 자동화 |
| **서버 실행/중지** | 내장 프로세스 관리로 서버 시작·중지·재시작 |
| **상태 모니터링** | 실시간 로그 스트리밍 및 헬스체크 |
| **설정 저장/불러오기** | 구성 프로파일 저장 및 재사용 |

---

## 3. 시스템 요구사항

### 실행 환경

- **OS**: Windows 10 (1903 이상) / Windows 11
- **.NET**: .NET 8.0 Runtime 이상
- **권한**: 관리자 권한 (일부 설치 단계)

### DeepClaude 구축을 위한 의존성 (자동 검증 대상)

| 항목 | 최소 버전 | 비고 |
|------|-----------|------|
| Python | 3.10 이상 | pyenv 또는 직접 설치 |
| pip | 최신 권장 | Python 번들 포함 |
| Git | 2.x 이상 | 소스 클론용 |
| Docker Desktop | 4.x 이상 | 컨테이너 방식 선택 시 |
| Rust / Cargo | 1.70 이상 | 소스 빌드 방식 선택 시 |

---

## 4. 아키텍처 설계 (WPF MVVM)

### 레이어 구조

```
┌─────────────────────────────────────────┐
│              View Layer                 │  ← XAML Views (WPF)
│  MainWindow, WizardPage, LogViewer 등   │
├─────────────────────────────────────────┤
│           ViewModel Layer               │  ← INotifyPropertyChanged
│  MainViewModel, StepViewModels 등       │
├─────────────────────────────────────────┤
│             Model / Service Layer       │  ← 비즈니스 로직
│  DependencyChecker, Installer,          │
│  ProcessManager, ConfigManager 등       │
├─────────────────────────────────────────┤
│           Infrastructure Layer          │
│  FileSystem, ProcessRunner, HttpClient  │
└─────────────────────────────────────────┘
```

### 핵심 MVVM 원칙

- **View**: XAML만으로 UI 정의, 코드비하인드 최소화
- **ViewModel**: `INotifyPropertyChanged` + `ICommand` (`RelayCommand`) 구현
- **Model/Service**: ViewModel에서 DI(의존성 주입)로 주입, 테스트 가능하게 설계
- **바인딩**: 단방향/양방향 데이터바인딩으로 UI 자동 갱신
- **메시징**: `WeakReferenceMessenger` (CommunityToolkit.Mvvm) 로 ViewModel 간 통신

### 사용 라이브러리

| 라이브러리 | 용도 |
|-----------|------|
| `CommunityToolkit.Mvvm` | MVVM 보일러플레이트 감소 (`ObservableObject`, `RelayCommand`) |
| `Microsoft.Extensions.DependencyInjection` | DI 컨테이너 |
| `Serilog` | 구조화 로깅 |
| `Newtonsoft.Json` / `System.Text.Json` | 설정 파일 직렬화 |

---

## 5. 화면 구성 및 워크플로우

### 마법사 단계 흐름

```
[시작 화면]
    │
    ▼
[Step 1: 설치 검증]  ── 의존성 자동 스캔 및 상태 표시
    │
    ▼
[Step 2: 누락 항목 설치]  ── 선택적 자동 설치 또는 수동 안내
    │
    ▼
[Step 3: API 키 구성]  ── Anthropic / DeepSeek API 키 입력
    │
    ▼
[Step 4: 빌드 방식 선택]  ── 소스 빌드 / Docker / 기존 설치
    │
    ▼
[Step 5: 서버 빌드 및 설치]  ── 진행률 표시줄 + 실시간 로그
    │
    ▼
[Step 6: 서버 시작 및 검증]  ── 헬스체크, 테스트 요청 전송
    │
    ▼
[완료 화면]  ── 서버 주소, 설정 저장, 바로가기 생성
```

### 주요 화면 설명

#### 설치 검증 화면 (Step 1)
- 의존성 목록을 테이블로 표시
- 각 항목에 **✅ 설치됨 / ⚠️ 버전 낮음 / ❌ 미설치** 상태 아이콘
- "다시 검사" 버튼으로 재검증
- 모든 항목 통과 시 "다음" 버튼 활성화

#### 실시간 로그 패널
- 하단 고정 또는 분리 창으로 로그 스트리밍
- 로그 레벨별 색상 구분 (INFO, WARN, ERROR)
- 로그 파일 저장 및 클립보드 복사 기능

---

## 6. 설치 검증 단계 상세

### 검증 항목 및 방법

| 검증 항목 | 검증 방법 | 실패 시 처리 |
|-----------|-----------|-------------|
| Python 설치 여부 | `python --version` 실행 | 자동 설치 제안 (winget / 직접 다운로드) |
| Python 버전 충족 | 버전 문자열 파싱 (≥3.10) | 업그레이드 안내 |
| pip 사용 가능 | `pip --version` 실행 | `ensurepip` 모듈로 복구 시도 |
| Git 설치 여부 | `git --version` 실행 | winget 자동 설치 제안 |
| Docker 실행 중 | Docker 소켓 / API 상태 확인 | Docker Desktop 실행 안내 |
| 포트 사용 가능 | TCP 포트(기본 3000) 개방 여부 확인 | 대체 포트 입력 유도 |
| 디스크 여유 공간 | 최소 2GB 여유 확인 | 경고 메시지 표시 |
| 인터넷 연결 | GitHub 도달 가능 여부 확인 | 프록시 설정 안내 |

### 검증 결과 모델

```csharp
public class DependencyCheckResult
{
    public string Name { get; set; }          // 항목명
    public CheckStatus Status { get; set; }   // Passed / Warning / Failed
    public string DetectedVersion { get; set; }
    public string RequiredVersion { get; set; }
    public string Message { get; set; }       // 사용자에게 보여줄 설명
    public string FixAction { get; set; }     // 수정 명령 또는 URL
}

public enum CheckStatus { Passed, Warning, Failed, Checking }
```

---

## 7. 프로젝트 구조

```
DeepClaudeAuto/
├── DeepClaudeAuto.sln
├── src/
│   ├── DeepClaudeAuto.App/          # WPF 진입점
│   │   ├── App.xaml
│   │   ├── App.xaml.cs              # DI 컨테이너 초기화
│   │   └── appsettings.json
│   │
│   ├── DeepClaudeAuto.UI/           # View / ViewModel
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml
│   │   │   ├── WizardShell.xaml
│   │   │   ├── Steps/
│   │   │   │   ├── Step01_ValidationView.xaml
│   │   │   │   ├── Step02_InstallView.xaml
│   │   │   │   ├── Step03_ConfigView.xaml
│   │   │   │   ├── Step04_BuildModeView.xaml
│   │   │   │   ├── Step05_BuildView.xaml
│   │   │   │   ├── Step06_VerifyView.xaml
│   │   │   │   └── Step07_CompleteView.xaml
│   │   │   └── Controls/
│   │   │       ├── DependencyStatusControl.xaml
│   │   │       └── LogViewerControl.xaml
│   │   └── ViewModels/
│   │       ├── MainViewModel.cs
│   │       ├── WizardViewModel.cs
│   │       └── Steps/
│   │           ├── ValidationViewModel.cs
│   │           ├── InstallViewModel.cs
│   │           ├── ConfigViewModel.cs
│   │           └── ...
│   │
│   ├── DeepClaudeAuto.Core/         # 비즈니스 로직 (WPF 비의존)
│   │   ├── Services/
│   │   │   ├── IDependencyChecker.cs
│   │   │   ├── DependencyChecker.cs
│   │   │   ├── IInstallerService.cs
│   │   │   ├── InstallerService.cs
│   │   │   ├── IProcessRunner.cs
│   │   │   ├── ProcessRunner.cs
│   │   │   ├── IConfigManager.cs
│   │   │   └── ConfigManager.cs
│   │   └── Models/
│   │       ├── DependencyCheckResult.cs
│   │       ├── AppConfig.cs
│   │       └── BuildOptions.cs
│   │
│   └── DeepClaudeAuto.Tests/        # 단위 테스트
│       ├── DependencyCheckerTests.cs
│       └── ...
│
└── docs/
    ├── architecture.md
    └── screenshots/
```

---

## 8. 개발 환경 설정

### 필수 도구

1. **Visual Studio 2022** (17.8 이상) 또는 **Rider**
   - 워크로드: `.NET 데스크톱 개발`
2. **.NET 8.0 SDK**
3. **Git**

### 초기 설정

```bash
git clone https://github.com/ulsanether/DeepClaudeAuto.git
cd DeepClaudeAuto
dotnet restore
```

### 코드 스타일

- EditorConfig(`.editorconfig`) 적용
- C# 최신 기능 사용 (`nullable`, `file-scoped namespace` 등)
- XAML: 들여쓰기 4칸, 속성 한 줄 정렬

---

## 9. 빌드 및 실행

```bash
# 디버그 빌드
dotnet build src/DeepClaudeAuto.sln -c Debug

# 릴리즈 빌드
dotnet build src/DeepClaudeAuto.sln -c Release

# 실행
dotnet run --project src/DeepClaudeAuto.App

# 단위 테스트
dotnet test src/DeepClaudeAuto.Tests
```

### 단독 실행 파일 패키징

```bash
dotnet publish src/DeepClaudeAuto.App \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish
```

---

## 10. 로드맵

### v0.1 — 설치 검증 MVP
- [ ] 의존성 체크 서비스 구현
- [ ] Step 1 (검증) View/ViewModel 구현
- [ ] 검증 결과 UI 표시

### v0.2 — 자동 설치
- [ ] winget 기반 자동 설치 서비스
- [ ] 설치 진행률 UI

### v0.3 — 환경 구성 및 빌드
- [ ] API 키 입력 및 `.env` 생성
- [ ] 소스 클론 및 의존성 설치 자동화

### v0.4 — 서버 실행 및 모니터링
- [ ] 서버 프로세스 관리
- [ ] 실시간 로그 스트리밍
- [ ] 헬스체크 및 테스트 요청

### v1.0 — 정식 릴리즈
- [ ] 설정 프로파일 저장/불러오기
- [ ] 자동 업데이트 체크
- [ ] 한국어/영어 다국어 지원
- [ ] 인스톨러 패키징 (WiX / MSIX)

---

## 라이선스

MIT License — 자세한 내용은 [LICENSE](./LICENSE) 참조

---

*이 문서는 DeepClaudeAuto 프로젝트의 설계 및 개발 가이드입니다.*

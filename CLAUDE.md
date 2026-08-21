# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

DeepClaudeAuto는 [DeepClaude](https://github.com/getasterisk/deepclaude) 서버를 Windows에 자동 설치·구성·실행하는 WPF 데스크톱 마법사 앱입니다. Python/pip/Git/Docker 등 의존성 검증 → 자동 설치 → API 키(.env) 구성 → 소스 클론·빌드 → 서버 실행·헬스체크의 6단계 흐름을 제공합니다.

UI 텍스트, 로그 메시지, 코드 주석은 **한국어**로 작성합니다. 새 코드도 이 관례를 따르세요.

## 빌드 / 테스트 / 실행

모든 프로젝트는 `net8.0-windows` + `EnableWindowsTargeting=true`입니다. 빌드는 어느 OS에서든 되지만 **앱 실행은 Windows 전용**(WPF + WinForms)입니다.

```powershell
dotnet build                            # 루트의 DeepClaudeAuto.slnx를 자동 선택해 빌드
dotnet test                             # 전체 테스트 (xUnit)
dotnet test --filter "FullyQualifiedName~BuilderServiceTests"   # 단일 테스트 클래스/메서드
dotnet run --project src/DeepClaudeAuto.App                      # 앱 실행
```

```powershell
# 단독 실행 파일 패키징
dotnet publish src/DeepClaudeAuto.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

솔루션 파일은 루트의 `DeepClaudeAuto.slnx`(신형 XML 솔루션 포맷)입니다. README에 적힌 `src/DeepClaudeAuto.sln` 경로는 outdated이니 참고하지 마세요.

## 아키텍처

### 프로젝트 구성

- **DeepClaudeAuto.App** — WPF 진입점이자 DI 컴포지션 루트. `App.xaml.cs`에서 `Host.CreateDefaultBuilder().UseSerilog()`로 구성
- **DeepClaudeAuto.UI** — View(XAML), ViewModel, Converter, 테마 (`Themes/Styles.xaml`)
- **DeepClaudeAuto.Core** — WPF 비의존 비즈니스 로직 (서비스 + 모델). UI가 필요한 부분은 인터페이스로 추상화
- **DeepClaudeAuto.Tests** — xUnit + Moq. **Core만 참조**하며 WPF/UI는 테스트하지 않음

### DI 구성 (`App.xaml.cs`)

- 모든 Core 서비스와 ViewModel은 **singleton**, `MainWindow`만 transient
- 새 서비스/VM 추가 순서: Core에 인터페이스·구현 작성 → `App.xaml.cs`에 등록
- 로깅은 Serilog로 `%APPDATA%\DeepClaudeAuto\logs\app-.log`에 일별 롤링 기록

### MVVM (CommunityToolkit.Mvvm 소스 제너레이터)

- ViewModel은 반드시 `sealed partial class` — `[ObservableProperty]`, `[RelayCommand]`(비동기 취소 필요 시 `IncludeCancelCommand = true`) 사용
- 커스텀 `RelayCommand`나 `WeakReferenceMessenger`는 없음 (README의 해당 설명은 outdated)

### 마법사(Wizard) 흐름

- `WizardViewModel`이 6개 Step ViewModel(`Validation → Install → Config → BuildMode → Build → Server`)을 순서 리스트(`_steps`)로 보유하고, `CurrentStep`이 `ContentControl`에 표시됨
- `Themes/Styles.xaml`의 암시적 `DataTemplate`이 각 Step **VM 타입 → View**를 매핑. 새 스텝 추가 = VM + View + DataTemplate + DI 등록 + `_steps` 리스트에 추가
- **스텝 간 데이터 공유**: Step VM들이 서로를 생성자 주입으로 참조함 (예: `BuildViewModel` → `ConfigViewModel`, `BuildModeViewModel`). `WizardViewModel.BuildConfig()`와 `BuildViewModel.StartBuildAsync`가 각각 `AppConfig`를 조립하는 중복이 있으니, 필드를 추가할 때는 두 곳 모두 갱신
- `AppConfig.BuildMode`는 `"Source" | "Docker"` **문자열**로 `BuilderService`(cargo build vs docker build)와 `ServerManager`(deepreasoning.exe 실행 vs docker run)의 동작을 분기
- 설정은 `%APPDATA%\DeepClaudeAuto\config.json`에 저장되며, 빌드 성공 시점에 `BuildViewModel`이 `IConfigManager.Save()` 호출

### 프로세스 실행

- 모든 외부 명령은 `IProcessRunner` 경유:
  - `RunAsync` — stdout/stderr 전체 캡처 (의존성 버전 확인용)
  - `RunWithStreamingAsync` — 라인 단위 콜백 스트리밍 (git clone, pip install, docker build 로그 UI 표시용)
- `ServerManager`는 자체 `Process`를 관리하며 **TCP 연결 성공 여부**(TcpClient, 대상 서버에는 `/health` 라우트 없음)로 10초(1초×10) 폴링해 상태 판정. 이벤트(`LogReceived`, `StatusChanged`)로 VM에 통지

### 스레딩

프로세스 콜백·이벤트는 **백그라운드 스레드**에서 발생합니다. `ObservableCollection`을 갱신하기 전에 `Application.Current.Dispatcher.Invoke`로 UI 스레드에 마샬링하세요 (`BuildViewModel.Log`, `ServerViewModel` 이벤트 핸들러 참고). `DependencyChecker`의 `IProgress<T>`는 자동으로 마샬링됩니다.

### 테스트 패턴

`Mock<IProcessRunner>` + `It.IsAny` Setup + `NullLogger<T>.Instance`(`Microsoft.Extensions.Logging.Abstractions`) 조합으로 Core 서비스를 WPF 없이 테스트합니다. 새 Core 서비스 테스트도 이 패턴을 따르세요.

## 주의사항

- 대상 저장소 `getasterisk/deepclaude`는 **Rust(axum) 프로젝트 "deepreasoning"으로 재작성**되었습니다. Python/uvicorn/requirements.txt는 없으며, API 키는 요청 헤더로 전달되고(서버가 `.env`를 읽지 않음), 리슨 포트는 `config.toml`의 `[server] port`로 결정됩니다. Python 기반 구버전을 가정한 코드나 문서를 발견하면 outdated입니다.
- 상류 Cargo.toml에는 코드에서 사용되지 않는 `openssl (vendored)` 의존성이 있어 Windows에서 perl 부재로 빌드가 실패합니다. `BuilderService.PatchCargoTomlAsync`가 클론 후 해당 라인을 제거합니다 (신규 클론에도 자동 적용).
- `src/**/obj/` 파일들이 `.gitignore`에 있음에도 **git에 추적**되어 있습니다(과거 커밋). git status의 obj/ 변경은 무시하고 커밋하지 마세요.
- `.env` 파일에 Anthropic/DeepSeek API 키가 평문으로 기록됩니다(`BuilderService.WriteEnvFileAsync`) — 서버가 직접 읽지는 않지만 사용자 편의를 위해 유지합니다.
- Copilot 규칙(`.github/copilot-instructions.md`): Azure 관련 작업 시 Azure 도구(azmcp)를 사용하라는 규칙이 있으나, 이 저장소에는 Azure 코드가 없습니다.

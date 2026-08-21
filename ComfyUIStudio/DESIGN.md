# ComfyUIStudio — ComfyUI 로컬 이미지 생성 WPF 앱 설계 문서

> 작성일: 2026-08-17 · 상태: 설계 초안 (구현 전)
> 이 문서는 `DeepClaudeAuto` 저장소의 관례(한국어 UI/로그/주석, slnx, MVVM, 테스트 패턴)를 그대로 따릅니다.

## 1. 개요

**ComfyUIStudio**는 로컬 PC에 ComfyUI를 설치·구성·실행하고, 별도 웹 브라우저 없이
WPF 데스크톱 앱 안에서 프롬프트 기반 이미지 생성을 할 수 있게 하는 마법사형 애플리케이션입니다.

### 목표 시나리오

1. 사용자가 앱을 실행 → 환경 확인(Windows, GPU, Git/7-Zip 등)
2. ComfyUI 설치 (portable 다운로드·해제 또는 소스 클론 + venv)
3. 필수 모델 다운로드 (체크포인트, VAE 등 — 진행률 표시)
4. ComfyUI 서버 자동 실행 + 헬스체크 (`GET /system_stats`)
5. 생성 화면에서 프롬프트/파라미터 입력 → 이미지 생성 (실시간 미리보기·진행률)
6. 생성 결과 이미지 저장·갤러리 확인

### 비목표 (v1 범위 제외)

- 워크플로우 그래프 시각 편집기 (ComfyUI 자체 UI가 담당, 외부 브라우저로 열기 버튼만 제공)
- ControlNet/이미지 업로드 기반 img2img는 워크플로우 템플릿 확장으로 지원 (기본은 txt2img)
- 원격 서버 접속·인증 (보안상 로컬 바인딩 전제)

## 2. 기술 스택

| 항목 | 선택 | 이유 |
|---|---|---|
| 런타임 | .NET 8 (net8.0-windows, `EnableWindowsTargeting=true`) | DeepClaudeAuto와 동일 |
| UI | WPF + CommunityToolkit.Mvvm (source generator) | 동일 관례, `[ObservableProperty]`/`[RelayCommand]` |
| DI | `Host.CreateDefaultBuilder()` + Serilog | 동일 관례, 로그는 `%APPDATA%\ComfyUIStudio\logs\` |
| ComfyUI REST | `System.Net.Http.HttpClient` | 외부 패키지 불필요 |
| ComfyUI WebSocket | `System.Net.WebSockets.ClientWebSocket` | 외부 패키지 불필요 |
| JSON | `System.Text.Json` | ComfyUI 메시지는 전부 JSON + 이진 프리뷰 |
| 7z 해제 | 7-Zip 설치 확인(`7z.exe`) 권장, fallback으로 SharpCompress | portable 배포판이 `.7z` |
| 테스트 | xUnit + Moq + `NullLogger<T>` | 동일 관례 |

## 3. 프로젝트 구성

```
ComfyUIStudio/
├── ComfyUIStudio.slnx
└── src/
    ├── ComfyUIStudio.App/        # WPF 진입점 + DI 컴포지션 루트 (App.xaml.cs)
    ├── ComfyUIStudio.Core/       # WPF 비의존 비즈니스 로직 (서비스 + 모델)
    ├── ComfyUIStudio.UI/         # Views, ViewModels, Converter, Themes/Styles.xaml
    └── ComfyUIStudio.Tests/      # xUnit + Moq, Core만 참조
```

- 모든 Core 서비스와 ViewModel은 **singleton**, `MainWindow`는 transient
- 새 서비스/VM 추가 순서: Core에 인터페이스·구현 → `App.xaml.cs` 등록 → (새 스텝이면) UI DataTemplate + `_steps` 리스트

## 4. ComfyUI 연동 설계

ComfyUI는 HTTP(JSON) + WebSocket 두 채널을 제공합니다. 기본 주소는 `http://127.0.0.1:8188`입니다.

### 4.1 REST API (사용 항목)

| 메서드/경로 | 용도 | 응답(핵심) |
|---|---|---|
| `GET /system_stats` | 헬스체크 + GPU 정보 | `devices[]`: name, vram_total/free, torch_vram_* |
| `GET /object_info` | 노드 입력 스키마 (파라미터 자동 맵핑) | `class_type` → required/default |
| `POST /prompt` | 워크플로우 제출 | `{ "prompt": <workflow>, "client_id": <uuid> }` → `prompt_id` |
| `GET /history/{prompt_id}` | 실행 결과·오류 | `outputs[].images[]` (filename/subfolder/type), `status.status_str` |
| `GET /view` | 결과 이미지 다운로드 | `?filename=&subfolder=&type=` → 이미지 바이너리 |
| `GET /queue` | 대기열 조회 | running/pending 목록 |
| `POST /interrupt` | 생성 중단 | — |
| `GET /models/checkpoints` 외 | 모델 목록 표시 | 모델명 배열 |

### 4.2 WebSocket (`/ws?clientId=<uuid>`)

- **`clientId`는 `POST /prompt`에 보낸 값과 반드시 일치**해야 해당 실행의 이벤트를 받음
- 텍스트 메시지: `{ "type": ..., "data": {...} }`

| type | data 핵심 | 의미 |
|---|---|---|
| `status` | `status.exec_info.queue_remaining` | 연결 직후 큐 상태 1회 수신 |
| `executing` | `node`, `prompt_id` | 노드 단위 실행. **`node: null` = 실행 완료** |
| `progress` | `value`, `max` | 샘플링 스텝 진행률 |
| `executed` | `node`, `output` | 노드 출력 (SaveImage면 결과 이미지 정보) |
| `execution_error` | `node_id`, `exception_message` | 실패 원인 |

- 바이너리 메시지(실시간 미리보기): **첫 4바이트 = 빅엔디안 UInt32 이벤트 타입**, 이후가 이미지 데이터
  - `1` = 인코딩된 프리뷰 (JPEG/PNG), `2` = 비인코딩 raw latent, `3` = 텍스트, `4` = 메타데이터 포함
  - 일부 문서는 4+4바이트(타입+포맷) 8바이트 헤더를 언급 → **구현 1단계에서 서버 소스(`comfy_server_ws.py`)로 확정** 후 파서 작성, 이진 프레임의 `event != 1`은 무시
- 프리뷰는 샘플링 스텝마다 오므로 **디스플레이 주기 스로틀링**(예: 300ms) 필요

### 4.3 생성 시퀀스 (핵심 흐름)

```
[UI: 생성 클릭]
 1. clientId = Guid.NewGuid()
 2. WS 연결: ws://127.0.0.1:8188/ws?clientId=<clientId>
 3. POST /prompt { prompt: 치환된 워크플로우, client_id: clientId } → prompt_id
 4. WS 이벤트 루프 (백그라운드 스레드):
      executing(노드) → 진행 메시지 → progress(value/max) → 프리뷰(바이너리)
      → executing{node:null} = 성공 / execution_error = 실패
 5. GET /history/{prompt_id} → outputs.images[]
 6. 각 이미지 GET /view → PNG 저장 + 결과 갤러리에 추가
```

### 4.4 워크플로우 (API format JSON)

- ComfyUI UI에서 "Save (API Format)"으로 내보낸 JSON 형태: `{ "노드id": { "class_type", "inputs", "_meta" } }`
- 노드 간 연결은 `inputs`의 값이 `["노드id", 출력인덱스]` 배열
- **파라미터 치환 규칙** (v1은 기본 제공 txt2img 템플릿에만 적용):
  - `CLIPTextEncode` → `positive`/`negative` 프롬프트
  - `KSampler` → `seed`(기본 -1), `steps`, `cfg`, `sampler_name`, `scheduler`, `denoise`
  - `EmptyLatentImage` → `width`, `height`, `batch_size`
  - `CheckpointLoaderSimple` → `ckpt_name` (모델 드롭다운)
- 기본 템플릿은 앱에 번들로 내장 (`Assets/Workflows/txt2img.json`), 사용자 워크플로우는 `%APPDATA%\ComfyUIStudio\workflows\` 폴더 지원 (구조 유효성만 검사, 프리셋 파라미터 치환은 템플릿 키 매칭)

## 5. Core 서비스 설계

모든 서비스는 인터페이스 + 구현으로 분리하고, 외부 호출부는 테스트 가능하도록 가볍게 유지합니다.

| 인터페이스 | 책임 | 주요 멤버 (스케치) |
|---|---|---|
| `IComfyApiClient` | REST 전용 (base URL 주입) | `SystemStatsAsync`, `SubmitPromptAsync(workflow, clientId)`, `GetHistoryAsync(promptId)`, `GetViewImageAsync(imageRef)`, `InterruptAsync`, `GetQueueAsync`, `GetObjectInfoAsync`, `GetModelsAsync(kind)` |
| `IComfyWebSocketClient` | WS 연결·이벤트 발행 | `ConnectAsync(clientId)`, `DisconnectAsync`, 이벤트: `ProgressReceived`, `ExecutingChanged`, `Executed`, `ExecutionFinished`, `ExecutionError`, `PreviewImageReceived` |
| `IWorkflowManager` | 워크플로우 로드·치환 | `LoadTemplateAsync(name)`, `LoadFromFileAsync(path)`, `ApplyParameters(workflow, GenerationParameters)`, `Validate(workflow)` |
| `IPromptRunner` | 생성 오케스트레이션 (위 4.3) | `Task<RunResult> RunAsync(GenerationParameters, CancellationToken)`, 이벤트로 진행·프리뷰 통지 |
| `IImageStore` | 결과 이미지 저장·조회 | `SaveResultAsync(imageRef, promptInfo)`, `GetGalleryAsync()`, `OpenFolder()` |
| `IConfigManager` | 설정 영속화 | `%APPDATA%\ComfyUIStudio\config.json` (포트, 기본 모델, 다운로드 폴더, GPU 옵션) |
| `IProcessRunner` | 외부 프로세스 실행 (기존 패턴 재사용) | `RunAsync`(전체 캡처) / `RunWithStreamingAsync`(라인 스트리밍) |
| `IDependencyChecker` | 환경 확인 | Git, 7-Zip, GPU(VRAM) 확인. portable 설치 시 Python 확인 불필요 |
| `IComfyInstaller` | ComfyUI 설치 | `InstallPortableAsync(url, progress)`, `InstallFromSourceAsync(progress)` (git clone+venv+pip) |
| `IModelDownloader` | 모델 다운로드 | `DownloadAsync(url, targetPath, progress, ct)` — HttpClient 스트리밍, `Content-Length`로 백분율, `.part` 파일 후 rename |
| `IComfyServerManager` | 서버 실행·관리 | `StartAsync(ServerOptions)`, `StopAsync()`, `IsHealthyAsync()`(= `system_stats` 성공), `StatusChanged`/`LogReceived` 이벤트 |

- **생성 흐름은 `IPromptRunner`가 단일 진입점** — VM은 Runner의 이벤트만 구독하면 됨 (기존 `BuilderService`와 같은 패턴)
- `IComfyApiClient`의 HTTP는 테스트에서 `HttpMessageHandler`를 교체해 모킹
- `IComfyWebSocketClient`는 WS receive 루프를 내부에 캡슐화 → 테스트는 인터페이스 모킹

## 6. UI 설계

### 6.1 마법사 스텝 (DeepClaudeAuto와 동일한 `WizardViewModel` 패턴)

1. **환경 확인** — Windows/GPU/디스크 여유 공간, (소스 설치 시) Git/Python, (portable 시) 7-Zip
2. **ComfyUI 설치** — 방식 선택(portable 권장 / 소스 클론), 진행률·로그 스트리밍
3. **모델 다운로드** — 기본 세트(SDXL base 등) 선택·URL 추가, 파일 크기·진행률, 취소
4. **서버 실행** — 포트/GPU 옵션(`--lowvram` 등), 시작·헬스체크·중단, 로그 뷰
5. **이미지 생성 (메인)** — 아래 6.2

> 마법사 완료 후 재실행 시 생성 스텝(5)부터 시작하고, 서버가 안 떠 있으면 스텝 4로 복귀

### 6.2 생성 화면 레이아웃 (스텝 5)

```
┌──────────────────────────────────────────────────────────┐
│ 워크플로우 [txt2img ▼]   모델 [sd_xl_base_1.0.safetensors ▼] │
├────────────────────────────┬─────────────────────────────┤
│ 프롬프트 (TextBox, 여러 줄)   │  실시간 미리보기               │
│ 네거티브 (TextBox, 여러 줄)   │  (WS 프리뷰 바이너리 → Image)   │
│ 시드[-1] 스텝[30] CFG[7]     │                             │
│ 샘플러[euler ▼] 스케줄러     │  ┌────────────────────────┐  │
│ [normal ▼]                  │  │  [진행률 ProgressBar]   │  │
│ 가로[1024] 세로[1024] 배치[1] │  │  3/30 스텝 · KSampler   │  │
│                             │  └────────────────────────┘  │
│                             │  [🖼 생성] [⏹ 중단]           │
├────────────────────────────┴─────────────────────────────┤
│ 결과 히스토리 (썸네일 스트립, 클릭 시 크게 보기 + 폴더 열기)      │
└──────────────────────────────────────────────────────────┘
```

- 진행률: `progress(value/max)` + 현재 노드(`executing`) 표시. 프리뷰 이미지는 스텝마다 갱신되므로 스로틀링
- `[생성]`은 `IPromptRunner.RunAsync` 호출, `[중단]`은 `POST /interrupt` (+ `CancellationToken` 연계, `IncludeCancelCommand = true`)
- 결과 히스토리: `IImageStore` 기반. 이미지는 파일 스트림으로 로드해 `BitmapImage`에 `CacheOption.OnLoad` + `Freeze()` (메모리 누수 방지)

### 6.3 스레딩 주의 (기존 관례 동일)

- WS 이벤트·프로세스 로그 콜백은 **백그라운드 스레드**에서 발생 → `ObservableCollection` 갱신 전 `Application.Current.Dispatcher.Invoke` 마샬링
- `IProgress<T>`는 자동 마샬링됨

## 7. 설치·모델 관리 설계

### 7.1 설치 방식

| 항목 | portable (권장) | 소스 클론 |
|---|---|---|
| 내용 | 공식 `ComfyUI_windows_portable*.7z` 다운로드 + 해제 (내장 Python+torch 포함) | `git clone` + `python -m venv` + `pip install -r requirements.txt` |
| 파이썬 | 불필요 | 필요 |
| 주의 | **7z 해제 도구 필요** (7-Zip 확인 → 없으면 winget 설치 안내, fallback: SharpCompress) | torch CUDA wheel: `--index-url https://download.pytorch.org/whl/cu124` 지정 필요 |
| 난이도 | 낮음 | 중간 (실패 지점 많음: 빌드 도구, 드라이버) |

- 다운로드·해제는 `IModelDownloader`와 동일한 스트리밍+진행률 패턴 재사용
- 클론 시에도 GPU 드라이버 확인 후 CUDA wheel 선택

### 7.2 서버 실행

```
실행: <comfyui_root>\python_embeded\python.exe -s ComfyUI\main.py --port 8188 --listen 127.0.0.1
헬스체크: GET /system_stats 성공 (기존 DeepClaudeAuto의 TCP 폴링보다 확실 — REST 응답 자체가 판정 기준)
중단: 프로세스 종료 + 트리거 가드
```

- GPU 옵션: `--lowvram`/`--novram`(VRAM 부족 시), 설정 화면에서 선택
- 보안: ComfyUI는 인증이 없으므로 `--listen 127.0.0.1` 고정 (외부 노출 금지)

### 7.3 모델 다운로드

- 대상 폴더: `ComfyUI\models\checkpoints`(체크포인트), `\vae`, `\loras` 등
- HuggingFace `resolve/main/...` URL 또는 CivitAI 다운로드 링크 지원
- 진행률: `HttpClient` ResponseHeadersRead + `Content-Length` 비율, 남은 시간 표시
- 실패 대응: `.part` 임시 파일 유지 후 재시도 시 이어서 받기(`Range` 헤더, v1 후순위), 완료 시 rename
- 기본 추천 세트: SDXL base 1.0(~6.9GB) 등 — 선택 사항이며 모델 없이도 생성은 불가하므로 스텝 3 완료 조건으로 삼음

## 8. 구현 단계 (Phase)

| Phase | 내용 | 완료 기준 |
|---|---|---|
| 1 | 스캐폴드: `ComfyUIStudio.slnx`, App/UI/Core/Tests 프로젝트, DI, Serilog, 테마, 빈 마법사 골격 | `dotnet build` 성공 |
| 2 | `IComfyApiClient` + 모델(JsonResponse): system_stats/history/view/interrupt/queue | 단위 테스트 (HttpMessageHandler 모킹) |
| 3 | `IComfyWebSocketClient`: 연결·JSON 메시지 파싱·바이너리 프리뷰 헤더 확정·이벤트 발행 | 단위 테스트 (프레임 시뮬레이션) |
| 4 | `IWorkflowManager`(템플릿/치환) + `IPromptRunner` 오케스트레이션 | 단위 테스트 (가짜 API+WS로 전체 시퀀스 검증) |
| 5 | 생성 화면 UI (6.2) + 결과 저장·갤러리 | 로컬 ComfyUI로 실제 생성 확인 |
| 6 | 설치 관리: `IDependencyChecker`/`IComfyInstaller`/`IModelDownloader`/`IComfyServerManager` + 마법사 스텝 1~4 UI | 클린 PC 시나리오 수동 검증 |
| 7 | 설정 영속화·모델 목록 표시(`GET /models/*` 연동)·중단·오류 UX 정리 | 회귀 테스트 통과 |
| 8 | 패키징: `dotnet publish ... -r win-x64 --self-contained true -p:PublishSingleFile=true` | 단독 실행 파일 |

## 9. 테스트 전략 (DeepClaudeAuto 패턴 유지)

- Core만 테스트. `Mock<IComfyApiClient>`/`Mock<IComfyWebSocketClient>` + `NullLogger<T>.Instance` 조합
- 파싱 계열(JSON 모델, 바이너리 프리뷰 헤더, workflow 치환)은 **실제 ComfyUI 서버에서 캡처한 샘플 메시지**를 고정 fixture로 사용
- `IModelDownloader`는 로컬 파일 서버(`HttpListener`)로 진행률·재시도 검증
- `IPromptRunner`는 가짜 WS 이벤트 재생으로 성공·오류·중단 3경로 검증

## 10. 리스크·주의사항

- **대용량 다운로드**: 체크포인트 2~24GB — 실패·네트워크 끊김 대비 `.part` 재개, 진행률 UI 필수
- **프리뷰 바이너리 헤더 포맷**: 문서마다 4바이트/8바이트 상이 → Phase 3에서 서버 소스 기준 확정 후 파서 고정
- **WS 끊김**: 프리뷰 수신 중 연결 종료 시 자동 재연결 + 미리보기 스킵(중단으로 간주하지 않음). `POST /history`로 상태 교차 검증
- **VRAM 부족**: `execution_error`/프로세스 크래시 → `--lowvram` 재시작 안내
- **torch 설치 실패**(클론 방식): CUDA 버전·드라이버 불일치가 주원인 — 오류 메시지에 해결 가이드 포함
- **같은 모델을 서버가 로드한 상태에서 파일 교체 금지**: 모델 다운로드는 서버 중지 후 수행하도록 순서 강제 (설치 마법사 단계 순서가 이를 보장)
- **이미지 표시 메모리**: BitmapImage 스트림 로드 + `Freeze()`, 대형 이미지 축소 표시
- UI 텍스트·로그·코드 주석은 전부 **한국어** (DeepClaudeAuto 관례)
- `src/**/obj/` 등 git 추적 불필요 파일은 커밋 금지

## 11. 참고 자료

- ComfyUI API 문서: [WebSocket API Overview](https://mintlify.wiki/Comfy-Org/ComfyUI/api/websocket-overview), [DeepWiki: WebSocket Protocol](https://deepwiki.com/Comfy-Org/ComfyUI/7.2-websocket-protocol), [REST API Endpoints](https://deepwiki.com/hiddenswitch/ComfyUI/13.1-rest-api-endpoints)
- [ComfyUI API: The Complete Developer's Guide](https://www.runflow.io/blog/comfyui-api-developer-guide)
- portable 배포판: `comfyanonymous/ComfyUI` GitHub Releases

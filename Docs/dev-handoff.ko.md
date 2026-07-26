# 장비 이전 핸드오프

*2026-07-27 작성. 다른 PC에서 clone/pull 받아 이어서 작업할 때 필요한, 리포 바깥에 있던 정보 전부.*

## 1. 새 PC 셋업

- **Unity 6000.5.3f1** (정확히 이 버전 — `ProjectSettings/ProjectVersion.txt`) + 모듈: **Android Build Support**(OpenXR/IL2CPP/ARM64, Quest용), **WebGL**, Windows IL2CPP
  - WebGL 모듈은 Hub headless 설치가 실패한 전적 있음 — 안 되면 `%APPDATA%\UnityHub\downloads\UnitySetup-WebGL-Support-*.exe` 직접 실행
  - **모듈 설치 직후 그 에디터 인스턴스로 빌드하면 "Build target not supported" — 에디터 재시작 필수**
- Python + `pip install edge-tts` (나레이션 재생성용)
- Node/npx (Netlify 배포용)

## 2. 첫 pull 직후 할 일

1. Unity로 프로젝트 열기 → 신규 에셋 임포트 대기
2. **`.meta` 커밋**: 커밋 `00f6af6`의 신규 에셋 57개(스크립트 4·mp3 48 등)는 meta 없이 커밋됨.
   새 PC의 에디터가 GUID를 생성하므로 **임포트 끝나면 `Assets/**/*.meta` 신규분을 바로 커밋·푸시**할 것.
   아직 어떤 씬도 이 에셋들을 참조하지 않아 GUID가 어느 쪽에서 생겨도 무해하지만, 늦게 커밋하면
   두 PC가 서로 다른 GUID를 만들어 충돌한다. (이전 PC는 pull 받은 meta를 채택 — 로컬 생성분 폐기)
3. 컴파일 확인 — `00f6af6`의 C# 5파일은 리뷰만 거치고 에디터 컴파일 검증 전임

## 3. 빌드 (Tools/Cosmos 메뉴 또는 헤드리스)

상세는 README "Building" 절. 요점만:

```
Unity.exe -batchmode -quit -projectPath <repo> -buildTarget Android \
          -executeMethod MilkyWay.WebGLSiteBuild.BuildAndroid -logFile android.log
```

- 메서드: `Build`(WebGL, 데스크톱 5씬) / `BuildAndroid`(Quest APK, **MRTitle 부팅**) / `BuildWindows`
- 에디터가 열려 있으면 배치모드는 즉시 죽음(프로젝트 락). 판정은 `Temp/UnityLockfile` 배타 열기로
- 성공 판정은 로그의 `[…Build] Succeeded` 줄 — **산출물 타임스탬프는 양방향으로 거짓말함**
  (Windows exe는 에디터 설치본 mtime 유지, 증분 WebGL은 wasm 미변경)
- PowerShell에서 `& Unity.exe`는 대기 안 함(GUI 서브시스템) — `Start-Process -Wait`
- 열린 에디터로 빌드: `Temp/cosmos-autobuild.txt`에 `webgl|android|windows` 쓰고 에디터 포커스 (AutoBuildTrigger)
- **타겟 전환 churn 주의**: `ProjectSettings.asset`의 `preloadedAssets` 재기록 + `Assets/XR/` 시뮬레이션
  에셋 삭제가 일어남 — **커밋 말고 `git checkout --`로 되돌릴 것** (안 그러면 Quest XR 설정 깨짐)
- Quest 설치: `adb install -r Builds/Android/CosmosEdu.apk`

## 4. 웹 배포 (Netlify)

- 라이브: **https://cosmos-edu-783.netlify.app** · 계정: Google 로그인(개인 지메일) · `.netlify/`는 gitignore라 새 PC엔 없음
- 새 PC 최초 1회: `npx -y netlify-cli login` (브라우저 인증)
- 배포:
  ```
  npx -y netlify-cli deploy --prod --dir Builds/WebGL --site c8efa062-adc1-4de3-838b-9cef49e8d6f0
  ```
  **`--site`는 이름 문자열이면 Not Found — 이 ID 필수.** gzip 헤더는 빌드에 포함되는 `_headers`가 처리
  (원본: `Assets/WebGLTemplates/CosmosEdu/_headers`). GitHub Pages는 data.gz가 100MB 제한 초과라 불가

## 5. 나레이션 TTS

- 기존 전시: `Tools/generate_nebula_narration.py` 패턴 (C# `NarrationLines*` 배열 = 원본, 자막=음성 규약)
- MR 도슨트: `Tools/generate_mr_solar_narration.py` → `Assets/MilkyWay/Audio/NarrationMR/` (**Resources 금지** — 웹에 실림)
- 보이스: SunHi(한)/Jenny(영)/Nanami(일)/Xiaoxiao(중)

## 6. Claude Code 메모리 이전

Claude의 프로젝트 메모리(빌드·렌더링·MR 교훈 다수)는 리포가 아니라 로컬에 있음:

```
%USERPROFILE%\.claude\projects\D--Unity-Work-BlackHoleEdu\memory\   (5개 .md)
```

이전 방법: 이 폴더를 새 PC의 같은 위치로 복사. **폴더명 = 프로젝트 절대경로의 슬러그**
(구분자·특수문자 → `-`)이므로 새 PC의 경로가 다르면 폴더명도 맞출 것:

- `D:\Unity_Work\BlackHoleEdu` → `D--Unity-Work-BlackHoleEdu`
- 예: `C:\Work\BlackHoleEdu`에 클론했다면 → `C--Work-BlackHoleEdu`

복사 예 (이전 PC에서 USB/클라우드로, 또는 새 PC에서 원격 복사):

```powershell
Copy-Item "$env:USERPROFILE\.claude\projects\D--Unity-Work-BlackHoleEdu" `
          "<새 위치>\.claude\projects\<새 슬러그>" -Recurse
```

복사하지 않아도 치명적이진 않음 — 핵심 운영 정보는 이 문서와 README에 있고, 상세 교훈은
새 세션의 Claude가 다시 쌓으면 됨. 단 메모리에만 있는 세부(셰이더 함정, 검증 기법 아카이브)는 소실.

## 7. 이전 PC에 남은 정리

- 미커밋 churn 4파일: `git checkout -- ProjectSettings/ProjectSettings.asset ProjectSettings/UnityConnectSettings.asset "Assets/Settings/Project Configuration/Standalone Balanced Preset.asset" "Assets/Settings/Project Configuration/Standalone Performant Preset.asset"`
- 이후 이 PC에서도 작업하려면 새 PC가 커밋한 `.meta`를 pull 받고, 그 사이 로컬 에디터가
  같은 에셋에 meta를 만들어놨다면 pull 쪽을 채택

## 8. 현재 작업 상태 (2026-07-27 04시 기준)

- 브랜치 `mr-education-layer` = origin = `00f6af6` (main은 `6b4e6bf`, 뒤처짐)
- MR 리디자인: 기획서 2편(`Docs/MR-*.ko.md`) + P0 프레임 정책 + 도슨트 오브 + 태양계 대본까지 구현
- 미검증: `00f6af6`의 C# 컴파일(§2), Quest 실기기 전반(특히 `8b5a800`의 MR 타이틀 머리 기준 배치)
- 다음 후보: 기기 프로파일링(HUD로 스텝 캡 44/52 검증) → P0b 하프해상도 볼륨 → P2 태양계 씬 리빌드

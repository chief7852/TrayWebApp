# MSIX 빌드 및 Microsoft Store 제출 가이드

TrayWebApp은 기존 EXE/Inno Setup 배포와 별도로 Microsoft Store 제출용 MSIX 패키지를 만들 수 있습니다.

## 왜 MSIX가 필요한가

Microsoft Store에 EXE/MSI 설치 파일을 제출하면 설치 파일과 포함된 PE 파일을 신뢰된 CA 코드 서명 인증서로 서명해야 합니다. 반면 MSIX로 Store에 제출하면 인증 통과 후 Microsoft가 패키지를 다시 서명하고 호스팅합니다. 따라서 개인 개발자는 EXE 서명 인증서 문제를 피하려면 MSIX 제출 경로가 더 현실적입니다.

## 준비물

- Windows 10/11
- .NET 8 SDK
- Windows SDK의 `makeappx.exe`

Windows SDK가 없으면 다음 명령으로 설치합니다.

```powershell
winget install --id Microsoft.WindowsSDK.10.0.18362 --accept-package-agreements --accept-source-agreements
```

## 테스트 MSIX 만들기

```powershell
cd C:\test\TrayWebApp
.\build-msix.ps1
```

생성 위치:

```text
publish\msix\TrayWebApp_1.0.0.0_x64.msix
publish\msix\TrayWebApp_1.0.0.0_x64.msixupload
```

기본값으로 만든 MSIX는 로컬 검증용입니다. Microsoft Store 제출용으로는 Partner Center의 패키지 ID 값을 넣어 다시 만들어야 합니다.

## Store 제출용 MSIX 만들기

Partner Center에서 앱의 패키지/ID 정보에 들어가 다음 값을 확인합니다.

- Package/Identity/Name
- Publisher
- Publisher display name

확인한 값을 아래 명령에 넣습니다. 아래 예시의 `...` 부분은 설명이 아니라 실제 Partner Center 값으로 바꿔야 합니다. `<`와 `>` 문자는 넣지 않습니다.

```powershell
.\build-msix.ps1 `
  -PackageIdentityName "실제 Package Identity Name" `
  -Publisher "CN=실제 Publisher 값" `
  -PublisherDisplayName "실제 게시자 표시 이름" `
  -Version "1.0.0.0"
```

Store에는 기존 EXE URL 대신 생성된 `.msixupload` 파일을 업로드합니다. Partner Center는 `.msix`도 받을 수 있지만, Microsoft 문서에서는 Store 제출용으로 `.msixupload`를 권장합니다.

## 로컬 설치 테스트

Store 밖에서 직접 설치하려면 MSIX는 신뢰된 인증서로 서명되어 있어야 합니다. 테스트용 자체 서명 인증서는 개발 PC에서만 신뢰하도록 등록해 사용할 수 있지만, Microsoft Store 제출 시에는 최종적으로 Store가 재서명합니다.

## 현재 MSIX 패키지 구성

- 앱 유형: Full Trust desktop app
- 실행 파일: `TrayWebApp.exe`
- 권한:
  - `internetClient`
  - `runFullTrust`
- 최소 OS: Windows 10 1809, build 17763
- WebView2: Evergreen Runtime 필요

## 주의할 점

- MSIX는 설치 파일이 `C:\Program Files\WindowsApps` 아래 읽기 전용 영역에 배치됩니다. 앱 설정과 WebView2 데이터는 사용자 AppData에 저장되어야 합니다.
- 현재 Windows 시작 시 실행 기능은 기존 EXE 배포 기준으로 구현되어 있습니다. MSIX/Store 배포에서 시작 프로그램 토글이 완전히 동일하게 동작하지 않으면, Store용 startup task manifest와 WinRT `StartupTask` 연동을 별도 적용해야 합니다.
- Inno Setup 기반 `TrayWebApp-Setup.exe`는 Store EXE 제출용이 아닙니다. Store 제출에는 MSIX 산출물을 사용하세요.
- 기존 Partner Center에서 "패키지 URL"을 입력하는 화면에 있다면 Win32 EXE/MSI 제출 흐름입니다. MSIX는 URL을 입력하는 대신 Packages 단계에서 `.msixupload` 또는 `.msix` 파일을 직접 업로드하는 흐름이어야 합니다.

## 참고 문서

- Microsoft Store 코드 서명 옵션: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- MakeAppx로 MSIX 만들기: https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool
- 데스크톱 앱 수동 MSIX 변환: https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion

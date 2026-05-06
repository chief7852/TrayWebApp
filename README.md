# TrayWebApp

TrayWebApp은 Windows 시스템 트레이에서 실행되는 작은 WebView2 기반 웹앱 브라우저입니다. 자주 쓰는 웹 서비스를 전용 미니 창처럼 빠르게 열고, 탭, 투명도, 항상 위, 창 배치, 앱별 세션을 조정해 사용할 수 있습니다.

## 다운로드

최신 배포 파일은 GitHub Releases에서 받을 수 있습니다.

- [설치 파일 다운로드](https://github.com/chief7852/TrayWebApp/releases/latest/download/TrayWebApp-Setup.exe)
- [포터블 ZIP 다운로드](https://github.com/chief7852/TrayWebApp/releases/latest/download/TrayWebApp-Portable-win-x64.zip)
- [릴리스 페이지 열기](https://github.com/chief7852/TrayWebApp/releases/latest)

## 미리보기

![TrayWebApp 메인 브라우저 창](docs/images/traywebapp-main.png)
![TrayWebApp 메인 브라우저 창](docs/images/traywebapp-main2.png)
![TrayWebApp 설정 창](docs/images/traywebapp-setting1.png)
![TrayWebApp 설정 창](docs/images/traywebapp-setting2.png)

## 주요 기능

- 시스템 트레이 기반 실행
- WebView2 기반 웹앱 브라우징
- 여러 웹앱 창 동시 실행
- 창 하단 탭, 새 탭, 탭 닫기, 닫은 탭 복구
- 앱별 마지막 탭 목록 복원
- 앱 추가, 수정, 삭제, 순서 변경
- 앱 관리 화면 검색
- 빠른 앱 전환 팔레트
- 전역 단축키로 앱 실행 및 앱 검색
- 앱별 URL, 창 크기, User-Agent, 항상 위 설정
- 앱별 독립 세션 옵션
- 창 크기 프리셋 및 창 배치 프리셋
- 전방향 창 크기 조절
- 항상 위 고정
- 창 투명도 조절
- 주소창 표시/숨김
- 최근 사용 앱 표시
- 다운로드 폴더 설정
- 카메라, 마이크, 위치, 알림 권한 요청 처리
- Windows 시작 시 자동 실행 옵션
- 포터블 exe, zip, 설치 파일 빌드 지원

## 사용 방법

앱을 실행하면 Windows 시스템 트레이에 TrayWebApp 아이콘이 표시됩니다.

- 트레이 아이콘 왼쪽 클릭: 활성 창 열기/숨기기
- 트레이 아이콘 오른쪽 클릭: 앱 목록, 열린 창, 최근 앱, 창 크기, 창 배치, 투명도, 설정, 종료 메뉴
- 앱 메뉴 선택: 해당 웹앱 창 열기 또는 이미 열린 창 앞으로 가져오기
- 하단 `+` 버튼: 새 탭 열기
- 탭 `x`: 탭 닫기
- 탭 가운데 클릭: 탭 닫기
- 탭 우클릭: 탭 복제, 탭 닫기, 다른 탭 닫기, 오른쪽 탭 닫기, 닫은 탭 다시 열기
- 앱 관리 화면의 `독립 세션 사용`: 해당 앱만 별도 쿠키/로그인 저장소 사용

## 단축키

- `Ctrl + Alt + Space`: 활성 창 열기/숨기기
- `Ctrl + Alt + 1~9`: 등록된 웹앱 빠른 실행
- `Ctrl + Alt + K`: 전역 앱 검색 팔레트 열기
- `Ctrl + K`: 현재 창에서 앱 검색 팔레트 열기
- `Ctrl + T`: 새 탭
- `Ctrl + W`: 현재 탭 닫기
- `Ctrl + Tab`: 다음 탭
- `Ctrl + Shift + Tab`: 이전 탭
- `Ctrl + Shift + T`: 닫은 탭 다시 열기
- `Ctrl + L`: 주소창 포커스
- `Ctrl + + / -`: 확대/축소
- `Ctrl + 0`: 확대 배율 초기화
- `Alt + Left / Right`: 뒤로/앞으로
- `Esc`: 창 숨기기
- `F5`: 새로고침
- `F12`: 개발자 도구 열기

## 실행 요구사항

- Windows 10/11
- Microsoft Edge WebView2 Runtime
- 개발/빌드 시 .NET 8 SDK
- 설치 파일 생성 시 Inno Setup 6

대부분의 Windows 10/11 환경에는 WebView2 Runtime이 이미 설치되어 있습니다. 없는 PC에서는 Microsoft Edge WebView2 Runtime을 별도로 설치해야 합니다.

## 개발 환경에서 실행

```powershell
git clone https://github.com/chief7852/TrayWebApp.git
cd TrayWebApp
dotnet restore
dotnet run --project src\TrayWebApp.App
```

## 배포 파일 만들기

릴리스 빌드는 다음 명령으로 생성합니다.

```powershell
cd TrayWebApp
.\build-release.ps1
```

생성 위치:

- 단일 실행 파일: `publish\win-x64-final\TrayWebApp.exe`
- 포터블 zip: `publish\TrayWebApp-1.0.0-win-x64-*.zip`
- 설치 파일: `publish\installer\TrayWebApp-Setup-1.0.0.exe`

Inno Setup 6이 설치되어 있지 않으면 설치 파일은 생성되지 않고, exe와 zip까지만 생성됩니다.

## Microsoft Store용 MSIX 만들기

Microsoft Store 제출에는 EXE 설치 파일 대신 MSIX 패키지를 사용할 수 있습니다.

```powershell
cd TrayWebApp
.\build-msix.ps1
```

생성 위치:

- Store 업로드 권장 파일: `publish\msix\TrayWebApp_1.0.0.0_x64.msixupload`
- 개별 MSIX 패키지: `publish\msix\TrayWebApp_1.0.0.0_x64.msix`

Store 제출 전에는 Partner Center의 Package Identity Name과 Publisher 값을 넣어 다시 빌드해야 합니다. 자세한 내용은 [MSIX.md](MSIX.md)를 확인하세요.

## 설치 방법

```text
1. TrayWebApp-Setup-1.0.0.exe를 실행합니다.
2. Windows SmartScreen 경고가 나오면 "추가 정보" > "실행"을 선택합니다.
3. 설치 마법사에서 설치를 진행합니다.
4. 설치 후 시스템 트레이의 TrayWebApp 아이콘을 클릭해 사용합니다.
```

설치 없이 사용하려면 포터블 zip을 압축 해제한 뒤 `TrayWebApp.exe`를 실행하면 됩니다.

## 데이터 저장 위치

사용자 설정, 등록된 웹앱, WebView2 세션 데이터는 다음 경로에 저장됩니다.

```text
%LOCALAPPDATA%\TrayWebApp
```

앱별 독립 세션을 켠 경우 해당 앱의 WebView2 프로필은 다음 하위 폴더에 저장됩니다.

```text
%LOCALAPPDATA%\TrayWebApp\WebView2Profiles
```

앱을 삭제해도 이 데이터 폴더가 남아 있으면 설정, 로그, 세션이 유지될 수 있습니다.

## 배포 시 주의사항

- 코드 서명 인증서가 없으면 Windows SmartScreen 경고가 표시될 수 있습니다.
- 항상 위 기능은 일반 데스크톱 창 위에서 동작합니다. 관리자 권한 앱, 보안 데스크톱, 일부 전체 화면 앱 위까지는 Windows 정책상 보장되지 않습니다.
- GitHub 저장소에는 빌드 산출물인 `publish/`, `bin/`, `obj/`를 올리지 않습니다. 배포 파일은 로컬에서 `build-release.ps1`로 생성합니다.

## 라이선스

MIT License

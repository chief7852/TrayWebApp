# TrayWebApp

TrayWebApp은 Windows 시스템 트레이에서 실행되는 WebView2 기반 미니 웹앱 브라우저입니다. 자주 쓰는 웹 서비스를 전용 창처럼 빠르게 열고, 탭, 투명도, 항상 위, 창 배치, 앱별 세션, 다크/라이트 테마를 조정해 사용할 수 있습니다.

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
- 앱별 독립 세션 옵션 및 독립 세션 초기화
- 현재 창 모바일 보기 전환
- 다크 모드와 라이트 모드
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
- 트레이 아이콘 오른쪽 클릭: 앱 목록, 열린 창, 최근 앱, 창 크기, 창 배치, 투명도, 테마, 설정, 종료 메뉴
- 앱 메뉴 선택: 해당 웹앱 창 열기 또는 이미 열린 창 앞으로 가져오기
- 상단 해 모양 아이콘: 다크 모드와 라이트 모드 즉시 전환
- 하단 `+` 버튼: 새 탭 열기
- 탭 `x`: 탭 닫기
- 탭 가운데 클릭: 탭 닫기
- 탭 우클릭: 탭 복제, 탭 닫기, 다른 탭 닫기, 오른쪽 탭 닫기, 닫은 탭 다시 열기
- 앱 관리 화면의 `독립 세션 사용`: 해당 앱만 별도 쿠키/로그인 저장소 사용
- 앱 관리 화면의 `세션 초기화`: 독립 세션 앱의 쿠키와 로그인 데이터 삭제
- 상단 휴대폰 아이콘 또는 트레이 메뉴의 `모바일 보기`: 현재 창을 모바일 User-Agent와 모바일 크기로 전환
- 설정 화면의 `테마`: 다크 모드와 라이트 모드 전환

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
dotnet build
dotnet run --project src\TrayWebApp.App
```

## 배포 빌드

포터블 exe, zip, 설치 파일을 생성합니다.

```powershell
.\build-release.ps1
```

생성 위치:

- 실행 파일: `publish\win-x64-final\TrayWebApp.exe`
- 포터블 zip: `publish\TrayWebApp-1.0.2-win-x64-*.zip`
- 설치 파일: `publish\installer\TrayWebApp-Setup-1.0.2.exe`

설치 파일 생성을 건너뛰려면 다음처럼 실행합니다.

```powershell
.\build-release.ps1 -SkipInstaller
```

## MSIX 빌드

Microsoft Store 제출용 MSIX 패키지를 만들 수 있습니다.

```powershell
.\build-msix.ps1
```

생성 위치:

- Store 업로드 권장 파일: `publish\msix\TrayWebApp_1.0.2.0_x64.msixupload`
- 개별 MSIX 패키지: `publish\msix\TrayWebApp_1.0.2.0_x64.msix`

자세한 내용은 [MSIX.md](MSIX.md)를 참고하세요.

## 설치

1. `TrayWebApp-Setup-1.0.2.exe`를 실행합니다.
2. 설치 마법사를 진행합니다.
3. 설치 후 TrayWebApp을 실행하면 트레이 아이콘이 표시됩니다.
4. 필요하면 설정에서 `Windows 시작 시 실행`을 켭니다.

포터블 버전은 zip을 풀고 `TrayWebApp.exe`를 직접 실행하면 됩니다.

## 데이터 저장 위치

앱 설정, 웹앱 목록, 로그, WebView2 사용자 데이터는 사용자 AppData 아래에 저장됩니다.

```text
%APPDATA%\TrayWebApp
```

독립 세션을 사용하는 앱은 앱별 WebView2 프로필 폴더를 별도로 사용합니다.

## 개인정보

TrayWebApp은 자체 서버로 사용자 데이터를 전송하지 않습니다. 웹앱 콘텐츠는 사용자가 등록한 웹사이트와 WebView2를 통해 직접 통신합니다.

자세한 내용은 [PRIVACY.md](PRIVACY.md)를 참고하세요.

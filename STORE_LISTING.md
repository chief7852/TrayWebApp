# Microsoft Store 등록 정보

`PaxListing` 오류가 표시되면 Partner Center의 **Store listing / 스토어 등록 정보** 섹션을 먼저 확인하세요. 보통 설명, 스크린샷, 로고, 언어별 등록 정보 중 필수 항목이 비어 있거나 이미지 규격이 맞지 않을 때 발생합니다.

## 제출 전 필수 확인

- Store listing 언어를 최소 1개 추가하고 해당 언어 페이지를 완료합니다.
- 설명 Description을 입력합니다.
- Desktop 스크린샷 PNG를 최소 1개 업로드합니다.
- 스크린샷은 1366x768 이상이어야 합니다.
- Store logo / Box art 이미지를 업로드합니다.
- 개인정보처리방침 URL을 입력합니다.
- 지원 URL 또는 지원 이메일을 입력합니다.
- 패키지 URL은 리디렉션 없는 직접 다운로드 URL을 사용합니다.

## 패키지 URL

Microsoft Store의 패키지 URL에는 GitHub Releases 링크 대신 아래 GitHub Pages 직접 파일 URL을 사용하세요.

```text
https://chief7852.github.io/TrayWebApp/download/TrayWebApp-Setup.exe
```

확인된 응답:

```text
HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Length: 67455845
Location 헤더 없음
```

## 개인정보처리방침 URL

```text
https://chief7852.github.io/TrayWebApp/privacy.html
```

## 설치 명령

Partner Center에서 자동 설치 명령을 요구하면 다음 값을 사용하세요.

```text
TrayWebApp-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
```

## 제거 명령

Partner Center에서 제거 명령을 요구하면 Inno Setup 기본 제거 프로그램 기준으로 다음 형식을 사용합니다.

```text
"%LOCALAPPDATA%\Programs\TrayWebApp\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

## 앱 이름

```text
TrayWebApp
```

## 짧은 설명

```text
Windows 트레이에서 자주 쓰는 웹앱을 빠르게 열 수 있는 WebView2 기반 미니 브라우저입니다.
```

## 설명

```text
TrayWebApp은 Windows 시스템 트레이에서 실행되는 WebView2 기반 미니 브라우저입니다.

자주 사용하는 웹사이트와 웹앱을 작은 창으로 빠르게 열고, 창 크기, 투명도, 항상 위 고정, 주소창 표시 여부, 전역 단축키 등을 조정해 작업 흐름에 맞게 사용할 수 있습니다.

주요 기능:
- 시스템 트레이에서 빠른 실행
- WebView2 기반 웹앱 브라우징
- 웹앱 추가, 수정, 삭제, 순서 변경
- 앱별 URL, 창 크기, User-Agent, 줌 설정
- 창 투명도 조절
- 항상 위 고정
- 주소창 표시 및 숨김
- 최근 사용 앱 표시
- 전역 단축키 지원
- 다운로드 폴더 설정
- 카메라, 마이크, 위치 등 웹 권한 요청 처리
- Windows 시작 시 자동 실행 옵션

TrayWebApp은 자체 서버로 개인정보를 수집하지 않습니다. 앱 설정, 등록한 웹앱 정보, WebView2 쿠키와 캐시는 사용자의 기기에 로컬로 저장됩니다.
```

## 기능 목록

```text
시스템 트레이 기반 미니 브라우저
WebView2 기반 웹앱 실행
창 투명도 조절
항상 위 고정
앱별 창 크기와 줌 설정
전역 단축키
다운로드 처리
Windows 시작 시 실행
```

## 검색어

```text
tray browser,mini browser,webview2,web app,system tray,productivity,browser,window,shortcut
```

## 카테고리 추천

```text
Productivity
```

또는 Partner Center 선택지에 따라:

```text
Utilities & tools
```

## 지원 정보

지원 URL:

```text
https://github.com/chief7852/TrayWebApp/issues
```

지원 이메일:

```text
chief7852@gmail.com
```

## 스크린샷 파일

스토어에는 `store-assets/screenshots` 폴더의 PNG 파일을 업로드하세요.

- `store-assets/screenshots/traywebapp-store-01.png`
- `store-assets/screenshots/traywebapp-store-02.png`
- `store-assets/screenshots/traywebapp-store-03.png`
- `store-assets/screenshots/traywebapp-store-04.png`

각 파일은 Desktop 요구사항에 맞춰 1366x768 PNG로 준비되어 있습니다.

## PaxListing 오류 체크리스트

1. Store listings에서 언어가 `Incomplete`인지 확인합니다.
2. 추가한 언어를 클릭해 Description을 입력합니다.
3. Screenshot을 최소 1개 업로드합니다.
4. 스크린샷 크기가 1366x768 이상인지 확인합니다.
5. Store logo 또는 Box art 필수 항목을 업로드합니다.
6. Privacy policy URL을 입력합니다.
7. Package URL에는 `github.com/releases/...`가 아닌 `chief7852.github.io/...` URL을 사용합니다.
8. 입력 후 각 섹션 오른쪽의 저장 버튼을 누릅니다.
9. 브라우저 캐시 문제일 수 있으니 저장 후 새로고침하거나 다른 브라우저에서 다시 제출합니다.

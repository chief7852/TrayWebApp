# TrayWebApp 배포 가이드

## 빠른 배포 빌드

PowerShell에서 프로젝트 루트로 이동한 뒤 실행합니다.

```powershell
cd C:\test\TrayWebApp
.\build-release.ps1
```

생성물은 다음 위치에 만들어집니다.

- 단일 실행 파일: `C:\test\TrayWebApp\publish\win-x64-final\TrayWebApp.exe`
- 포터블 zip: `C:\test\TrayWebApp\publish\TrayWebApp-1.0.0-win-x64-*.zip`
- 설치 파일: `C:\test\TrayWebApp\publish\installer\TrayWebApp-Setup-1.0.0.exe`

설치 파일은 Inno Setup 6이 설치되어 있을 때만 생성됩니다. 설치되어 있지 않으면 exe와 zip까지만 만들어집니다.

## 배포 전 체크리스트

- `dotnet build`와 `publish`가 오류 없이 끝나는지 확인합니다.
- 실행 중인 `TrayWebApp.exe`를 종료한 뒤 publish합니다. 실행 중인 파일은 덮어쓸 수 없습니다.
- 새 PC에서 실행할 때 Microsoft Edge WebView2 Runtime이 필요합니다. 대부분의 Windows 10/11에는 포함되어 있지만, 없는 PC에는 WebView2 Runtime을 별도로 설치해야 합니다.
- 코드 서명 인증서가 없으면 외부 배포 시 Windows SmartScreen 경고가 표시될 수 있습니다.
- 항상 위 기능은 일반 데스크톱 앱 위에서 동작합니다. 관리자 권한 앱, 보안 데스크톱, 전체 화면 독점 모드 위까지 보장하려면 동일한 권한 수준과 별도 테스트가 필요합니다.
- 앱 데이터는 `%LOCALAPPDATA%\TrayWebApp`에 저장됩니다. 배포본을 삭제해도 설정과 로그인 세션은 남을 수 있습니다.

## 수동 publish

설치 파일 없이 exe만 만들려면 다음 명령을 사용합니다.

```powershell
cd C:\test\TrayWebApp
.\publish.ps1 -Configuration Release -Runtime win-x64 -OutputDir publish\win-x64-final
```

## 배포 파일 구성

포터블 배포는 `publish\win-x64-final` 폴더 전체 또는 생성된 zip 파일을 전달하면 됩니다. 설치형 배포는 `publish\installer`의 setup exe를 전달하면 됩니다.

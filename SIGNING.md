# Microsoft Store 제출용 코드 서명 가이드

Microsoft Store에 EXE/MSI 방식으로 제출하려면 설치 파일과 그 안에 포함된 Portable Executable(PE) 파일이 코드 서명되어 있어야 합니다. 자체 서명 인증서는 허용되지 않으며, Microsoft Trusted Root Program에 포함된 CA로 이어지는 코드 서명 인증서가 필요합니다.

현재 unsigned 상태라면 Partner Center에서 다음과 같은 오류가 표시됩니다.

```text
10.2.9 Security - Package Submissions
Unsigned
Package should be signed with SHA256 or higher algorithm
```

## 선택지

### 선택지 A. EXE 제출 유지

다음 중 하나가 필요합니다.

- 공인 코드 서명 인증서: Sectigo, DigiCert, GlobalSign 등 CA에서 발급
- Microsoft Trusted Signing / Azure Artifact Signing

서명해야 하는 파일:

```text
publish\win-x64-final\TrayWebApp.exe
publish\TrayWebApp-Setup.exe
docs\download\TrayWebApp-Setup.exe
```

중요: `docs\download\TrayWebApp-Setup.exe`는 Microsoft Store 패키지 URL이 가리키는 파일입니다. 서명 후 반드시 이 파일도 signed 버전으로 교체하고 GitHub Pages에 다시 배포해야 합니다.

### 선택지 B. MSIX 제출로 전환

MSIX로 제출하면 Microsoft Store가 패키지 서명과 호스팅을 처리해 주는 장점이 있습니다. 다만 현재 EXE/MSI 제출로 만든 제품 이름을 그대로 쓰려면 Partner Center에서 기존 Win32 앱 이름을 삭제하거나 새 MSIX 앱으로 다시 만들어야 할 수 있습니다.

MSIX 전환 시 확인할 점:

- 트레이 앱 실행 방식
- 시작 프로그램 등록 방식
- WebView2 데이터 저장 경로
- 설치/제거 경험
- Windows App Certification Kit 결과

현재 앱 구조에서는 빠른 제출을 위해 **EXE 제출 유지 + 코드 서명**이 가장 단순합니다.

## 공인 코드 서명 인증서로 서명하는 방법

Windows SDK의 `signtool.exe`가 필요합니다. Visual Studio Build Tools 또는 Windows SDK를 설치하면 포함됩니다.

먼저 signtool 위치를 확인합니다.

```powershell
Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
  Where-Object { $_.FullName -match "\\x64\\signtool.exe$" } |
  Select-Object -First 1 -ExpandProperty FullName
```

PFX 인증서가 있는 경우:

```powershell
$SignTool = "C:\Program Files (x86)\Windows Kits\10\bin\<SDK-VERSION>\x64\signtool.exe"
$PfxPath = "C:\path\to\codesign.pfx"
$TimestampUrl = "http://timestamp.digicert.com"

& $SignTool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $PfxPath /p "<PFX_PASSWORD>" "publish\win-x64-final\TrayWebApp.exe"
& $SignTool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /f $PfxPath /p "<PFX_PASSWORD>" "publish\TrayWebApp-Setup.exe"
```

인증서가 Windows 인증서 저장소에 설치되어 있고 subject 이름으로 서명하는 경우:

```powershell
$SignTool = "C:\Program Files (x86)\Windows Kits\10\bin\<SDK-VERSION>\x64\signtool.exe"
$TimestampUrl = "http://timestamp.digicert.com"

& $SignTool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /n "<CERT_SUBJECT_NAME>" "publish\win-x64-final\TrayWebApp.exe"
& $SignTool sign /fd SHA256 /td SHA256 /tr $TimestampUrl /n "<CERT_SUBJECT_NAME>" "publish\TrayWebApp-Setup.exe"
```

서명 확인:

```powershell
Get-AuthenticodeSignature "publish\win-x64-final\TrayWebApp.exe"
Get-AuthenticodeSignature "publish\TrayWebApp-Setup.exe"

& $SignTool verify /pa /v "publish\win-x64-final\TrayWebApp.exe"
& $SignTool verify /pa /v "publish\TrayWebApp-Setup.exe"
```

결과가 `Valid`여야 합니다.

## Microsoft Trusted Signing / Azure Artifact Signing 사용

Trusted Signing을 쓰려면 Azure에서 Trusted Signing 계정, 인증서 프로필, ID 검증이 필요합니다.

Trusted Signing으로 SignTool을 사용하는 일반적인 형태는 다음과 같습니다.

```powershell
$SignTool = "C:\Program Files (x86)\Windows Kits\10\bin\<SDK-VERSION>\x64\signtool.exe"
$Dlib = "C:\Users\<USER>\AppData\Local\Microsoft\MicrosoftTrustedSigningClientTools\x64\Azure.CodeSigning.Dlib.dll"
$Metadata = "C:\path\to\trusted-signing-metadata.json"

& $SignTool sign /v /fd SHA256 /td SHA256 /tr "http://timestamp.acs.microsoft.com" /dlib $Dlib /dmdf $Metadata "publish\win-x64-final\TrayWebApp.exe"
& $SignTool sign /v /fd SHA256 /td SHA256 /tr "http://timestamp.acs.microsoft.com" /dlib $Dlib /dmdf $Metadata "publish\TrayWebApp-Setup.exe"
```

메타데이터 파일 예시는 다음과 같습니다. 실제 값은 Azure Portal의 Trusted Signing 리소스 정보로 바꿔야 합니다.

```json
{
  "Endpoint": "https://<region>.codesigning.azure.net",
  "CodeSigningAccountName": "<account-name>",
  "CertificateProfileName": "<certificate-profile-name>"
}
```

## 서명 후 Store 패키지 URL 파일 갱신

서명한 설치 파일을 GitHub Pages 다운로드 경로로 복사합니다.

```powershell
Copy-Item "publish\TrayWebApp-Setup.exe" "docs\download\TrayWebApp-Setup.exe" -Force
git add docs\download\TrayWebApp-Setup.exe
git commit -m "Update signed Store package"
git push origin main
```

배포 후 URL을 확인합니다.

```powershell
curl.exe -I https://chief7852.github.io/TrayWebApp/download/TrayWebApp-Setup.exe
```

확인할 값:

```text
HTTP/1.1 200 OK
Location 헤더 없음
```

## 최종 제출 전 체크리스트

- `publish\win-x64-final\TrayWebApp.exe` 서명 상태가 `Valid`
- `publish\TrayWebApp-Setup.exe` 서명 상태가 `Valid`
- `docs\download\TrayWebApp-Setup.exe`가 signed 설치 파일로 교체됨
- GitHub Pages URL이 `200 OK`이고 리디렉션 없음
- Partner Center 패키지 URL:

```text
https://chief7852.github.io/TrayWebApp/download/TrayWebApp-Setup.exe
```

## 참고

- EXE/MSI 제출은 개발자가 Authenticode 서명을 직접 해야 합니다.
- MSIX를 Microsoft Store로 제출하면 Microsoft가 Store 배포용 서명과 호스팅을 처리합니다.
- self-signed 인증서는 Microsoft Store 10.2.9 요구사항을 만족하지 않습니다.

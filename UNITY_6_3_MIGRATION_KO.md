# Unity 6.3 LTS 전환 기록

## 목표 버전

- Editor: Unity 6.3 LTS `6000.3.20f1`
- Changeset: `c9ba695d4f07`
- Windows 빌드: IL2CPP 지원 모듈 설치
- 원본 보관: `work/AAA_EconomySLG_Starter_2022_Backup`

## 적용된 변경

- `ProjectVersion.txt`를 Unity 6.3 LTS로 변경
- Unity 6.3 Editor에 내장된 버전에 맞춰 uGUI를 `2.0.0`으로 변경
- Unity 6.3 Editor에 내장된 버전에 맞춰 Test Framework를 `1.6.0`으로 변경
- Unity에서 지원되지 않던 `PriceInput`의 `init` 접근자를 일반 프로퍼티로 변경
- Domain과 Application 전체를 Unity 6.3 Roslyn 컴파일러로 독립 컴파일
- EditMode 테스트 소스 전체를 Unity 6.3 NUnit 참조로 독립 컴파일

## 현재 검증 결과

- Domain + Application C# 컴파일: 통과
- EditMode 테스트 어셈블리 컴파일: 통과
- Unity Editor 프로젝트 임포트: 실행을 시도했으나 라이선스 없음으로 종료 코드 198
- EditMode 29개 실제 실행: Personal 라이선스 활성화 후 실행 필요
- Windows IL2CPP Player 빌드: Scene과 라이선스 준비 후 실행 필요

실행 로그는 작업 루트의 `unity_6_3_upgrade.log`에 있다. 라이선스 단계에서 종료되어 프로젝트 파일의 API 자동 변환이나 에셋 임포트는 수행되지 않았다.

## 라이선스 활성화 후 실행 순서

1. Unity Hub에서 Unity ID 로그인
2. Unity Personal 라이선스 활성화
3. Unity 6.3으로 프로젝트를 열어 API Updater와 Package Manager 완료 대기
4. Console의 Error를 0개로 만든다.
5. Test Runner에서 `Game.Tests.EditMode` 29개를 실행한다.
6. Windows IL2CPP 개발 빌드를 생성한다.
7. 동일 초기 데이터로 30턴을 두 번 실행해 턴별 체크섬을 비교한다.

## 롤백

Unity 6.3 전환본에 문제가 생기면 현재 폴더를 덮어쓰지 말고, 보관된 2022 프로젝트에서 필요한 데이터만 새 브랜치나 새 복제본으로 옮긴다. `Library`, `Temp`, `Logs`, `obj` 폴더는 버전 간 공유하지 않는다.

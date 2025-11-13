# Changelog
すべての重要な変更はこのファイルに記録されます。  
この形式は [Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) に準拠し、  
バージョン管理には [Semantic Versioning](https://semver.org/lang/ja/) を採用しています。

---
## [1.0.0] - 2025-10-27
### Added
- 初版リリース 🎉  
- VRUGD Cluster 開発環境の自動セットアップ機能を追加  
  - Scoped Registries に `https://registry.npmjs.com` を自動登録  
  - `mu.cluster.cluster-creator-kit` パッケージを自動導入  
  - `Newtonsoft.Json`（`com.unity.nuget.newtonsoft-json`）を依存関係に自動追加  
  - `Color Space` を `Linear` に設定  
- 初回起動時に自動セットアップダイアログを表示  
- メニューから手動実行可能：  
  **VRUGD → Cluster SetupTools → セットアップを実行**
- Unity 6000.2.0f1 以降に対応  

## [1.0.1] - 2025-10-27
- UPM (Git URL) 経由での導入に対応

## [1.0.2] - 2025-10-27
- 微細な不具合の修正

## [1.0.3] - 2025-10-27
- Cluster SDKのバージョンを3.0.0に対応
## [1.0.4] - 2025-10-27
- Bugfix
## [1.0.5] - 2025-10-27
- VRChat用ワールドをクラスターワールドとして簡単にセットアップできるように互換Prefabを追加。

---

## [今後の予定]
### Planned
- UI Toolkit を用いたセットアップウィザード表示  
- セットアップログの保存機能 (`ProjectSettings/VRUGDSetup.log`)  
- GitHub Actions による自動ビルド・タグ配信  
- セットアップ完了時にロゴ付き完了画面を表示

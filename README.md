# NvidiaPersonasJapanDataVirtualSurvey_Gemini

## 概要

このプロジェクトは、NVIDIA の **Nemotron-Personas-Japan** データセットを活用して、仮想的なアンケート調査を実施するための C# ライブラリとコンソールアプリケーションです。様々な背景を持つ日本の合成ペルソナに対して、Google Gemini API を使用してアンケート調査をシミュレートできます。小規模な調査に最適化されており、逐次実行で各ペルソナから回答を収集します。

## 主な機能

- **多様なペルソナデータの活用**: NVIDIA の Nemotron-Personas-Japan データセットから、年齢、性別、職業、居住地などの属性を持つ合成ペルソナを読み込み
- **3つのアンケート形式をサポート**:
  - **自由記述式**: 指定した文字数で自由に意見を述べる形式
  - **Yes/No 形式**: 二択で回答する形式
  - **選択式**: 複数の選択肢から単一または複数選択する形式
  ※ISurveyRequestインターフェースを実装することで、独自のアンケート形式も追加可能
- **Google Gemini API 統合**: 小規模なアンケートを効率的に処理（逐次実行）
- **自動データ取得**: Hugging Face から Parquet ファイルを自動ダウンロード

## プロジェクト構成

```
NvidiaPersonasJapanDataVirtualSurvey/
├── NvidiaPersonasJapanDataVirtualSurvey.Console/  # コンソールアプリケーション
│   └── Program.cs                                   # サンプル実行コード
└── NvidiaPersonasJapanDataVirtualSurvey.Core/      # コアライブラリ
    ├── SurveyService.cs                             # アンケート実行サービス
    ├── GeminiChatService.cs                         # Google Gemini チャット処理
    ├── DataLoader.cs                                # ペルソナデータ読み込み
    └── Models/                                       # データモデル
        ├── FreeTextSurveyRequest.cs                  # 自由記述式アンケート
        ├── YesNoSurveyRequest.cs                     # Yes/No形式アンケート
        └── OptionSelectSurveyRequest.cs              # 選択式アンケート
```

## セットアップ

### 必要な環境

- .NET 9.0 SDK
- Google AI Studio API キー

### Google Gemini API の設定

1. Google AI Studio で API キーを取得: https://aistudio.google.com/app/apikey

2. **APIキーの設定**（以下の方法から選択）:

#### 方法A: .env ファイル（推奨・簡単）
```bash
# プロジェクトルートに .env ファイルを作成
cp .env.example .env

# .env ファイルを編集してAPIキーを設定
GEMINI_API_KEY=your_actual_api_key_here
GEMINI_MODEL=gemini-2.5-flash
# アンケート設定
SAMPLE_SIZE=5

# ペルソナ選択設定（実験用）
RANDOM_SEED=12345  # 固定値を設定すると毎回同じペルソナが選ばれる
```
```

#### 方法B: dotnet user-secrets（セキュア）
```bash
cd NvidiaPersonasJapanDataVirtualSurvey.Console
dotnet user-secrets set "GEMINI_API_KEY" "your_actual_api_key_here"
dotnet user-secrets set "GEMINI_MODEL" "gemini-2.5-flash"
```

#### 方法C: 環境変数
```bash
# macOS/Linux
export GEMINI_API_KEY="your_actual_api_key_here"
export GEMINI_MODEL="gemini-2.5-flash"

# Windows
set GEMINI_API_KEY=your_actual_api_key_here
set GEMINI_MODEL=gemini-2.5-flash
```

### ⚠️ セキュリティ上の注意事項

- **APIキーをソースコードに直接書かないでください**
- `.env` ファイルは `.gitignore` に含まれており、GitHubにプッシュされません
- 本番環境では `dotnet user-secrets` または環境変数の使用を推奨

### 🔬 実験・比較用機能

**同じペルソナで複数回テスト**（条件変更の影響を確認）:
```bash
# .envで固定シード値を設定
RANDOM_SEED=12345

# 毎回同じ5人のペルソナが選ばれる
dotnet run
```

**ランダムペルソナで多様性テスト**:
```bash
# .envでRANDOM_SEEDをコメントアウト
# RANDOM_SEED=12345

# 毎回異なるペルソナが選ばれる
dotnet run
```

### 注意事項

- このバージョンは小規模なアンケート調査に最適化されています（推奨: 5-20人程度）
- 各ペルソナに順次リクエストを送信するため、大量のペルソナでは時間がかかります
- API レート制限を考慮して、リクエスト間に500msの遅延が入ります

### 使用方法

1. **APIキーを設定**（上記の方法A〜Cのいずれかで設定）

2. **アンケートリクエストを作成**:

```csharp
// 自由記述式
var request = new FreeTextSurveyRequest("昨今のAI(LLM)の目覚ましい進化についてどう思いますか？", 300);

// Yes/No形式
var request = new YesNoSurveyRequest("現在の日本において金融緩和政策は必要だと思いますか？");

// 選択式
var request = new OptionSelectSurveyRequest(
    "あなたが食べて見たいのはどちらですか？",
    new List<string> { "正統派芋煮", "庄内風芋煮" },
    isMultiSelect: false
);
```

3. **アンケートを実行**:

```bash
cd NvidiaPersonasJapanDataVirtualSurvey.Console
dotnet run
```

プログラムは自動的に設定された値を読み込み、アンケートを実行します。

## 使用例
完全なコードは[NvidiaPersonasJapanDataVirtualSurvey.Console/Program.cs](https://github.com/07JP27/NvidiaPersonasJapanDataVirtualSurvey/blob/main/NvidiaPersonasJapanDataVirtualSurvey.Console/Program.cs)を参照してください。
```csharp
// 5人のペルソナに対してアンケートを実施
var service = await SurveyService.CreateAsync(apiKey, "gemini-2.5-flash", 5);

var request = new FreeTextSurveyRequest("昨今のAI(LLM)の目覚ましい進化についてどう思いますか？", 300);
var result = await service.RunSurveyAsync(request);

// 結果の表示
foreach (var answer in result.Answers)
{
    Console.WriteLine($"{answer.Persona.Age}歳 / {answer.Persona.Sex} / {answer.Persona.Occupation}");
    Console.WriteLine($"回答: {answer.Answer}");
}
```

## ライセンス

This project uses nvidia/Nemotron-Personas-Japan, licensed under CC BY 4.0.
https://creativecommons.org/licenses/by/4.0/

```
@software{nvidia/Nemotron-Personas-Japan,
  author = {Fujita, Atsunori and Gong, Vincent and Ogushi, Masaya and Yamamoto, Kotaro and Suhara, Yoshi and Corneil, Dane and Meyer, Yev},
  title = {{Nemotron-Personas-Japan}: Synthetic Personas Aligned to Real-World Distributions},
  month = {September},
  year = {2025},
  url = {https://huggingface.co/datasets/nvidia/Nemotron-Personas-Japan}
}
```

## 変更点（Azure OpenAI版からの差異）

- **API の変更**: Azure OpenAI Batch API → Google Gemini API
- **実行方式の変更**: バッチ処理 → 逐次実行（小規模調査向け）
- **依存関係の変更**: `Azure.AI.OpenAI` → `Google.GenerativeAI`
- **レート制限対策**: リクエスト間に自動遅延を挿入
- **トークン計算**: 実測値 → 推定値（Gemini APIの制限により）

## 参考リンク

- [NVIDIA Nemotron-Personas-Japan Dataset](https://huggingface.co/datasets/nvidia/Nemotron-Personas-Japan)
- [Google Gemini API](https://ai.google.dev/)
- [Google AI Studio](https://aistudio.google.com/)
- [Google.GenerativeAI NuGet Package](https://www.nuget.org/packages/Google.GenerativeAI/)
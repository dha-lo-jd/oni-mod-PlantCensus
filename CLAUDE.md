# CLAUDE.md

ONI mod（Census シリーズ。兄弟は `../CritterCensus`）。Build: `dotnet build` (Debug copies to `ModInstallPath` from `Local.Build.props`; Release creates `release/PlantCensus`). 共通事項は HQ（親フォルダ）の `CLAUDE.md`。仕様は `SPEC.md`、機能の概要は `README.md`。

- 構成は `CritterCensus` を写している。`Ui/`（`UiFactory`、`UiStyle`、`ModalScreens`）と `Selection/` は同じ内容で、名前空間だけが違う。直すときは兄弟側にも同じ問題がないか見る。
- `Ui/CensusScreen.cs` の列は `ColumnX` と `*Column` 定数で決まる。列を足すときは、`ColumnNames`、`SortKey` / `TextKey`、`AddRow` を合わせる。
- 植物の一覧は `Components.Uprootables` から取り、`GameTags.Plant` で絞る。成長は `IManageGrowingStates`（作物は `Growing` コンポーネント、木の枝はステートマシン）から読む。
- 木は幹を 1 行にし、枝（`PlantBranch.Instance`、幹は `PlantBranchGrower.Instance`）を `PlantRow.Children` に入れる。幹の行の値は `PlantCollector.Summarize` が枝から補う。画面側の件数・絞り込み・並べ替えは幹の行が単位。
- ツールボタンのアイコンは `Ui/ToolIcon.cs`。`ModAssets/tool_icon.png` を読み、`Assets.Sprites` に自分の名前で登録する。ファイルがなければバニラのアイコン名を返す。`CritterCensus` にも同じものがある。元画像は HQ の `_Incoming/PlantCensus/2026-11-13-icon-tool-3/`、加工は `tools/asset.py`。
- 「問題」列は `WiltCondition.WiltCausesString()` の各行。ゲームが植物の状態欄に出す文言そのまま（行頭の記号とリンクのマークアップは取り除く）。
- ゲームの文字列クラス `STRINGS` は、この Mod の `STRINGS` に隠れる。ゲーム側を使うときは `global::STRINGS`。
- `CritterCensus` と同じ対象（`GameInputMapping.CompareActionKeyCodes`、`GameUtil.GetHotkeyString`、`SelectTool.OnLeftClickDown`）にパッチを当てている。両方を入れても動くように、どちらも自分の条件に合わないときは元の処理へ通す。両方を有効にしたハーネス実行（両方のタグ、17 件）は 2026-11-13 に合格済み。ただしツールのドラッグやクリックの実操作は見ていない。

## 実機検証
ONI 実機で検証するには Gravitas.TestHarness を使う。テストは `[HarnessTest]` を付けた static メソッドとして書く（`PlantCensus/HarnessTests.cs`、`#if DEBUG`）。
実行: `python C:/Users/dha_1/ONI-Modding/GravitasTestHarness/Gravitas.TestProbe/probe.py run --project . --tests '{"tags":["plantcensus"]}'`
ゲームを起動し本番の mods.json を書き換えるので、実行前にユーザーに確認する。
初回登録（`--setup`）は 2026-11-13 に済んでいる。別の環境に初めて置くときは、DLL 配置 → `probe.py run --project . --setup` → 通常の probe、の順にする。
詳細は harness repo の `README.md` と `protocol.md`。

テストが見ていないもの: 画面の見た目、ツールボタンからのドラッグ操作、部屋オーバーレイでの温室クリック、行クリックでの選択とカメラ移動、開閉ボタンのクリック、突然変異種の表示、専用アイコンの表示。

見た目を確かめるときは、デモ用の場面を作って撮る（`docs/census-demo.png` はこの手順で撮ったもの）:

```
python C:/Users/dha_1/ONI-Modding/GravitasTestHarness/Gravitas.TestProbe/probe.py run --project . --steps '[["POST","/invoke",{"method":"PlantCensus.HarnessTests.DemoBuild"}],["POST","/time/advance",{"seconds":8}],["POST","/invoke",{"method":"PlantCensus.HarnessTests.DemoOpen"}],["POST","/screenshot",{"name":"census"}]]'
```

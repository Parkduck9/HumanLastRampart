# UI 부품(UI/Kit) 사용법

> Claude · 2026-10-19 · K-047. 기준: `Docs/GDD/UI_NoSlop.md`(검사 30항목). 새 화면(강화·상점 K-058 등)은 이 부품만 쓴다.
> 코드: `Game/Assets/_Project/UI/Kit/` — `namespace HumanLastRampart.UI`. asmdef에 `HLR.UI` 참조 필요 (Meta·Story·Frontend·Combat은 이미 있음).

## 1. 값 (UiTokens) — 숫자를 새로 만들지 않는다
| 무엇 | 쓸 것 |
|---|---|
| 색 | `Paper` `PaperShade` `Ink` `InkSoft`(종이 위 글) · `Wood` `WoodLight` `WoodDark` · `Body` `BodyDim`(나무·어둠 위 글) · `Brass` `BrassLight` `BrassDark` · `Danger` `DangerLight` · `Good` · `Fire` `Blast` · `Enemy` · `Night`(어둠막) · 투명도는 `UiTokens.Alpha(색, a)` |
| 간격 | `S4 S8 S16 S24 S32 S48` (판과 화면 끝 = S32, 판 안 여백 = S24~S48, 같은 무리 = S8) |
| 글자 크기 | `H1`48 · `H2`32 · `Text`24 · `Small`20 — **이 넷만** |
| 점 | `Dot` = 3px. 테두리·선 두께는 Dot의 배수 |
| 움직임 | `Open` 0.18초 · `Close` 0.12초 |

## 2. 부품 (Ui)
| 부품 | 부르는 법 | 메모 |
|---|---|---|
| 판 | `Ui.Panel(parent, "이름", UiPanel.Parchment / Wood / Band / Plate / Slip / SlipDanger / Bubble)` | 장부·보고서·확인 = 양피지, 명령판·작은 창 = 나무, 화면 위 띠 = Band, 간판·이름표 = Plate, 대사 = Bubble |
| 글 | `Ui.Label(parent, "이름", 글, UiTokens.Text, UiTokens.Ink, TextAnchor.MiddleLeft, display: false)` | `display: true` = 제목·이름·큰 숫자(세계 글꼴). 장면 위에 바로 놓는 글은 `Ui.Outlined(label)` |
| 단추 | `Ui.Button(parent, "열쇠", "보이는 글", 할일, UiButtonKind.Normal / Primary / Quiet / Tab / Icon, 아이콘)` | 이름 = `btn:열쇠` (시험이 찾는다). **Primary(황동)는 화면에 하나.** 고른 탭은 `Ui.SetOn(b, true)`, 글 바꾸기 `Ui.SetLabel`, 아이콘 `Ui.SetIcon` |
| 아이콘 단추 | `Ui.IconButton(parent, "닫기", UiArt.IconClose, Close, 48f)` | 닫기는 판 오른쪽 위 이 단추로 (긴 "닫기" 단추 금지) |
| 아이콘 | `Ui.Icon(parent, "이름", UiArt.IconCheck, 1f, 색)` · Codex HudArt `Ui.HudIcon(parent, "이름", "Icons/stat_gold", 24f)` | 크기는 자동으로 정수 배율. HudIcon은 그림이 없으면 null → 낱말로 대신 |
| 키 표시 | `Ui.KeyHint(parent, "E", "들어가기", overScene: true)` | 화면에 키는 3개까지 |
| 막대 | `UiBar bar = Ui.Bar(parent, "Hp", 200f, 15f); bar.Set(0.6f);` | 줄어든 부분이 잠깐 남았다 따라감, 30% 아래 맥박 |
| 어둠막 | `Ui.Shade(canvas, "이름")` | 창을 열 때 뒤 화면 누르기 (클릭도 막음) |
| 확인 창 | `Ui.Confirm(canvas, 제목, 한 줄, "예 열쇠", "예 글", 예, "아니오 열쇠", "아니오 글", 아니오)` | 예 = 황동 |
| 배치 | `Ui.Place(rect, 기준점, 자리, 크기, pivot)` · `Ui.Stretch(rect, 안쪽)` · `Ui.Row(…)` · `Ui.Column(…)` · `Ui.Size(c, 폭, 높이, 늘림)` | 레이아웃 안에서는 Size로 |
| 나타남·사라짐 | `UiAppear.Show(go, 밀어올 거리)` · `UiAppear.Hide(go, 지울까, 끝나면)` · 매 프레임 `UiAppear.Toggle(go, 보일까)` · 닫기 `Ui.Close(go)` | `SetActive`로 툭 켜고 끄지 않는다 |
| 위급 빛 | `UiPulse.Play(그림, 초)` | 정말 위험할 때만 |

## 3. 예시 — 상점 창 하나
```csharp
using HumanLastRampart.UI;

Image dim = Ui.Shade(canvas, "Shop");
Image sheet = Ui.Panel(dim.transform, "Ledger", UiPanel.Parchment);
Ui.Place(sheet.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 0f));
var column = sheet.gameObject.AddComponent<VerticalLayoutGroup>();
column.padding = new RectOffset(48, 48, 32, 48);
column.spacing = UiTokens.S24;
column.childControlWidth = column.childControlHeight = true;
column.childForceExpandHeight = false;
sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
UiAppear.Show(sheet.gameObject, new Vector2(0f, -UiTokens.S24));

// 머리 줄: 이름 · 골드(1순위) · 닫기
HorizontalLayoutGroup head = Ui.Row(sheet.transform, "Head", UiTokens.S16);
Ui.Size(head, -1f, 48f);
Ui.Size(Ui.Label(head.transform, "Title", "대장간", UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true), 0f, 48f, true);
Ui.HudIcon(head.transform, "Gold Icon", "Icons/stat_gold", 24f);
Ui.Size(Ui.Label(head.transform, "Gold", gold.ToString("N0"), UiTokens.H2, UiTokens.Ink, TextAnchor.MiddleLeft, true), 136f, 48f);
Ui.IconButton(head.transform, "닫기", UiArt.IconClose, () => Ui.Close(dim.gameObject));

// 물건 고르기 = 탭/칩 (같은 긴 단추를 세로로 쌓지 않는다), 사기 = 황동 하나
Button sword = Ui.Button(sheet.transform, "물건:sword", "장교 군도", () => Pick("sword"), UiButtonKind.Tab);
Ui.SetOn(sword, picked == "sword");
Button buy = Ui.Button(sheet.transform, "확정 구매", $"{price:N0}골드에 사기", Buy, UiButtonKind.Primary);
buy.interactable = canBuy;   // 못 사면 이유를 값 줄에 붉게 (Ink 대신 UiTokens.Danger)
```

## 4. 금지 (UI_NoSlop 요약)
- 새 색·새 글자 크기·새 간격 숫자, `new Color(…)`로 판 칠하기, `Outline`·1px 선으로 테두리 그리기
- 같은 크기 긴 단추 5개 이상 세로로 쌓기 (→ 칩·탭·2열 칸)
- 규칙·계산식 설명 문장, "~하세요" 안내, 괄호 덧말 "(기본)", 구분점 · 세 개 이상
- ▶▲◆× 같은 글자 기호를 아이콘으로 (→ `UiArt.Icon*`)
- "없음"·"0개" 빈 줄 (→ 칸을 감춘다)
- 시험 단추·내부 ID·"임시" 글을 기본 화면에 (→ F12 개발 표시)
- 결과를 글 한 줄로만 (→ 값이 바뀌는 자리·✓ 표시·도장)
- `SetActive`로 판 즉시 켜고 끄기, 엔진 기본 밝기 단추(ColorBlock)
- UiArt 그림 이름·9칸 경계 바꾸기 (Codex K-059가 이 이름·경계로 그린다)

## 5. 그림 이름 (UiArt)
판 `panel_parchment` `panel_wood` `panel_band` `plate` `slip` `slip_danger` `bubble` `bubble_tail` · 단추 `button_wood(_down)` `button_brass(_down)` `frame_glow` `keycap` `bar_track` · 기타 `seal` `vignette` · 아이콘 `icon_close` `icon_up` `icon_down` `icon_play` `icon_pause` `icon_speed1/2/4` `icon_hero` `icon_north` `icon_check` `icon_flag` `icon_auto` `icon_idle` `icon_special` `icon_minus` `icon_plus` `icon_right` `icon_warn`.
크기·경계는 `Docs/GDD/UI_NoSlop_에셋요청.md`. 새 아이콘이 필요하면 지어내지 말고 그 문서에 줄을 더해 Codex에 요청한다 (그동안은 `UiArt.cs`의 `IconShapes`에 코드 도트로 임시).

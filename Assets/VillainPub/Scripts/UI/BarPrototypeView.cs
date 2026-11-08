using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VillainPub.Bar;
using VillainPub.Data;

namespace VillainPub.UI
{
    /// <summary>
    /// 영업 파트 프로토타입 화면. 사각형과 텍스트 UI를 코드로 만들기 때문에 씬에는 이 컴포넌트 하나만 두면 된다.
    /// 규칙은 BarSession이 갖고, 이 클래스는 입력 전달과 표시만 한다.
    /// </summary>
    public sealed class BarPrototypeView : MonoBehaviour
    {
        private static readonly Color SeedOnColor = new Color(0.6f, 0.15f, 0.25f);
        private static readonly Color EncounterTextColor = new Color(1f, 0.75f, 0.55f);

        [Header("데이터")]
        [SerializeField] private DrinkTable _drinks;
        [SerializeField] private RecipeTable _recipes;
        [SerializeField] private IngredientTable _ingredients;
        [SerializeField] private StringTable _strings;
        [SerializeField] private Language _language = Language.Kr;

        [Header("영업 설정")]
        [SerializeField, Min(5f)] private float _dayDurationSeconds = 90f;
        [Tooltip("0이면 실행할 때마다 다른 주문 순서")]
        [SerializeField] private int _randomSeed;

        [Header("표시")]
        [Tooltip("한글 글리프가 있는 TMP 폰트 에셋. 비우면 TMP 기본 폰트(한글 없음)를 쓴다.")]
        [SerializeField] private TMP_FontAsset _font;

        [Header("전투 (비우면 영업만 반복)")]
        [Tooltip("빌런을 만든 날 영업이 끝나면 이 화면으로 넘어간다. 오늘의 적 구성도 여기서 가져와 영업 중에 보여준다.")]
        [SerializeField] private BattlePrototypeView _battle;

        private PrototypeUi _ui;
        private GameObject _canvasObject;
        private TextMeshProUGUI _encounterText;
        private BarSession _session;
        private RecipeMatcher _matcher;
        private int _shownSecond = -1;

        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _orderText;
        private TextMeshProUGUI _glassText;
        private TextMeshProUGUI _feedbackText;
        private TextMeshProUGUI _recipeBookText;
        private TextMeshProUGUI _resultText;
        private Image _seedButtonImage;
        private TextMeshProUGUI _seedButtonLabel;
        private GameObject _recipeBookPanel;
        private GameObject _resultPanel;

        private void Start()
        {
            if (_drinks == null || _recipes == null || _ingredients == null || _strings == null)
            {
                Debug.LogError($"[{name}] 데이터 테이블(Drink, Recipe, Ingredient, String)을 모두 연결해 주세요.", this);
                enabled = false;
                return;
            }
            if (_font == null)
                Debug.LogWarning($"[{name}] 한글 폰트가 연결되지 않아 한글이 네모로 보일 수 있습니다.", this);

            _ui = new PrototypeUi(_font);
            BuildUi();
            StartDay();
        }

        private void Update()
        {
            if (_session == null) return;

            _session.Tick(Time.deltaTime);

            int second = Mathf.CeilToInt(_session.RemainingSeconds);
            if (second != _shownSecond)
            {
                _shownSecond = second;
                RefreshStatus();
            }

            if (_session.IsOver && !_resultPanel.activeSelf)
                ShowResult();
        }

        private void StartDay()
        {
            _matcher = new RecipeMatcher(_drinks, _recipes);
            foreach (string problem in _matcher.Problems)
                Debug.LogWarning($"[{name}] {problem}", this);

            int seed = _randomSeed != 0 ? _randomSeed : Environment.TickCount;
            _session = new BarSession(_matcher, _dayDurationSeconds, seed);

            _recipeBookText.text = BuildRecipeBook();
            // 적 구성은 영업 전에 공개한다: "이길 만큼만 빌런, 나머지는 돈" 계산을 하게 하려는 것.
            _encounterText.text = HasBattle ? _battle.DescribeEncounter() : string.Empty;
            _feedbackText.text = "영업 시작!";
            _resultPanel.SetActive(false);
            _recipeBookPanel.SetActive(false);
            _shownSecond = -1;
            RefreshAll();
        }

        #region 입력

        private void OnIngredient(string ingredientId)
        {
            _session.AddIngredient(ingredientId);
            RefreshGlass();
        }

        private void OnToggleSeed()
        {
            _session.SetSeed(!_session.HasSeed);
            RefreshGlass();
        }

        private void OnClearGlass()
        {
            _session.ClearGlass();
            RefreshGlass();
        }

        private void OnServe()
        {
            ServeResult result = _session.Serve();
            if (result.Outcome == ServeOutcome.None)
            {
                _feedbackText.text = "잔이 비어 있습니다.";
                return;
            }

            _feedbackText.text = DescribeServe(result);
            RefreshAll();
        }

        #endregion

        #region 표시 갱신

        private void RefreshAll()
        {
            RefreshStatus();
            RefreshOrder();
            RefreshGlass();
        }

        private void RefreshStatus()
        {
            _statusText.text =
                $"남은 시간 {Mathf.CeilToInt(_session.RemainingSeconds)}초    " +
                $"수익 {_session.Money}    빌런 {_session.Villains.Count}명    손님 {_session.CustomerCount}번째";
        }

        private void RefreshOrder()
        {
            _orderText.text = _session.CurrentOrder != null
                ? $"주문: {Text(_session.CurrentOrder.NameStrKey)}"
                : "영업 종료";
        }

        private void RefreshGlass()
        {
            string contents = _session.Glass.Count == 0 ? "(비어 있음)" : DescribeIngredients(CountInOrder(_session.Glass));
            _glassText.text = $"잔: {contents}" + (_session.HasSeed ? "  + <color=#FF6680>빌런 시드</color>" : string.Empty);

            _seedButtonImage.color = _session.HasSeed ? SeedOnColor : PrototypeUi.ButtonColor;
            _seedButtonLabel.text = _session.HasSeed ? "시드 빼기" : "시드 넣기";
        }

        private void ShowResult()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=150%>영업 종료</size>").AppendLine();
            sb.AppendLine($"손님 {_session.SoldCount + _session.LostCount + _session.Villains.Count}명 응대");
            sb.AppendLine($"판매 {_session.SoldCount}    놓친 손님 {_session.LostCount}");
            sb.AppendLine($"수익 {_session.Money}").AppendLine();
            sb.AppendLine($"오늘 만든 빌런 {_session.Villains.Count}명");
            foreach (BarVillain villain in _session.Villains)
            {
                sb.AppendLine(villain.IsDefault
                    ? " - 기본 빌런(잡몹)"
                    : $" - {villain.VillainKey}  ({DrinkName(villain.FromDrinkId)})");
            }
            string next;
            if (_session.Villains.Count == 0) next = "→ 전투 없이 다음 날";
            else if (HasBattle) next = "→ 전투로";
            else next = "→ 전투 (전투 화면이 연결되지 않아 건너뜀)";
            sb.AppendLine().Append(next);

            _resultText.text = sb.ToString();
            _resultPanel.SetActive(true);
        }

        private bool HasBattle => _battle != null && _battle.IsReady;

        // 빌런을 하나라도 만들었으면 전투는 필수다.
        private void OnResultContinue()
        {
            if (_session.Villains.Count == 0 || !HasBattle)
            {
                StartDay();
                return;
            }

            _canvasObject.SetActive(false);
            _battle.Begin(_session.Villains, () =>
            {
                _canvasObject.SetActive(true);
                StartDay();
            });
        }

        private string DescribeServe(ServeResult result)
        {
            string order = result.Order != null ? Text(result.Order.NameStrKey) : "?";
            switch (result.Outcome)
            {
                case ServeOutcome.Sold:
                    return $"{order} 판매! +{result.MoneyEarned}";
                case ServeOutcome.WrongDrink:
                    return $"주문은 {order}인데 {Text(result.Made.NameStrKey)}를 냈습니다. 손님이 떠났습니다.";
                case ServeOutcome.Failed:
                    return "레시피에 없는 음료입니다. 손님이 떠났습니다.";
                case ServeOutcome.Villain:
                    return $"{Text(result.Made.NameStrKey)}에 시드를 넣었습니다. 손님이 빌런({result.Made.VillainKey})이 되었습니다." +
                           (result.MoneyEarned > 0 ? $" +{result.MoneyEarned}" : " (주문과 달라 돈은 못 받음)");
                case ServeOutcome.DefaultVillain:
                    return "엉터리 음료에 시드를 넣었습니다. 손님이 잡몹 빌런이 되었습니다.";
                default:
                    return string.Empty;
            }
        }

        private string BuildRecipeBook()
        {
            var sb = new StringBuilder("<size=130%>레시피 북</size>\n\n");
            foreach (DrinkData drink in _matcher.Drinks.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                IEnumerable<RecipeEntry> entries = _recipes.TryGet(drink.RecipeKey, out RecipeData recipe)
                    ? recipe.Ingredients
                    : Enumerable.Empty<RecipeEntry>();
                sb.AppendLine($"{Text(drink.NameStrKey)}  ({drink.Price}):  {DescribeIngredients(entries)}");
            }
            return sb.ToString();
        }

        #endregion

        #region 문자열 헬퍼

        private string Text(string key) => _strings.GetText(key, _language);

        private string DrinkName(string drinkId) =>
            _drinks.TryGet(drinkId, out DrinkData drink) ? Text(drink.NameStrKey) : drinkId;

        private string IngredientName(string ingredientId) =>
            _ingredients.TryGet(ingredientId, out IngredientData ingredient) ? Text(ingredient.NameStrKey) : ingredientId;

        private string DescribeIngredients(IEnumerable<RecipeEntry> entries) =>
            string.Join(", ", entries.Select(e => $"{IngredientName(e.IngredientId)} x{e.Count}"));

        // 잔 표시는 넣은 순서를 유지해야 플레이어가 덜 헷갈린다.
        private static List<RecipeEntry> CountInOrder(IEnumerable<string> ids)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (!counts.ContainsKey(id)) order.Add(id);
                counts.TryGetValue(id, out int count);
                counts[id] = count + 1;
            }
            return order.Select(id => new RecipeEntry(id, counts[id])).ToList();
        }

        // 프로토타입 전용 표시명. 정식 UI에서는 문자열 테이블로 옮긴다.
        private static string GroupLabel(IngredientGroup group)
        {
            switch (group)
            {
                case IngredientGroup.Spirit: return "베이스";
                case IngredientGroup.Liqueur: return "리큐어";
                case IngredientGroup.Citrus: return "산미";
                case IngredientGroup.Sweetener: return "단맛";
                case IngredientGroup.Mixer: return "믹서";
                default: return group.ToString();
            }
        }

        #endregion

        #region UI 생성

        private void BuildUi()
        {
            PrototypeUi.EnsureEventSystem();
            Canvas canvas = PrototypeUi.CreateCanvas(transform, "BarCanvas");
            _canvasObject = canvas.gameObject;

            RectTransform root = PrototypeUi.CreatePanel(_canvasObject.transform, "Root", PrototypeUi.BackgroundColor);
            PrototypeUi.AddVerticalLayout(root, new RectOffset(48, 48, 40, 40), 24f);

            _statusText = _ui.CreateText(root, 32f);
            _encounterText = _ui.CreateText(root, 28f);
            _encounterText.color = EncounterTextColor;
            _orderText = _ui.CreateText(root, 56f);
            _glassText = _ui.CreateText(root, 36f);

            foreach (IGrouping<IngredientGroup, IngredientData> group in _ingredients.Rows
                         .GroupBy(i => i.Type)
                         .OrderBy(g => g.Key))
            {
                RectTransform row = PrototypeUi.CreateRow(root);
                TextMeshProUGUI label = _ui.CreateText(row, 30f);
                label.text = GroupLabel(group.Key);
                label.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;

                foreach (IngredientData ingredient in group)
                {
                    string id = ingredient.Id;
                    _ui.CreateButton(row, Text(ingredient.NameStrKey), () => OnIngredient(id), PrototypeUi.ButtonColor);
                }
            }

            RectTransform actions = PrototypeUi.CreateRow(root);
            Button seedButton = _ui.CreateButton(actions, "시드 넣기", OnToggleSeed, PrototypeUi.ButtonColor);
            _seedButtonImage = seedButton.GetComponent<Image>();
            _seedButtonLabel = seedButton.GetComponentInChildren<TextMeshProUGUI>();
            _ui.CreateButton(actions, "비우기", OnClearGlass, PrototypeUi.ButtonColor);
            _ui.CreateButton(actions, "제공", OnServe, PrototypeUi.AcceptColor);
            _ui.CreateButton(actions, "레시피 북", () => _recipeBookPanel.SetActive(true), PrototypeUi.ButtonColor);

            _feedbackText = _ui.CreateText(root, 34f);

            _recipeBookPanel = _ui.CreateOverlay(_canvasObject.transform, "RecipeBook", out _recipeBookText,
                "닫기", () => _recipeBookPanel.SetActive(false));
            _resultPanel = _ui.CreateOverlay(_canvasObject.transform, "Result", out _resultText,
                "계속", OnResultContinue);
        }

        #endregion
    }
}

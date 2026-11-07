using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color BackgroundColor = new Color(0.12f, 0.1f, 0.14f);
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.3f, 0.36f);
        private static readonly Color ServeButtonColor = new Color(0.2f, 0.5f, 0.3f);
        private static readonly Color SeedOnColor = new Color(0.6f, 0.15f, 0.25f);

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

            _seedButtonImage.color = _session.HasSeed ? SeedOnColor : ButtonColor;
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
            sb.AppendLine().Append(_session.Villains.Count > 0 ? "→ 전투로 (프로토타입에서는 미구현)" : "→ 전투 없이 정산");

            _resultText.text = sb.ToString();
            _resultPanel.SetActive(true);
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
            EnsureEventSystem();

            var canvasObject = new GameObject("BarCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform root = CreatePanel(canvasObject.transform, "Root", BackgroundColor);
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(48, 48, 40, 40);
            rootLayout.spacing = 24f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            _statusText = CreateText(root, 32f);
            _orderText = CreateText(root, 56f);
            _glassText = CreateText(root, 36f);

            foreach (IGrouping<IngredientGroup, IngredientData> group in _ingredients.Rows
                         .GroupBy(i => i.Type)
                         .OrderBy(g => g.Key))
            {
                RectTransform row = CreateRow(root);
                TextMeshProUGUI label = CreateText(row, 30f);
                label.text = GroupLabel(group.Key);
                label.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;

                foreach (IngredientData ingredient in group)
                {
                    string id = ingredient.Id;
                    CreateButton(row, Text(ingredient.NameStrKey), () => OnIngredient(id), ButtonColor);
                }
            }

            RectTransform actions = CreateRow(root);
            Button seedButton = CreateButton(actions, "시드 넣기", OnToggleSeed, ButtonColor);
            _seedButtonImage = seedButton.GetComponent<Image>();
            _seedButtonLabel = seedButton.GetComponentInChildren<TextMeshProUGUI>();
            CreateButton(actions, "비우기", OnClearGlass, ButtonColor);
            CreateButton(actions, "제공", OnServe, ServeButtonColor);
            CreateButton(actions, "레시피 북", () => _recipeBookPanel.SetActive(true), ButtonColor);

            _feedbackText = CreateText(root, 34f);

            _recipeBookPanel = CreateOverlay(canvasObject.transform, "RecipeBook", out _recipeBookText,
                "닫기", () => _recipeBookPanel.SetActive(false));
            _resultPanel = CreateOverlay(canvasObject.transform, "Result", out _resultText,
                "다시 시작", StartDay);
        }

        // 씬에 EventSystem이 없으면 새 Input System용으로 만든다(없으면 버튼이 클릭되지 않는다).
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static RectTransform CreatePanel(Transform parent, string objectName, Color color)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static RectTransform CreateRow(Transform parent)
        {
            var rect = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private TextMeshProUGUI CreateText(Transform parent, float fontSize)
        {
            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Action onClick, Color color)
        {
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 200f;
            layout.preferredHeight = 72f;

            TextMeshProUGUI text = CreateText(buttonObject.transform, 30f);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private GameObject CreateOverlay(Transform parent, string objectName, out TextMeshProUGUI body,
            string buttonLabel, Action onButton)
        {
            RectTransform panel = CreatePanel(parent, objectName, OverlayColor);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(120, 120, 80, 80);
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            body = CreateText(panel, 34f);
            body.alignment = TextAlignmentOptions.Center;
            CreateButton(panel, buttonLabel, onButton, ButtonColor);

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        #endregion
    }
}

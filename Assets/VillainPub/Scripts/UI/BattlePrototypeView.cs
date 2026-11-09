using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VillainPub.Bar;
using VillainPub.Battle;
using VillainPub.Battle.Simulation;
using VillainPub.Data;

namespace VillainPub.UI
{
    /// <summary>
    /// 전투 파트 프로토타입 화면: 배치 → 전투 재생 → 결과.
    /// 영업 화면(<see cref="BarPrototypeView"/>)이 <see cref="Begin"/>으로 불러 쓴다. 혼자서는 시작하지 않는다.
    /// 전투 계산은 <see cref="BattleSimulator"/>가 끝까지 미리 하고, 이 클래스는 이벤트 목록을 재생만 한다.
    /// </summary>
    public sealed class BattlePrototypeView : MonoBehaviour
    {
        private static readonly Color VillainColor = new Color(0.45f, 0.18f, 0.35f);
        private static readonly Color HeroColor = new Color(0.18f, 0.3f, 0.55f);
        private static readonly Color BuildingColor = new Color(0.35f, 0.33f, 0.3f);
        private static readonly Color SelectedColor = new Color(0.85f, 0.65f, 0.2f);
        private static readonly Color HitColor = new Color(0.85f, 0.2f, 0.2f);
        private const float CardWidth = 192f;
        private const float CardHeight = 216f;

        [Header("데이터")]
        [SerializeField] private VillainTable _villains;
        [SerializeField] private HeroTable _heroes;
        [SerializeField] private BuildingTable _buildings;
        [SerializeField] private EncounterTable _encounters;
        [SerializeField] private StringTable _strings;
        [SerializeField] private Language _language = Language.Kr;

        [Header("전투 설정")]
        [Tooltip("차례로 도전할 적 구성 ID (Encounter 시트). 이기면 다음으로 넘어가고, 마지막까지 이기면 캠페인 클리어. 영업 전에 공개된다.")]
        [SerializeField] private string[] _encounterKeys = { "Encounter_A1", "Encounter_A2", "Encounter_A3" };
        [SerializeField, Min(0.05f)] private float _stepSeconds = 0.4f;

        [Header("표시")]
        [SerializeField] private TMP_FontAsset _font;

        private enum Phase { Hidden, Placement, Replay, Result }

        private PrototypeUi _ui;
        private BattleUnitFactory _factory;
        private readonly List<EncounterData> _stages = new List<EncounterData>();
        private int _stageIndex;
        private EncounterData _encounter;
        private List<BattleUnitSpec> _enemySpecs;

        private Phase _phase = Phase.Hidden;
        private readonly List<BattleUnitSpec> _order = new List<BattleUnitSpec>();
        private int _selectedSlot = -1;
        private Action<BattleSummary> _onFinished;
        private BattleSummary _summary;

        // 재생 상태. uid는 BattleResult.Units의 index.
        private BattleResult _result;
        private readonly List<int> _villainLine = new List<int>();
        private readonly List<int> _enemyLine = new List<int>();
        private readonly Dictionary<int, int> _hp = new Dictionary<int, int>();
        private readonly HashSet<int> _hit = new HashSet<int>();
        private bool _skip;

        private GameObject _canvasObject;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _infoText;
        private RectTransform _lineRow;
        private Button _primaryButton;
        private TextMeshProUGUI _primaryLabel;
        private Button _skipButton;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            if (_villains == null || _heroes == null || _buildings == null || _encounters == null || _strings == null)
            {
                Debug.LogError($"[{name}] 데이터 테이블(Villain, Hero, Building, Encounter, String)을 모두 연결해 주세요.", this);
                return;
            }
            foreach (string key in _encounterKeys ?? Array.Empty<string>())
            {
                if (_encounters.TryGet(key, out EncounterData encounter)) _stages.Add(encounter);
                else Debug.LogError($"[{name}] 적 구성 '{key}'가 Encounter 테이블에 없습니다.", this);
            }
            if (_stages.Count == 0)
            {
                Debug.LogError($"[{name}] 도전할 적 구성이 없습니다. _encounterKeys를 확인하세요.", this);
                return;
            }

            _factory = new BattleUnitFactory(_villains, _heroes, _buildings);
            _ui = new PrototypeUi(_font);
            BuildUi();
            _canvasObject.SetActive(false);
            IsReady = true;
            ResetCampaign();
        }

        public int StageIndex => _stageIndex;
        public int StageCount => _stages.Count;
        public bool IsCampaignCleared => _stageIndex >= _stages.Count;

        /// <summary>처음 적 구성부터 다시 시작한다.</summary>
        public void ResetCampaign()
        {
            _stageIndex = 0;
            LoadStage();
        }

        private void LoadStage()
        {
            if (IsCampaignCleared) return;

            _encounter = _stages[_stageIndex];
            var problems = new List<string>();
            _enemySpecs = _factory.CreateEnemies(_encounter, problems);
            foreach (string problem in problems) Debug.LogWarning($"[{name}] {problem}", this);
        }

        /// <summary>영업 화면에 미리 보여줄 오늘의 적 구성(맨 앞부터).</summary>
        public string DescribeEncounter()
        {
            if (!IsReady) return "적 구성 없음";
            if (IsCampaignCleared) return "모든 적을 물리쳤습니다";

            string units = string.Join("  ·  ", _enemySpecs.Select(DescribeShort));
            return $"오늘의 적 [{Text(_encounter.NameStrKey)}  {_stageIndex + 1}/{_stages.Count}]  {units}";
        }

        /// <summary>영업에서 만든 빌런으로 전투를 시작한다. 끝나면(결과 화면에서 버튼) onFinished에 결과를 넘긴다.</summary>
        public void Begin(IReadOnlyList<BarVillain> villains, Action<BattleSummary> onFinished)
        {
            if (!IsReady || IsCampaignCleared) return;

            var problems = new List<string>();
            _order.Clear();
            _order.AddRange(_factory.CreateVillains(villains, problems));
            foreach (string problem in problems) Debug.LogWarning($"[{name}] {problem}", this);

            _onFinished = onFinished;
            _selectedSlot = -1;
            _canvasObject.SetActive(true);
            EnterPlacement();
        }

        #region 배치

        private void EnterPlacement()
        {
            _phase = Phase.Placement;
            _titleText.text = $"배치  —  {Text(_encounter.NameStrKey)}";
            _infoText.text = "빌런 두 명을 차례로 누르면 자리를 바꿉니다. 오른쪽 끝이 맨 앞(먼저 싸움)입니다.";
            _primaryLabel.text = "전투 시작";
            _primaryButton.interactable = _order.Count > 0;
            _skipButton.gameObject.SetActive(false);
            RenderPlacement();
        }

        private void OnVillainSlot(int slot)
        {
            if (_phase != Phase.Placement) return;

            if (_selectedSlot < 0)
            {
                _selectedSlot = slot;
            }
            else
            {
                (_order[_selectedSlot], _order[slot]) = (_order[slot], _order[_selectedSlot]);
                _selectedSlot = -1;
            }
            RenderPlacement();
        }

        private void RenderPlacement()
        {
            ClearLine();
            // 빌런 줄은 맨 앞이 가운데(VS 쪽)에 오도록 뒤에서부터 그린다.
            for (int slot = _order.Count - 1; slot >= 0; slot--)
            {
                int captured = slot;
                Color color = slot == _selectedSlot ? SelectedColor : VillainColor;
                CreateCard(_order[slot], _order[slot].Hp, color, () => OnVillainSlot(captured));
            }
            CreateVs();
            foreach (BattleUnitSpec enemy in _enemySpecs)
                CreateCard(enemy, enemy.Hp, ColorOf(enemy.Kind), null);
        }

        #endregion

        #region 재생

        private void OnPrimary()
        {
            switch (_phase)
            {
                case Phase.Placement:
                    StartCoroutine(Replay());
                    break;
                case Phase.Result:
                    _phase = Phase.Hidden;
                    _canvasObject.SetActive(false);
                    _onFinished?.Invoke(_summary);
                    break;
            }
        }

        private IEnumerator Replay()
        {
            _phase = Phase.Replay;
            _result = BattleSimulator.Run(new BattleSetup(_order, _enemySpecs));

            _villainLine.Clear();
            _enemyLine.Clear();
            _hp.Clear();
            foreach (BattleUnitInfo unit in _result.Units)
            {
                _hp[unit.Uid] = unit.Spec.Hp;
                (unit.Side == BattleSide.Villain ? _villainLine : _enemyLine).Add(unit.Uid);
            }

            _skip = false;
            _titleText.text = $"전투  —  {Text(_encounter.NameStrKey)}";
            _primaryButton.interactable = false;
            _skipButton.gameObject.SetActive(true);
            RenderReplay();

            foreach (BattleEvent e in _result.Events)
            {
                _hit.Clear();
                switch (e.Type)
                {
                    case BattleEventType.RoundStarted:
                        _infoText.text = $"{e.Value}라운드";
                        break;
                    case BattleEventType.Damaged:
                        _hp[e.TargetUid] = e.TargetHpAfter;
                        _hit.Add(e.TargetUid);
                        break;
                    case BattleEventType.Fainted:
                        _villainLine.Remove(e.TargetUid);
                        _enemyLine.Remove(e.TargetUid);
                        break;
                }

                if (_skip) continue;
                RenderReplay();
                yield return new WaitForSeconds(_stepSeconds);
            }

            _hit.Clear();
            RenderReplay();
            ShowResult();
        }

        private void RenderReplay()
        {
            ClearLine();
            for (int i = _villainLine.Count - 1; i >= 0; i--)
                CreateReplayCard(_villainLine[i]);
            CreateVs();
            foreach (int uid in _enemyLine)
                CreateReplayCard(uid);
        }

        private void CreateReplayCard(int uid)
        {
            BattleUnitSpec spec = _result.Units[uid].Spec;
            Color color = _hit.Contains(uid) ? HitColor : ColorOf(spec.Kind);
            CreateCard(spec, Math.Max(0, _hp[uid]), color, null);
        }

        private void ShowResult()
        {
            _phase = Phase.Result;
            int reward = _factory.SumBuildingReward(_result.DestroyedBuildings);

            var sb = new StringBuilder();
            sb.Append(_result.Outcome == BattleOutcome.Victory
                ? "<color=#7CFF9A>승리!</color> 히어로를 모두 쓰러뜨렸습니다."
                : "<color=#FF7C7C>패배</color> 히어로가 남았습니다.");
            if (_result.Stalemate) sb.Append(" (더 이상 피해를 줄 수 없어 종료)");
            sb.Append($"   {_result.Rounds}라운드");

            if (_result.DestroyedBuildings.Count > 0)
            {
                IEnumerable<string> names = _result.DestroyedBuildings.Select(id =>
                    _buildings.TryGet(id, out BuildingData b) ? Text(b.NameStrKey) : id);
                sb.Append($"\n부순 건물: {string.Join(", ", names)}  →  보상 +{reward}");
            }
            else
            {
                sb.Append("\n부순 건물 없음");
            }

            bool victory = _result.Outcome == BattleOutcome.Victory;
            _summary = new BattleSummary(victory, reward);
            if (victory)
            {
                _stageIndex++;
                LoadStage();
                sb.Append(IsCampaignCleared ? "\n마지막 적까지 물리쳤습니다!" : $"\n다음 적: {Text(_encounter.NameStrKey)}");
            }
            else
            {
                sb.Append("\n내일 같은 적에게 다시 도전합니다.");
            }

            _infoText.text = sb.ToString();
            _primaryLabel.text = "정산으로";
            _primaryButton.interactable = true;
            _skipButton.gameObject.SetActive(false);
        }

        #endregion

        #region 표시 헬퍼

        private string Text(string key) => _strings.GetText(key, _language);

        private string UnitName(BattleUnitSpec spec)
        {
            string key = _factory.NameStrKey(spec);
            return key != null ? Text(key) : spec.Id;
        }

        private string DescribeShort(BattleUnitSpec spec) =>
            spec.Kind == UnitKind.Building
                ? $"{UnitName(spec)}(체력 {spec.Hp})"
                : $"{UnitName(spec)}({spec.Attack}/{spec.Hp})";

        private static Color ColorOf(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Villain: return VillainColor;
                case UnitKind.Hero: return HeroColor;
                default: return BuildingColor;
            }
        }

        private void ClearLine()
        {
            for (int i = _lineRow.childCount - 1; i >= 0; i--)
                Destroy(_lineRow.GetChild(i).gameObject);
        }

        private void CreateVs()
        {
            TextMeshProUGUI vs = _ui.CreateText(_lineRow, 48f);
            vs.text = "VS";
            vs.alignment = TextAlignmentOptions.Center;
            vs.gameObject.AddComponent<LayoutElement>().preferredWidth = 96f;
        }

        private void CreateCard(BattleUnitSpec spec, int hp, Color color, Action onClick)
        {
            string stats = spec.Kind == UnitKind.Building
                ? $"체력 {hp}"
                : $"공격 {spec.Attack}\n체력 {hp}";
            Button card = _ui.CreateButton(_lineRow, $"{UnitName(spec)}\n\n{stats}", onClick ?? (() => { }),
                color, CardWidth, CardHeight, 24f);
            card.interactable = onClick != null;
            // 비활성 버튼의 기본 회색 처리를 끄고 카드 색을 그대로 보여준다.
            ColorBlock colors = card.colors;
            colors.disabledColor = Color.white;
            card.colors = colors;
        }

        #endregion

        #region UI 생성

        private void BuildUi()
        {
            PrototypeUi.EnsureEventSystem();
            Canvas canvas = PrototypeUi.CreateCanvas(transform, "BattleCanvas", sortingOrder: 10);
            _canvasObject = canvas.gameObject;

            RectTransform root = PrototypeUi.CreatePanel(_canvasObject.transform, "Root", PrototypeUi.BackgroundColor);
            VerticalLayoutGroup layout = PrototypeUi.AddVerticalLayout(root, new RectOffset(48, 48, 40, 40), 36f);
            layout.childAlignment = TextAnchor.UpperCenter;

            _titleText = _ui.CreateText(root, 48f);
            _lineRow = PrototypeUi.CreateRow(root, TextAnchor.MiddleCenter);
            _lineRow.gameObject.AddComponent<LayoutElement>().preferredHeight = CardHeight;
            _infoText = _ui.CreateText(root, 36f);

            RectTransform actions = PrototypeUi.CreateRow(root, TextAnchor.MiddleCenter);
            _primaryButton = _ui.CreateButton(actions, "전투 시작", OnPrimary, PrototypeUi.AcceptColor, fontSize: 36f);
            _primaryLabel = _primaryButton.GetComponentInChildren<TextMeshProUGUI>();
            _skipButton = _ui.CreateButton(actions, "건너뛰기", () => _skip = true, PrototypeUi.ButtonColor, fontSize: 36f);
        }

        #endregion
    }

    /// <summary>전투 화면이 영업 화면(정산)에 넘기는 결과.</summary>
    public readonly struct BattleSummary
    {
        public bool Victory { get; }
        /// <summary>부순 건물 보상 합계(져도 받음).</summary>
        public int Reward { get; }

        public BattleSummary(bool victory, int reward)
        {
            Victory = victory;
            Reward = reward;
        }
    }
}

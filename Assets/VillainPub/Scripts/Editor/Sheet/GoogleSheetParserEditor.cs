using UnityEditor;
using UnityEngine;
using VillainPub.Data;

namespace VillainPub.EditorTools
{
    [CustomEditor(typeof(GoogleSheetParserBase), true)]
    internal sealed class GoogleSheetParserEditor : Editor
    {
        private const string SheetUrlFormat = "https://docs.google.com/spreadsheets/d/{0}/edit";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var table = (GoogleSheetParserBase)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox($"{table.RowType.Name} · {table.Count}행", MessageType.None);

            // 동기화는 진행 창을 띄우고 잠시 에디터를 멈추므로 인스펙터 그리기가 끝난 뒤 실행한다.
            if (GUILayout.Button("시트에서 동기화", GUILayout.Height(30)))
                EditorApplication.delayCall += () => SheetSync.Sync(table);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("검증"))
                    SheetSync.Validate(table);

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(table.SheetId)))
                {
                    if (GUILayout.Button("시트 열기"))
                        Application.OpenURL(string.Format(SheetUrlFormat, table.SheetId.Trim()));
                }
            }
        }

        [MenuItem("VillainPub/시트/전체 동기화")]
        private static void SyncAll() => SheetSync.SyncAll();

        [MenuItem("VillainPub/시트/전체 검증")]
        private static void ValidateAll() => SheetSync.ValidateAll();
    }
}

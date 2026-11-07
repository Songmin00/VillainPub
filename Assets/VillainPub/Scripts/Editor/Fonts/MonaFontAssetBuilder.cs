using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace VillainPub.EditorTools
{
    /// <summary>
    /// Mona S 픽셀 폰트로 TMP 폰트 에셋을 만든다. 폰트 에셋은 외부 에셋 폴더(_IgnoredAssets)에 있어 Git에 없으므로,
    /// 클론 후에도 같은 설정으로 다시 만들 수 있게 메뉴로 둔다.
    /// </summary>
    internal static class MonaFontAssetBuilder
    {
        private const string SourceFontPath = "Assets/_IgnoredAssets/Fonts/MonaS_20260406_1909/ttf/01_Main/MonaS8x12.ttf";
        private const string OutputPath = "Assets/_IgnoredAssets/Fonts/MonaS8x12 Pixel.asset";

        // 원본은 1200유닛 = 12px(1px = 100유닛)인 픽셀 폰트다. 샘플링을 12로 하면 폰트 1px이 아틀라스 1px에 정확히 대응한다.
        // 화면 글자 크기도 12의 배수(24, 36, 48…)로 써야 픽셀이 고르게 보인다.
        private const int SamplingPointSize = 12;
        // SDF가 아닌 비트맵이고 Point 필터로 읽으므로 이웃 글리프 번짐만 막으면 된다.
        private const int AtlasPadding = 1;
        private const int AtlasSize = 2048;

        // 미리 구워 둘 글자. 그 밖의 글자는 Dynamic 모드라 처음 쓸 때 추가된다.
        private const string ExtraCharacters = "·…‘’“”※★☆♥♡○●◎◇◆□■△▲▽▼←→↑↓「」『』【】〈〉《》、。";

        [MenuItem("VillainPub/폰트/Mona 폰트 에셋 다시 만들기")]
        private static void Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null)
            {
                Debug.LogError($"[Mona 폰트] 원본 폰트를 찾을 수 없습니다: {SourceFontPath}\n외부 에셋 목록(CLAUDE.md)대로 Mona S를 임포트하세요.");
                return;
            }

            if (File.Exists(OutputPath) &&
                !EditorUtility.DisplayDialog("Mona 폰트 에셋", $"기존 폰트 에셋을 지우고 새로 만듭니다.\n{OutputPath}\n\n이 에셋을 연결해 둔 곳은 다시 연결해야 합니다.", "다시 만들기", "취소"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Mona 폰트 에셋", "글리프 굽는 중…", 0.5f);
                Create(font);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Create(Font font)
        {
            if (File.Exists(OutputPath)) AssetDatabase.DeleteAsset(OutputPath);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                font, SamplingPointSize, AtlasPadding, GlyphRenderMode.RASTER,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            if (fontAsset == null)
            {
                Debug.LogError("[Mona 폰트] 폰트 에셋 생성에 실패했습니다. 폰트 임포트 설정에서 Include Font Data가 켜져 있는지 확인하세요.", font);
                return;
            }

            string assetName = Path.GetFileNameWithoutExtension(OutputPath);
            fontAsset.name = assetName;

            AssetDatabase.CreateAsset(fontAsset, OutputPath);

            // 프로젝트 기본값(TMP Settings)은 빌드 때 동적 데이터를 비운다. 미리 구운 한글을 빌드에서도 유지하려고 끈다.
            // 해당 프로퍼티가 internal이라 직렬화 필드로 바꾼다.
            var serialized = new SerializedObject(fontAsset);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Texture2D atlas = fontAsset.atlasTexture;
            atlas.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);

            fontAsset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            bool allAdded = fontAsset.TryAddCharacters(BuildCharacterSet(), out uint[] missing);

            // 픽셀 폰트는 보간하면 흐려진다. 아틀라스가 여러 장으로 늘어난 경우도 모두 적용한다.
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                Texture2D texture = fontAsset.atlasTextures[i];
                if (texture == null) continue;
                texture.filterMode = FilterMode.Point;
                if (!AssetDatabase.IsSubAsset(texture))
                {
                    texture.name = $"{assetName} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(OutputPath);

            int glyphCount = fontAsset.characterTable.Count;
            if (!allAdded && missing != null && missing.Length > 0)
                Debug.LogWarning($"[Mona 폰트] 폰트에 없는 글자 {missing.Length}개는 제외했습니다. (예: U+{missing[0]:X4})", fontAsset);

            Debug.Log($"[Mona 폰트] 폰트 에셋을 만들었습니다: {OutputPath} (글자 {glyphCount}개, 아틀라스 {fontAsset.atlasTextures.Length}장, 샘플링 {SamplingPointSize}, Raster/Point)", fontAsset);
            EditorGUIUtility.PingObject(fontAsset);
        }

        private static uint[] BuildCharacterSet()
        {
            var set = new SortedSet<uint>();
            AddRange(set, 0x0020, 0x007E); // ASCII
            AddRange(set, 0x3131, 0x318E); // 한글 호환 자모(ㄱ, ㅏ…)
            AddRange(set, 0xAC00, 0xD7A3); // 한글 음절 11,172자
            foreach (char c in ExtraCharacters) set.Add(c);

            var result = new uint[set.Count];
            set.CopyTo(result);
            return result;
        }

        private static void AddRange(SortedSet<uint> set, uint first, uint last)
        {
            for (uint c = first; c <= last; c++) set.Add(c);
        }
    }
}

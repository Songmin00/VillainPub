using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using VillainPub.Data;

namespace VillainPub.EditorTools
{
    /// <summary>
    /// 시트를 읽어(SheetRequest) GoogleSheetParser 에셋에 채운다.
    /// 변환 에러가 하나라도 있으면 에셋을 건드리지 않는다(기존 데이터 유지).
    /// </summary>
    internal static class SheetSync
    {
        private const string ProgressTitle = "시트 동기화";
        private const char ListSeparator = '|';
        private const string CommentPrefix = "#";
        private const string EmptyMarker = "-";

        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private sealed class ColumnBinding
        {
            public int Index;
            public string Column;
            public string Header;
            public FieldInfo Field;
        }

        public static bool Sync(GoogleSheetParserBase table)
        {
            if (string.IsNullOrWhiteSpace(table.SheetId) || string.IsNullOrWhiteSpace(table.Worksheet))
            {
                Debug.LogError($"[{table.name}] 시트 ID 또는 워크시트 이름이 비어 있습니다.", table);
                return false;
            }

            bool received;
            List<List<string>> values;
            string error;
            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, $"{table.name} 시트 요청 중...", 0.5f);
                received = SheetRequest.TryReadWorksheet(table.SheetId.Trim(), table.Worksheet.Trim(), out values, out error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!received)
            {
                Debug.LogError($"[{table.name}] 시트를 읽지 못했습니다. {error}", table);
                return false;
            }
            return Apply(table, values);
        }

        public static void SyncAll()
        {
            List<GoogleSheetParserBase> tables = FindAllTables();
            if (tables.Count == 0)
            {
                Debug.LogWarning("동기화할 시트 테이블 에셋이 없습니다.");
                return;
            }

            int failed = tables.Count(t => !Sync(t));
            if (failed > 0)
                Debug.LogError($"전체 동기화: {failed}/{tables.Count}개 테이블이 실패했습니다.");

            // 다른 테이블 참조 검증은 모든 테이블이 최신일 때 의미가 있으므로 마지막에 한 번에 한다.
            ValidateAll(tables);
        }

        public static void ValidateAll() => ValidateAll(FindAllTables());

        public static bool Validate(GoogleSheetParserBase table)
        {
            var errors = new List<string>();
            table.Validate(errors);
            if (errors.Count > 0)
            {
                Debug.LogError(Join($"[{table.name}] 검증 실패 {errors.Count}건", errors), table);
                return false;
            }
            Debug.Log($"[{table.name}] 검증 통과 ({table.Count}행)", table);
            return true;
        }

        private static void ValidateAll(List<GoogleSheetParserBase> tables)
        {
            int failed = tables.Count(t => !Validate(t));
            if (failed == 0)
                Debug.Log($"<color=cyan><b>전체 검증 통과</b></color> ({tables.Count}개 테이블)");
            else
                Debug.LogError($"전체 검증: {failed}/{tables.Count}개 테이블에 문제가 있습니다.");
        }

        private static List<GoogleSheetParserBase> FindAllTables()
        {
            return AssetDatabase.FindAssets($"t:{nameof(GoogleSheetParserBase)}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<GoogleSheetParserBase>(path))
                .Where(t => t != null)
                .OrderBy(t => t.name)
                .ToList();
        }

        private static bool Apply(GoogleSheetParserBase table, List<List<string>> values)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            List<string> headerCells = RowAt(values, table.HeaderRow);
            if (headerCells == null || headerCells.All(c => string.IsNullOrWhiteSpace(c)))
            {
                Debug.LogError($"[{table.name}] 변수명 행({table.HeaderRow}행)이 비어 있습니다.", table);
                return false;
            }

            List<string> typeCells = table.TypeRow > 0 ? RowAt(values, table.TypeRow) : null;
            List<ColumnBinding> bindings = BindColumns(table.RowType, headerCells, typeCells, errors, warnings);
            var rows = new List<object>();

            for (int rowNumber = table.DataStartRow; rowNumber <= values.Count; rowNumber++)
            {
                List<string> cells = RowAt(values, rowNumber);
                if (cells == null || cells.All(c => string.IsNullOrWhiteSpace(c))) continue;
                if (CellAt(cells, 0).StartsWith(CommentPrefix)) continue;

                object row = Activator.CreateInstance(table.RowType);
                foreach (ColumnBinding binding in bindings)
                {
                    string raw = CellAt(cells, binding.Index);
                    if (TryConvert(raw, binding.Field.FieldType, out object value))
                        binding.Field.SetValue(row, value);
                    else
                        errors.Add($"{binding.Column}{rowNumber} ({binding.Header}): '{raw}'을(를) {TypeKeyword(binding.Field.FieldType)}(으)로 변환할 수 없습니다.");
                }

                if (string.IsNullOrEmpty(((ISheetRow)row).Id))
                {
                    warnings.Add($"{rowNumber}행: ID가 비어 있어 건너뜁니다.");
                    continue;
                }
                rows.Add(row);
            }

            if (errors.Count > 0)
            {
                Debug.LogError(Join($"[{table.name}] 동기화 취소, 기존 데이터를 유지합니다. 에러 {errors.Count}건", errors), table);
                return false;
            }

            Undo.RecordObject(table, $"Sync {table.name}");
            table.ReplaceRows(rows);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);

            if (warnings.Count > 0)
                Debug.LogWarning(Join($"[{table.name}] 경고 {warnings.Count}건", warnings), table);
            Debug.Log($"<color=cyan><b>{table.name}</b></color> 동기화 완료 ({rows.Count}행)", table);

            // 다른 테이블을 참조하는 검증은 그 테이블이 아직 동기화 전이면 실패할 수 있으므로 데이터는 반영하고 결과만 알린다.
            Validate(table);
            return true;
        }

        private static List<ColumnBinding> BindColumns(
            Type rowType, List<string> headerCells, List<string> typeCells,
            List<string> errors, List<string> warnings)
        {
            var bindings = new List<ColumnBinding>();
            var boundFields = new HashSet<FieldInfo>();
            var seenHeaders = new HashSet<string>();

            for (int index = 0; index < headerCells.Count; index++)
            {
                string header = CellAt(headerCells, index);
                string column = ColumnName(index);
                if (header.Length == 0 || header.StartsWith(CommentPrefix)) continue;

                if (!seenHeaders.Add(header))
                {
                    errors.Add($"변수명 '{header}'이(가) 여러 열에 있습니다. ({column}열)");
                    continue;
                }

                FieldInfo field = FindField(rowType, header);
                if (field == null)
                {
                    warnings.Add($"{column}열 '{header}': {rowType.Name}에 대응하는 필드가 없어 무시합니다. (필드명 '{header}' 또는 '_{header}')");
                    continue;
                }
                if (!IsSupported(field.FieldType))
                {
                    errors.Add($"{column}열 '{header}': 지원하지 않는 타입 {field.FieldType.Name}");
                    continue;
                }

                string sheetType = CellAt(typeCells, index);
                if (sheetType.Length > 0 && !TypeMatches(sheetType, field.FieldType))
                {
                    warnings.Add($"{column}열 '{header}': 시트 타입 '{sheetType}'와 필드 타입 '{TypeKeyword(field.FieldType)}'이 다릅니다.");
                }

                bindings.Add(new ColumnBinding { Index = index, Column = column, Header = header, Field = field });
                boundFields.Add(field);
            }

            foreach (FieldInfo field in SerializedFields(rowType).Where(f => !boundFields.Contains(f)))
                warnings.Add($"필드 '{field.Name}'에 대응하는 열이 시트에 없습니다. 기본값으로 채워집니다.");

            return bindings;
        }

        // API는 행 끝의 빈 칸과 시트 끝의 빈 행을 생략하므로, 범위 밖은 빈 칸으로 취급한다.
        private static List<string> RowAt(List<List<string>> values, int rowNumber)
        {
            return rowNumber >= 1 && rowNumber <= values.Count ? values[rowNumber - 1] : null;
        }

        private static string CellAt(List<string> row, int index)
        {
            return row != null && index < row.Count ? row[index]?.Trim() ?? string.Empty : string.Empty;
        }

        // 0 → A, 25 → Z, 26 → AA
        private static string ColumnName(int index)
        {
            var sb = new StringBuilder();
            for (int n = index + 1; n > 0; n = (n - 1) / 26)
                sb.Insert(0, (char)('A' + (n - 1) % 26));
            return sb.ToString();
        }

        // 시트 변수명 'id'는 필드 'id' 또는 '_id'에 대응한다. ([SerializeField] private _camelCase 컨벤션 지원)
        private static FieldInfo FindField(Type type, string header)
        {
            return SerializedFields(type).FirstOrDefault(f => f.Name == header)
                ?? SerializedFields(type).FirstOrDefault(f => f.Name == "_" + header);
        }

        private static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(FieldFlags))
                {
                    if (f.IsNotSerialized || f.IsInitOnly || f.IsLiteral) continue;
                    if (f.IsPublic || f.IsDefined(typeof(SerializeField), false)) yield return f;
                }
            }
        }

        #region 값 변환

        private static bool IsSupported(Type type)
        {
            Type element = ElementType(type);
            return IsScalar(element ?? type);
        }

        private static bool IsScalar(Type t) =>
            t == typeof(string) || t == typeof(int) || t == typeof(long) || t == typeof(float)
            || t == typeof(double) || t == typeof(bool) || t.IsEnum;

        private static Type ElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return null;
        }

        private static bool TryConvert(string raw, Type type, out object value)
        {
            Type element = ElementType(type);
            if (element == null)
            {
                if (string.IsNullOrEmpty(raw) || raw == EmptyMarker)
                {
                    value = type == typeof(string) ? string.Empty : Activator.CreateInstance(type);
                    return true;
                }
                return TryConvertScalar(raw, type, out value);
            }

            string[] parts = string.IsNullOrEmpty(raw) || raw == EmptyMarker
                ? Array.Empty<string>()
                : raw.Split(ListSeparator).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();

            IList list = type.IsArray
                ? Array.CreateInstance(element, parts.Length)
                : (IList)Activator.CreateInstance(type);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryConvertScalar(parts[i], element, out object item))
                {
                    value = null;
                    return false;
                }
                if (type.IsArray) list[i] = item;
                else list.Add(item);
            }

            value = list;
            return true;
        }

        private static bool TryConvertScalar(string raw, Type type, out object value)
        {
            value = null;
            CultureInfo inv = CultureInfo.InvariantCulture;

            if (type == typeof(string)) { value = raw; return true; }
            if (type == typeof(int) && int.TryParse(raw, NumberStyles.Integer, inv, out int i)) { value = i; return true; }
            if (type == typeof(long) && long.TryParse(raw, NumberStyles.Integer, inv, out long l)) { value = l; return true; }
            if (type == typeof(float) && float.TryParse(raw, NumberStyles.Float, inv, out float f)) { value = f; return true; }
            if (type == typeof(double) && double.TryParse(raw, NumberStyles.Float, inv, out double d)) { value = d; return true; }

            if (type == typeof(bool))
            {
                switch (raw.ToLowerInvariant())
                {
                    case "true": case "1": case "y": case "yes": value = true; return true;
                    case "false": case "0": case "n": case "no": value = false; return true;
                    default: return false;
                }
            }

            if (type.IsEnum)
            {
                // 숫자 문자열은 정의되지 않은 값도 통과시키므로 이름만 허용한다.
                if (char.IsDigit(raw[0]) || raw[0] == '-') return false;
                if (!Enum.TryParse(type, raw, true, out object e)) return false;
                if (!type.IsDefined(typeof(FlagsAttribute), false) && !Enum.IsDefined(type, e)) return false;
                value = e;
                return true;
            }

            return false;
        }

        private static string TypeKeyword(Type t)
        {
            Type element = ElementType(t);
            if (element != null) return TypeKeyword(element) + "[]";
            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(long)) return "long";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";
            return t.Name;
        }

        // 시트 타입 칸은 참고용이다. 'List<int>'와 'int[]'는 같은 것으로 본다.
        private static bool TypeMatches(string sheetType, Type fieldType)
        {
            string normalized = sheetType.Replace(" ", string.Empty);
            if (normalized.StartsWith("List<", StringComparison.OrdinalIgnoreCase) && normalized.EndsWith(">"))
                normalized = normalized.Substring(5, normalized.Length - 6) + "[]";
            return string.Equals(normalized, TypeKeyword(fieldType), StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        private static string Join(string title, List<string> lines)
        {
            var sb = new StringBuilder(title);
            foreach (string line in lines) sb.Append("\n  - ").Append(line);
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using GoogleSheetsToUnity;
using TinyJSON;
using UnityEngine;

namespace VillainPub.EditorTools
{
    /// <summary>
    /// 구글 시트 API에서 워크시트 전체를 읽는다. GSTU는 인증 정보 보관(GSTU_Config)과 최초 인증 창에만 쓴다.
    /// 에디터 전용 도구이므로 동기 요청으로 처리한다. 비동기(GSTU 코루틴, UnityWebRequest 완료 처리)로 했을 때
    /// 토큰 갱신 뒤 요청이 끝나지 않고 아무 로그도 남지 않는 문제가 있었다. 동기 요청은 성공·실패·타임아웃 중 하나로 반드시 끝난다.
    /// </summary>
    internal static class SheetRequest
    {
        private const string ValuesUrlFormat =
            "https://sheets.googleapis.com/v4/spreadsheets/{0}/values/{1}?majorDimension=ROWS";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";
        private const string ConfigMenu = "Window/GSTU/Open Config";
        private const int TimeoutSeconds = 30;
        // 만료 직전 토큰으로 요청하지 않도록 여유를 둔다.
        private const int TokenExpiryMarginSeconds = 60;

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public int expires_in;
            public string error;
            public string error_description;
        }

        [Serializable]
        private class ApiErrorResponse
        {
            public ApiError error;
        }

        [Serializable]
        private class ApiError
        {
            public int code;
            public string message;
            public string status;
        }

        /// <summary>
        /// 성공하면 values(인덱스 0 = 시트 1행, 행마다 길이가 다를 수 있음)를, 실패하면 error를 채운다.
        /// </summary>
        public static bool TryReadWorksheet(string sheetId, string worksheet, out List<List<string>> values, out string error)
        {
            values = null;

            GoogleSheetsToUnityConfig config = SpreadsheetManager.Config;
            if (config == null || config.gdr == null || string.IsNullOrEmpty(config.gdr.refresh_token))
            {
                error = $"GSTU 인증 정보가 없습니다. {ConfigMenu}에서 인증하세요.";
                return false;
            }

            if (!TryEnsureAccessToken(config, out error)) return false;

            // 범위에 워크시트 이름만 주면 시트 전체를 받는다. 이름은 작은따옴표로 감싸야 'A1' 같은 이름도 안전하다.
            string range = Uri.EscapeDataString($"'{worksheet.Replace("'", "''")}'");
            string url = string.Format(ValuesUrlFormat, Uri.EscapeDataString(sheetId), range);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.gdr.access_token);

            if (!TrySend(request, out int status, out string body, out error)) return false;
            if (status != 200)
            {
                error = DescribeApiError(status, body);
                return false;
            }

            try
            {
                values = JSON.Load(body).Make<ValueRange>().values ?? new List<List<string>>();
                return true;
            }
            catch (Exception e)
            {
                error = $"응답을 해석하지 못했습니다: {e.Message}";
                return false;
            }
        }

        // nextRefreshTime은 직렬화되지 않으므로 에디터를 켤 때마다 첫 요청에서 한 번 갱신된다.
        // 토큰은 메모리에만 반영한다. 저장해도 다음 세션에서 어차피 다시 갱신한다.
        private static bool TryEnsureAccessToken(GoogleSheetsToUnityConfig config, out string error)
        {
            error = null;
            if (!string.IsNullOrEmpty(config.gdr.access_token) && DateTime.Now < config.gdr.nextRefreshTime)
                return true;

            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", config.CLIENT_ID },
                    { "client_secret", config.CLIENT_SECRET },
                    { "refresh_token", config.gdr.refresh_token },
                    { "grant_type", "refresh_token" },
                }),
            };

            if (!TrySend(request, out int status, out string body, out error))
            {
                error = "토큰 갱신 중 " + error;
                return false;
            }

            TokenResponse token = TryParse<TokenResponse>(body);
            if (status != 200 || string.IsNullOrEmpty(token?.access_token))
            {
                error = DescribeTokenError(status, token);
                return false;
            }

            config.gdr.access_token = token.access_token;
            config.gdr.nextRefreshTime = DateTime.Now.AddSeconds(token.expires_in - TokenExpiryMarginSeconds);
            return true;
        }

        private static bool TrySend(HttpRequestMessage request, out int status, out string body, out string error)
        {
            status = 0;
            body = null;
            error = null;

            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) })
            using (request)
            {
                try
                {
                    // 스레드 풀에서 실행해 Unity 메인 스레드의 SynchronizationContext로 이어지지 않게 한다(동기 대기 시 교착 방지).
                    (status, body) = Task.Run(async () =>
                    {
                        using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                        {
                            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            return ((int)response.StatusCode, text);
                        }
                    }).GetAwaiter().GetResult();
                    return true;
                }
                catch (TaskCanceledException)
                {
                    error = $"{TimeoutSeconds}초 안에 응답이 없습니다.";
                    return false;
                }
                catch (Exception e)
                {
                    error = $"연결 실패: {e.GetBaseException().Message}";
                    return false;
                }
            }
        }

        private static string DescribeTokenError(int status, TokenResponse token)
        {
            if (token?.error == "invalid_grant")
                return "리프레시 토큰이 만료됐거나 취소됐습니다. " +
                       $"{ConfigMenu}에서 다시 인증하세요. (구글 클라우드의 OAuth 앱이 '테스트' 상태면 7일마다 만료됩니다)";

            string detail = token?.error_description ?? token?.error ?? "응답 없음";
            return $"토큰 갱신 실패 (HTTP {status}): {detail}. {ConfigMenu}에서 클라이언트 ID/시크릿과 인증을 확인하세요.";
        }

        private static string DescribeApiError(int status, string body)
        {
            string message = TryParse<ApiErrorResponse>(body)?.error?.message;

            string hint;
            switch (status)
            {
                case 400: hint = "워크시트(탭) 이름을 확인하세요."; break;
                case 401: hint = $"인증이 만료됐거나 잘못됐습니다. {ConfigMenu}에서 다시 인증하세요."; break;
                case 403: hint = "인증한 구글 계정에 이 시트 접근 권한이 있는지 확인하세요."; break;
                case 404: hint = "시트 ID를 확인하세요."; break;
                default: hint = string.Empty; break;
            }

            return $"HTTP {status} {message} {hint}".Trim();
        }

        private static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                // 본문이 JSON이 아니면(HTML 에러 페이지 등) HTTP 상태 코드만 쓴다.
                return null;
            }
        }
    }
}

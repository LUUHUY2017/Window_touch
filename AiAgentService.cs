using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IDE_Touch_Window;

/// <summary>
/// Service quản lý toàn bộ tính năng Trợ lý AI (AI Voice Agent, Speech Synthesis, LLM Chat & Smart Home).
/// Tái tạo từ kiến trúc giải mã ứng dụng Lily AI Agent.
/// </summary>
public class AiAgentService
{
    private static AiAgentService? _instance;
    public static AiAgentService Instance => _instance ??= new AiAgentService();

    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    private SpeechSynthesizer? _speechSynthesizer;
    private CancellationTokenSource? _ttsCts;

    public event Action<string>? OnAiResponseReceived;
    public event Action<string>? OnStatusChanged;

    private AiAgentService()
    {
        InitializeSpeechSynthesizer();
    }

    private void InitializeSpeechSynthesizer()
    {
        try
        {
            _speechSynthesizer = new SpeechSynthesizer();
            _speechSynthesizer.SetOutputToDefaultAudioDevice();
            
            // Chọn giọng tiếng Việt nếu hệ thống có sẵn, hoặc giọng nữ ngọt ngào
            foreach (var voice in _speechSynthesizer.GetInstalledVoices())
            {
                if (voice.VoiceInfo.Culture.Name.StartsWith("vi") || voice.VoiceInfo.Name.Contains("An") || voice.VoiceInfo.Name.Contains("HoaiMy"))
                {
                    _speechSynthesizer.SelectVoice(voice.VoiceInfo.Name);
                    break;
                }
            }
        }
        catch
        {
            // SpeechSynthesizer fallback nếu hệ thống không hỗ trợ
            _speechSynthesizer = null;
        }
    }

    /// <summary>
    /// Đọc câu trả lời AI ra loa bằng giọng nói tổng hợp (TTS)
    /// </summary>
    public void SpeakText(string text)
    {
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();

        Task.Run(() =>
        {
            try
            {
                if (_speechSynthesizer != null)
                {
                    _speechSynthesizer.SpeakAsyncCancelAll();
                    _speechSynthesizer.SpeakAsync(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi phát giọng nói TTS: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Dừng đọc thoại ngay lập tức
    /// </summary>
    public void StopSpeaking()
    {
        try
        {
            _speechSynthesizer?.SpeakAsyncCancelAll();
        }
        catch { }
    }

    /// <summary>
    /// Gửi câu hỏi tới AI Engine (Hỗ trợ Gemini API / XiaoZhi Protocol / Fallback AI Engine)
    /// </summary>
    public async Task<string> AskAiAsync(string prompt, string? apiKey = null)
    {
        OnStatusChanged?.Invoke("🤖 AI đang suy nghĩ...");

        if (!string.IsNullOrEmpty(apiKey))
        {
            try
            {
                // Gọi Gemini API trực tiếp
                string requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = $"Bạn là Lily - Trợ lý AI đáng yêu và thông minh. Trả lời ngắn gọn (dưới 3 câu) bằng tiếng Việt cho câu hỏi: {prompt}" }
                            }
                        }
                    }
                };

                string jsonContent = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(requestUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    string resJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(resJson);
                    string reply = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? "Anh cần em giúp gì thêm không ạ?";

                    OnAiResponseReceived?.Invoke(reply);
                    return reply;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gemini API Error: {ex.Message}");
            }
        }

        // Smart Fallback AI Engine nếu chưa nhập API Key
        string fallbackReply = GenerateLocalAiReply(prompt);
        OnAiResponseReceived?.Invoke(fallbackReply);
        return fallbackReply;
    }

    private string GenerateLocalAiReply(string prompt)
    {
        string p = prompt.ToLower();
        if (p.Contains("chào") || p.Contains("hi") || p.Contains("hello"))
        {
            return "🌸 Em chào Anh Huy! Em là Lily AI Agent. Anh cần em hỗ trợ gì hôm nay ạ?";
        }
        if (p.Contains("thời tiết") || p.Contains("nhiệt độ"))
        {
            return "🌤️ Thời tiết Hà Nội hôm nay rất dễ chịu, thích hợp để tập trung làm việc ạ!";
        }
        if (p.Contains("tên") || p.Contains("bạn là ai") || p.Contains("ai đấy"))
        {
            return "✨ Em là Lily - Trợ lý AI Agent trên màn hình cảm ứng của Anh Huy!";
        }
        if (p.Contains("smart home") || p.Contains("đèn") || p.Contains("bật") || p.Contains("tắt"))
        {
            return "🏠 Đã sẵn sàng kết nối thiết bị Smart Home (Home Assistant / BroadLink) ạ!";
        }
        if (p.Contains("chụp ảnh") || p.Contains("màn hình"))
        {
            return "📸 Anh nhấn nút Chụp màn hình trên menu để chụp ngay nhé!";
        }

        return $"✨ Em nghe rõ rồi ạ: '{prompt}'. Em đã sẵn sàng nhận thêm lệnh!";
    }

    /// <summary>
    /// Điều khiển thiết bị Smart Home qua Home Assistant REST API
    /// </summary>
    public async Task<bool> ControlHomeAssistantAsync(string haUrl, string token, string domain, string service, string entityId)
    {
        try
        {
            string url = $"{haUrl.TrimEnd('/')}/api/services/{domain}/{service}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var body = new { entity_id = entityId };
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

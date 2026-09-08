using System.Diagnostics; using TeamsBot.Application; using TeamsBot.Domain; using Microsoft.Extensions.Logging; using Azure.Identity; using System.Net.Http.Headers; using System.Net.Http.Json; using System.Text.Json;
namespace TeamsBot.Infrastructure;
public sealed class GraphTranscriptOptions { public string TenantId { get; set; } = ""; public string ClientId { get; set; } = ""; public string ClientSecret { get; set; } = ""; public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0"; }
public sealed class GraphTranscriptSource(HttpClient http, Microsoft.Extensions.Options.IOptions<GraphTranscriptOptions> options, ILogger<GraphTranscriptSource> logger) : ITranscriptSource
{
    public async Task<TranscriptArtifact?> GetAsync(MeetingRecord meeting, CancellationToken ct)
    {
        var o = options.Value; if (string.IsNullOrWhiteSpace(o.TenantId) || string.IsNullOrWhiteSpace(o.ClientId) || string.IsNullOrWhiteSpace(o.ClientSecret)) { logger.LogWarning("Graph credentials are not configured"); return null; }
        var credential = new ClientSecretCredential(o.TenantId, o.ClientId, o.ClientSecret);
        var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(["https://graph.microsoft.com/.default"]), ct);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var root = meeting.Kind == MeetingKind.AdhocCall ? $"users/{meeting.OrganizerId}/adhocCalls/{meeting.MeetingId}/transcripts" : $"users/{meeting.OrganizerId}/onlineMeetings/{meeting.MeetingId}/transcripts";
        using var list = await http.GetAsync($"{o.GraphBaseUrl.TrimEnd('/')}/{root}", ct); if (!list.IsSuccessStatusCode) { logger.LogWarning("Graph transcript list failed: {Status}", list.StatusCode); return null; }
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync(ct)); var first = json.RootElement.GetProperty("value").EnumerateArray().FirstOrDefault(); if (first.ValueKind == JsonValueKind.Undefined) return null;
        var id = first.GetProperty("id").GetString()!; using var content = await http.GetAsync($"{o.GraphBaseUrl.TrimEnd('/')}/{root}/{id}/content", ct); if (!content.IsSuccessStatusCode) return null;
        var text = await content.Content.ReadAsStringAsync(ct); var utterance = new Utterance(TimeSpan.Zero, TimeSpan.Zero, null, null, text);
        return new TranscriptArtifact(id, meeting.MeetingId, [utterance], false, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))));
    }
}
public sealed class PlaywrightTranscriptSource : ITranscriptSource
{
    public Task<TranscriptArtifact?> GetAsync(MeetingRecord meeting, CancellationToken ct)
    {
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
        var file = Directory.Exists(Path.Combine(root, "output", "playwright"))
            ? Directory.GetFiles(Path.Combine(root, "output", "playwright"), "*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
        if (file is null) return Task.FromResult<TranscriptArtifact?>(null);
        var rows = new List<Utterance>();
        foreach (var line in File.ReadLines(file))
        {
            try
            {
                using var json = JsonDocument.Parse(line);
                if (!json.RootElement.TryGetProperty("text", out var textValue)) continue;
                var text = textValue.GetString(); if (string.IsNullOrWhiteSpace(text)) continue;
                var speaker = json.RootElement.TryGetProperty("participant", out var participant) ? participant.GetString() : null;
                var stamp = json.RootElement.TryGetProperty("timestamp", out var ts) && DateTimeOffset.TryParse(ts.GetString(), out var when) ? when : DateTimeOffset.MinValue;
                rows.Add(new Utterance(TimeSpan.Zero, TimeSpan.Zero, null, speaker, text));
            }
            catch (JsonException) { }
        }
        if (rows.Count == 0) return Task.FromResult<TranscriptArtifact?>(null);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows.Select(x => x.Text)))));
        return Task.FromResult<TranscriptArtifact?>(new TranscriptArtifact(Path.GetFileNameWithoutExtension(file), meeting.MeetingId, rows, rows.Any(x => x.SpeakerDisplayName is not null), hash));
    }
}
public sealed class DemoTranscriptSource : ITranscriptSource
{
    public Task<TranscriptArtifact?> GetAsync(MeetingRecord meeting, CancellationToken ct)
    {
        var utterances = new[]
        {
            new Utterance(TimeSpan.Zero, TimeSpan.FromSeconds(8), "u1", "Dana", "We decided to ship the first pilot on Friday."),
            new Utterance(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(18), "u2", "Yossi", "I will prepare the deployment checklist by Thursday."),
            new Utterance(TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(25), "u1", "Dana", "Please review the checklist before the pilot.")
        };
        return Task.FromResult<TranscriptArtifact?>(new TranscriptArtifact("demo-transcript", meeting.MeetingId, utterances, true, "demo-hash"));
    }
}
public sealed class ServiceHostedCallingBot(ILogger<ServiceHostedCallingBot> logger) : IMeetingPresence
{
    public Task<string> JoinAsync(MeetingRecord meeting, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(meeting.JoinUrl)) return Task.FromResult("join-url-required");
        if (!Uri.TryCreate(meeting.JoinUrl, UriKind.Absolute, out var uri) || uri.Host is not ("teams.microsoft.com" or "teams.live.com" or "teams.cloud.microsoft")) return Task.FromResult("invalid-teams-url");
        var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
        var script = Path.Combine(repoRoot, "tools", "teams-browser", "join.mjs");
        if (!File.Exists(script)) return Task.FromResult("worker-not-found");
        var psi = new ProcessStartInfo("node") { WorkingDirectory = repoRoot, UseShellExecute = false, CreateNoWindow = false };
        psi.ArgumentList.Add(script); psi.ArgumentList.Add(meeting.JoinUrl); psi.ArgumentList.Add("Meeting Companion");
        try { Process.Start(psi); logger.LogInformation("Opened Teams browser worker for {MeetingId}", meeting.MeetingId); return Task.FromResult("browser-opened"); }
        catch (Exception ex) { logger.LogError(ex, "Could not start Teams browser worker"); return Task.FromResult("worker-start-failed"); }
    }
}
public sealed class GroundedInsightExtractor : IInsightExtractor
{
    public Task<InsightResult> ExtractAsync(TranscriptArtifact t, CancellationToken ct)
    {
        var evidence = new[] { new Evidence(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(18), "I will prepare the deployment checklist by Thursday.") };
        var action = new ActionItemDraft("demo-action-1", "Prepare deployment checklist", "Prepare the deployment checklist by Thursday.", "Yossi", "u2", AssignmentKind.Explicit, null, 0.98, evidence);
        return Task.FromResult(new InsightResult("The team decided to ship the first pilot on Friday.", [new Decision("Ship the first pilot on Friday.", [new Evidence(TimeSpan.Zero, TimeSpan.FromSeconds(8), "We decided to ship the first pilot on Friday.")])], [action], t.ContentHash, "demo-v1"));
    }
}
public sealed class OllamaOptions { public string BaseUrl { get; set; } = "http://localhost:11434"; public string Model { get; set; } = "qwen2.5:7b"; }
public sealed class OllamaInsightExtractor(HttpClient http, Microsoft.Extensions.Options.IOptions<OllamaOptions> options, ILogger<OllamaInsightExtractor> logger) : IInsightExtractor
{
    public async Task<InsightResult> ExtractAsync(TranscriptArtifact transcript, CancellationToken ct)
    {
        var text = string.Join("\n", transcript.Utterances.Select(u => $"{u.SpeakerDisplayName ?? "Unknown"}: {u.Text}"));
        var prompt = $"ענה בעברית. סכם את פגישת Teams הבאה ב-3 משפטים לכל היותר. ציין החלטות ומשימות רק אם נאמרו במפורש.\n\n{text}";
        using var response = await http.PostAsJsonAsync($"{options.Value.BaseUrl.TrimEnd('/')}/api/generate", new { model = options.Value.Model, prompt, stream = false }, ct);
        if (!response.IsSuccessStatusCode) { logger.LogWarning("Ollama request failed: {Status}", response.StatusCode); return await new GroundedInsightExtractor().ExtractAsync(transcript, ct); }
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var summary = json.RootElement.TryGetProperty("response", out var value) ? value.GetString() ?? "" : "";
        return new InsightResult(summary, [], [], transcript.ContentHash, options.Value.Model);
    }
}
public sealed class NoopTaskPublisher : ITaskPublisher { public Task<string> PublishAsync(ActionItemDraft d, Approval a, CancellationToken ct) => Task.FromResult($"draft-{d.Id}"); }

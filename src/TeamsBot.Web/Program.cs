using TeamsBot.Application; using TeamsBot.Domain; using TeamsBot.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
if (builder.Configuration.GetValue("DemoMode", true)) builder.Services.AddSingleton<ITranscriptSource, DemoTranscriptSource>(); else { builder.Services.Configure<GraphTranscriptOptions>(builder.Configuration.GetSection("Graph")); builder.Services.AddHttpClient<ITranscriptSource, GraphTranscriptSource>(); }
builder.Services.AddSingleton<IMeetingPresence, ServiceHostedCallingBot>();
if (builder.Configuration.GetValue("Ai:Provider", "ollama") == "ollama") { builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ai:Ollama")); builder.Services.AddHttpClient<IInsightExtractor, OllamaInsightExtractor>(); } else builder.Services.AddSingleton<IInsightExtractor, GroundedInsightExtractor>();
builder.Services.AddSingleton<ITaskPublisher, NoopTaskPublisher>(); builder.Services.AddSingleton<MeetingCompanionService>();
var app = builder.Build();
app.UseCors();
app.UseDefaultFiles(); app.UseStaticFiles();


app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/meetings/{meetingId}/join", async (string meetingId, JoinRequest r, MeetingCompanionService s, CancellationToken ct) => { if (!r.Confirmed) return Results.BadRequest(new { error = "Explicit confirmation is required." }); var m = new MeetingRecord(r.TenantId, meetingId, r.Kind, r.UserId, r.JoinUrl, MeetingState.Planned); return Results.Ok(new { status = await s.JoinAsync(m, ct) }); });
app.MapPost("/api/meetings/{meetingId}/process", async (string meetingId, ProcessRequest r, MeetingCompanionService s, CancellationToken ct) => { var m = new MeetingRecord(r.TenantId, meetingId, r.Kind, r.UserId, null, MeetingState.MeetingEnded); var result = await s.ProcessAsync(m, ct); return result is null ? Results.Problem("Transcript unavailable or Graph access is not configured.", statusCode: 424) : Results.Ok(result); });
app.MapGet("/api/meetings/{meetingId}/transcript", async (string meetingId, ITranscriptSource source, CancellationToken ct) => { var m = new MeetingRecord("local", meetingId, MeetingKind.OnlineMeeting, "local-user", null, MeetingState.MeetingEnded); var t = await source.GetAsync(m, ct); return t is null ? Results.NotFound(new { error = "Transcript is not available yet." }) : Results.Ok(new { t.Id, t.MeetingId, t.SpeakerAttributionAvailable, utterances = t.Utterances.Select(u => new { start = u.Start.ToString(), speaker = u.SpeakerDisplayName, text = u.Text }) }); });
app.MapPost("/api/meetings/{meetingId}/actions/{actionId}/approve", async (string meetingId, string actionId, PublishRequest r, MeetingCompanionService s, CancellationToken ct) => { if (r.Draft.Id != actionId) return Results.BadRequest(new { error = "Action id mismatch." }); var approval = new Approval(meetingId, actionId, r.UserId, r.Destination, MeetingCompanionService.VersionHash(r.Draft), DateTimeOffset.UtcNow); return Results.Ok(new { taskId = await s.PublishApprovedAsync(r.Draft, approval, ct), approval }); });
app.Run();
public sealed record JoinRequest(string TenantId, string UserId, MeetingKind Kind, string? JoinUrl, bool Confirmed);
public sealed record ProcessRequest(string TenantId, string UserId, MeetingKind Kind);
public sealed record PublishRequest(string UserId, string Destination, ActionItemDraft Draft);

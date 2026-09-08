using TeamsBot.Domain;
namespace TeamsBot.Application;
public interface ITranscriptSource { Task<TranscriptArtifact?> GetAsync(MeetingRecord meeting, CancellationToken cancellationToken); }
public interface IInsightExtractor { Task<InsightResult> ExtractAsync(TranscriptArtifact transcript, CancellationToken cancellationToken); }
public interface ITaskPublisher { Task<string> PublishAsync(ActionItemDraft draft, Approval approval, CancellationToken cancellationToken); }
public interface IMeetingPresence { Task<string> JoinAsync(MeetingRecord meeting, CancellationToken cancellationToken); }
public sealed class MeetingCompanionService(ITranscriptSource transcripts, IInsightExtractor insights, ITaskPublisher publisher, IMeetingPresence presence)
{
    public Task<string> JoinAsync(MeetingRecord meeting, CancellationToken ct) => presence.JoinAsync(meeting, ct);
    public async Task<InsightResult?> ProcessAsync(MeetingRecord meeting, CancellationToken ct) { var t = await transcripts.GetAsync(meeting, ct); return t is null ? null : await insights.ExtractAsync(t, ct); }
    public Task<string> PublishApprovedAsync(ActionItemDraft draft, Approval approval, CancellationToken ct) { if (approval.ActionItemId != draft.Id || approval.VersionHash != VersionHash(draft)) throw new InvalidOperationException("Approval does not match the current action draft."); return publisher.PublishAsync(draft, approval, ct); }
    public static string VersionHash(ActionItemDraft d) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{d.Id}|{d.Title}|{d.Details}|{d.OwnerDirectoryId}|{d.DueDate}")));
}

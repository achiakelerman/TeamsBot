namespace TeamsBot.Domain;
public enum MeetingKind { OnlineMeeting, AdhocCall }
public enum MeetingState { Planned, MeetingStarted, MeetingEnded, TranscriptPending, TranscriptReceived, Processing, ReviewReady, Delivered, ApprovedActionsCreated, NotTranscribed, PermissionBlocked, PresenceDenied, ProcessingFailed, Deleted }
public enum AssignmentKind { Explicit, Suggested, Unassigned }
public enum ReviewState { Pending, Approved, Rejected }
public sealed record MeetingRecord(string TenantId, string MeetingId, MeetingKind Kind, string OrganizerId, string? JoinUrl, MeetingState State);
public sealed record Utterance(TimeSpan Start, TimeSpan End, string? SpeakerId, string? SpeakerDisplayName, string Text);
public sealed record Evidence(TimeSpan Start, TimeSpan End, string Quote);
public sealed record Decision(string Text, IReadOnlyList<Evidence> Evidence);
public sealed record ActionItemDraft(string Id, string Title, string Details, string? OwnerDisplayName, string? OwnerDirectoryId, AssignmentKind Assignment, DateOnly? DueDate, double Confidence, IReadOnlyList<Evidence> Evidence, ReviewState ReviewState = ReviewState.Pending);
public sealed record InsightResult(string Summary, IReadOnlyList<Decision> Decisions, IReadOnlyList<ActionItemDraft> ActionItems, string TranscriptHash, string ModelVersion);
public sealed record TranscriptArtifact(string Id, string MeetingId, IReadOnlyList<Utterance> Utterances, bool SpeakerAttributionAvailable, string ContentHash);
public sealed record Approval(string MeetingId, string ActionItemId, string UserId, string Destination, string VersionHash, DateTimeOffset ApprovedAt);

namespace FateHelper.Core.News;

/// <summary>One line of the release notes: what kind of change it was, and what changed.</summary>
public sealed record ReleaseNote(ReleaseNoteKind Kind, string Text);

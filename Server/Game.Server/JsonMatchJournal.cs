using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Server;

public sealed record MatchJournalEntry(
    long Sequence,
    string Type,
    string PlayerId,
    SubmitCommandRequest? Command,
    ReadyRequest? Ready,
    int Revision,
    int Turn,
    string StateHash,
    DateTimeOffset UtcTime);

public sealed class JsonMatchJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _journalPath;
    private readonly string _snapshotPath;
    private long _nextSequence = 1;

    public JsonMatchJournal(string dataDirectory, string matchId)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("데이터 디렉터리가 필요합니다.", nameof(dataDirectory));
        if (string.IsNullOrWhiteSpace(matchId))
            throw new ArgumentException("매치 ID가 필요합니다.", nameof(matchId));

        Directory.CreateDirectory(dataDirectory);
        string safeMatchId = string.Concat(matchId.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_'));
        _journalPath = Path.Combine(dataDirectory, $"{safeMatchId}.journal.jsonl");
        _snapshotPath = Path.Combine(dataDirectory, $"{safeMatchId}.snapshot.json");
    }

    public IReadOnlyList<MatchJournalEntry> Load()
    {
        if (!File.Exists(_journalPath))
            return Array.Empty<MatchJournalEntry>();

        var entries = new List<MatchJournalEntry>();
        long expectedSequence = 1;

        foreach (string line in File.ReadLines(_journalPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            MatchJournalEntry entry = JsonSerializer.Deserialize<MatchJournalEntry>(
                line,
                JsonOptions) ?? throw new InvalidDataException(
                    "PvP JSON 저널 항목을 읽을 수 없습니다.");
            if (entry.Sequence != expectedSequence)
            {
                throw new InvalidDataException(
                    $"PvP JSON 저널 순서가 손상되었습니다. expected={expectedSequence}, actual={entry.Sequence}");
            }

            entries.Add(entry);
            expectedSequence++;
        }

        _nextSequence = expectedSequence;
        return entries;
    }

    public void AppendCommand(
        AuthenticatedPlayer player,
        SubmitCommandRequest request,
        int revision,
        int turn)
    {
        Append(new MatchJournalEntry(
            _nextSequence++,
            "command",
            player.PlayerId,
            request,
            null,
            revision,
            turn,
            string.Empty,
            DateTimeOffset.UtcNow));
    }

    public void AppendReady(
        AuthenticatedPlayer player,
        ReadyRequest request,
        int revision,
        int turn,
        string stateHash)
    {
        Append(new MatchJournalEntry(
            _nextSequence++,
            "ready",
            player.PlayerId,
            null,
            request,
            revision,
            turn,
            stateHash ?? string.Empty,
            DateTimeOffset.UtcNow));
    }

    public void SaveSnapshot(object snapshot)
    {
        string temporaryPath = _snapshotPath + ".tmp";
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);

        using (var stream = new FileStream(
                   temporaryPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   FileOptions.WriteThrough))
        using (var writer = new StreamWriter(
                   stream,
                   new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, _snapshotPath, overwrite: true);
    }

    public void AppendResolution(
        int revision,
        int turn,
        string stateHash)
    {
        Append(new MatchJournalEntry(
            _nextSequence++,
            "resolution",
            string.Empty,
            null,
            null,
            revision,
            turn,
            stateHash,
            DateTimeOffset.UtcNow));
    }

    public static string ComputeRequestHash<T>(T request)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(json));
    }

    private void Append(MatchJournalEntry entry)
    {
        string json = JsonSerializer.Serialize(entry, JsonOptions);
        using var stream = new FileStream(
            _journalPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.WriteThrough);
        using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(json);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}

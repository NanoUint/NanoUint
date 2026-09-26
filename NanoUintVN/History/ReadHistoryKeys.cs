using System.Security.Cryptography;
using System.Text;
using NanoUintVN.Dialogue;

namespace NanoUintVN.History;

/// <summary>Builds the stable identity used to record that a line has been read.</summary>
public static class ReadHistoryKeys
{
    /// <summary>
    /// Content-based key, so reordering pages or renaming documents does not invalidate history.
    /// Two lines with identical speaker and text intentionally share a key.
    /// </summary>
    public static string ForBeat(DialogueBeat beat) => ForLine(beat.Speaker, beat.Text);

    public static string ForLine(string speaker, string text)
    {
        var payload = speaker + "\u0001" + text;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}

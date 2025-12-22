using System.Text;

namespace TLScope.Utilities;

/// <summary>
/// Combines SSH randomart with avatar character overlay
/// </summary>
public static class RandomartAvatarUtility
{
    /// <summary>
    /// Generate combined randomart with avatar overlay
    /// Avatar (9 chars wide × 4 lines) is placed in center of randomart (17 chars wide × 9 content lines)
    /// </summary>
    /// <param name="sshPublicKey">SSH public key for randomart generation</param>
    /// <param name="avatarLines">4-line avatar (9 chars wide each)</param>
    /// <returns>11-line combined art (includes header and footer)</returns>
    public static string[] GenerateCombinedArt(string sshPublicKey, string[] avatarLines)
    {
        if (avatarLines == null || avatarLines.Length != 4)
        {
            throw new ArgumentException("Avatar must have exactly 4 lines", nameof(avatarLines));
        }

        var randomart = SSHRandomart.GenerateRandomart(sshPublicKey);

        var combined = randomart.Select(line => line).ToArray();


        int avatarStartLine = 4; // Line 4 of full randomart (line 3 of content, 0-indexed)
        int avatarStartCol = 5;  // Column 5 inside the border (|X____...) where X is the border

        for (int i = 0; i < 4 && i < avatarLines.Length; i++)
        {
            int randomartLineIndex = avatarStartLine + i;

            if (randomartLineIndex >= randomart.Length - 1)
                break; // Don't overwrite footer

            var line = combined[randomartLineIndex];
            var avatarLine = avatarLines[i];

            if (avatarLine.Length < 9)
                avatarLine = avatarLine.PadRight(9);
            else if (avatarLine.Length > 9)
                avatarLine = avatarLine.Substring(0, 9);

            var sb = new StringBuilder();
            sb.Append(line[0]); // Left border '|'

            for (int col = 1; col < avatarStartCol; col++)
            {
                sb.Append(line[col]);
            }

            sb.Append(avatarLine);

            int afterAvatarCol = avatarStartCol + 9;
            for (int col = afterAvatarCol; col < line.Length - 1; col++)
            {
                sb.Append(line[col]);
            }

            sb.Append(line[line.Length - 1]); // Right border '|'

            combined[randomartLineIndex] = sb.ToString();
        }

        return combined;
    }

    /// <summary>
    /// Convert combined art array to single string with newline separators (for database storage)
    /// </summary>
    public static string CombinedArtToString(string[] combinedArt)
    {
        return string.Join("\n", combinedArt);
    }

    /// <summary>
    /// Convert combined art string to array (from database storage)
    /// </summary>
    public static string[] StringToCombinedArt(string combinedArtString)
    {
        if (string.IsNullOrEmpty(combinedArtString))
            return Array.Empty<string>();

        return combinedArtString.Split('\n');
    }
}

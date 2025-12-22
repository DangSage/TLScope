using System.Security.Cryptography;
using System.Text;

namespace TLScope.Utilities;

/// <summary>
/// Generates SSH randomart visualization from SSH public keys
/// Uses the "drunken bishop" algorithm
/// </summary>
public static class SSHRandomart
{
    private const int Width = 17;
    private const int Height = 9;
    private static readonly char[] Characters = { ' ', '.', 'o', '+', '=', '*', 'B', 'O', 'X', '@', '%', '&', '#', '/', '^' };

    /// <summary>
    /// Generate SSH randomart from a public key string
    /// </summary>
    public static string[] GenerateRandomart(string sshPublicKey)
    {
        try
        {
            // Generate fingerprint from SSH key
            byte[] fingerprint = GenerateFingerprint(sshPublicKey);

            // Create the field
            int[,] field = new int[Height, Width];

            // Start position (center)
            int x = Width / 2;
            int y = Height / 2;

            // Process each byte of the fingerprint
            foreach (byte b in fingerprint)
            {
                // Process 4 moves per byte (2 bits each)
                for (int i = 0; i < 4; i++)
                {
                    int move = (b >> (i * 2)) & 0x3;

                    // Move based on direction
                    switch (move)
                    {
                        case 0: // up-left
                            if (y > 0) y--;
                            if (x > 0) x--;
                            break;
                        case 1: // up-right
                            if (y > 0) y--;
                            if (x < Width - 1) x++;
                            break;
                        case 2: // down-left
                            if (y < Height - 1) y++;
                            if (x > 0) x--;
                            break;
                        case 3: // down-right
                            if (y < Height - 1) y++;
                            if (x < Width - 1) x++;
                            break;
                    }

                    // Increment position value
                    if (field[y, x] < Characters.Length - 1)
                        field[y, x]++;
                }
            }

            // Mark start and end positions
            int startX = Width / 2;
            int startY = Height / 2;

            // Build the output
            var lines = new List<string>();
            lines.Add("+---[RSA 2048]----+");

            for (int row = 0; row < Height; row++)
            {
                var sb = new StringBuilder("|");
                for (int col = 0; col < Width; col++)
                {
                    if (col == x && row == y)
                        sb.Append('E'); // End position
                    else if (col == startX && row == startY)
                        sb.Append('S'); // Start position
                    else
                    {
                        int value = Math.Min(field[row, col], Characters.Length - 1);
                        sb.Append(Characters[value]);
                    }
                }
                sb.Append('|');
                lines.Add(sb.ToString());
            }

            lines.Add("+----[SHA256]-----+");

            return lines.ToArray();
        }
        catch
        {
            // Return a default randomart on error
            return new[]
            {
                "+---[RSA 2048]----+",
                "|                 |",
                "|                 |",
                "|                 |",
                "|                 |",
                "|        S        |",
                "|                 |",
                "|                 |",
                "|                 |",
                "|                 |",
                "+----[SHA256]-----+"
            };
        }
    }

    private static byte[] GenerateFingerprint(string sshPublicKey)
    {
        // Simple hash-based fingerprint generation
        // In a real implementation, you would parse the actual key data
        var keyData = Encoding.UTF8.GetBytes(sshPublicKey);
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(keyData);
    }
}

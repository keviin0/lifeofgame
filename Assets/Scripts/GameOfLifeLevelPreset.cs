using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject defining a single level: grid size and initial live cells.
/// Create via Assets > Create > Game Of Life > Level Preset.
/// </summary>
[CreateAssetMenu(fileName = "New Level", menuName = "Game Of Life/Level Preset")]
public class GameOfLifeLevelPreset : ScriptableObject
{
    [Tooltip("Stable identifier used for this level's leaderboard name. Keep this fixed even if you reorder levels. If left blank, falls back to a sanitized version of the asset name.")]
    [SerializeField] private string leaderboardKey;

    /// <summary>
    /// Stable key used to build this level's leaderboard name.
    /// Falls back to a sanitized asset name if <c>leaderboardKey</c> is not set.
    /// </summary>
    public string LeaderboardKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(leaderboardKey)) return leaderboardKey.Trim();
            return SanitizeKey(name);
        }
    }

    private static string SanitizeKey(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "unnamed";
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
        }
        var result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }

    [Tooltip("Grid width (number of cells)")]
    public int gridWidth = 16;

    [Tooltip("Grid height (number of cells)")]
    public int gridHeight = 16;

    [Tooltip("Initial live cell positions in grid coordinates (0,0 = bottom-left). Add entries in inspector or use the helper below.")]
    public List<Vector2Int> initialLiveCells = new List<Vector2Int>();

    [Tooltip("Grid positions for collectible cells. These do NOT start alive in the simulation; they are spawned as separate collectible objects.")]
    public List<Vector2Int> collectibleCells = new List<Vector2Int>();

    [Tooltip("Optional grid positions where the cursor/player can start. Not yet used by gameplay, but editable in the level grid.")]
    public List<Vector2Int> cursorStartCells = new List<Vector2Int>();

    [Tooltip("Main menu: easy-mode coin (2 lives). Not part of the Life simulation grid. Not included in URL/Base64 encoding.")]
    public List<Vector2Int> easyModeCoinCells = new List<Vector2Int>();

    [Tooltip("Main menu: hard-mode coin (1 life). Not part of the Life simulation grid. Not included in URL/Base64 encoding.")]
    public List<Vector2Int> hardModeCoinCells = new List<Vector2Int>();

    /// <summary>
    /// Optional: set initial state from a simple pattern string.
    /// '1' or '#' = alive, anything else = dead. One row per line.
    /// Example 3x3 glider:
    /// ".1.\n1.1\n.11"
    /// </summary>
    public void SetFromPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;
        initialLiveCells.Clear();
        string[] rows = pattern.Trim().Split('\n');
        int h = rows.Length;
        int w = 0;
        foreach (string row in rows)
            w = Mathf.Max(w, row.Length);
        gridHeight = h;
        gridWidth = w;
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y];
            for (int x = 0; x < row.Length && x < w; x++)
            {
                char c = row[x];
                if (c == '1' || c == '#')
                    initialLiveCells.Add(new Vector2Int(x, y));
            }
        }
    }

    public string ToBase64()
    {
        int totalCells = gridWidth * gridHeight;
        int totalBits = totalCells * 2;

        // +2 bytes for width/height (since max is 63 from your 6-bit comment)
        int byteCount = (totalBits + 7) / 8 + 2;

        byte[] bytes = new byte[byteCount];

        // store width & height (1 byte each is enough for <=255)
        bytes[0] = (byte)gridWidth;
        bytes[1] = (byte)gridHeight;

        // construct grid
        int[,] grid = new int[gridWidth, gridHeight];

        foreach (var cell in initialLiveCells)
            grid[cell.x, cell.y] = 1;

        foreach (var cell in collectibleCells)
            grid[cell.x, cell.y] = 2;

        foreach (var cell in cursorStartCells)
            grid[cell.x, cell.y] = 3;

        // pack 2 bits per cell
        int bitIndex = 0;

        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                int value = grid[col, row] & 0b11;

                int byteIndex = 2 + (bitIndex / 8);
                int offset = bitIndex % 8;

                bytes[byteIndex] |= (byte)(value << offset);

                // handle overflow into next byte
                if (offset > 6)
                {
                    bytes[byteIndex + 1] |= (byte)(value >> (8 - offset));
                }

                bitIndex += 2;
            }
        }

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string NormalizeUrlSafeBase64ForDecode(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return string.Empty;
        string s = base64.Trim().Replace('-', '+').Replace('_', '/');
        int pad = (4 - (s.Length % 4)) % 4;
        if (pad > 0) s += new string('=', pad);
        return s;
    }

    static int ReadPackedTwoBits(byte[] bytes, int bitIndex)
    {
        int byteIndex = 2 + bitIndex / 8;
        int offset = bitIndex % 8;
        if (offset <= 6)
            return (bytes[byteIndex] >> offset) & 0b11;
        return ((bytes[byteIndex] >> offset) & 1) | ((bytes[byteIndex + 1] & 1) << 1);
    }

    public void FromBase64(string base64)
    {
        TryFromBase64(base64, out _);
    }

    public bool TryFromBase64(string base64, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(base64))
        {
            error = "String is empty.";
            return false;
        }

        string normalized = NormalizeUrlSafeBase64ForDecode(base64);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(normalized);
        }
        catch (FormatException ex)
        {
            error = "Invalid Base64: " + ex.Message;
            return false;
        }

        if (bytes.Length < 2)
        {
            error = "Data too short (need width and height bytes).";
            return false;
        }

        int width = bytes[0];
        int height = bytes[1];
        if (width <= 0 || height <= 0)
        {
            error = $"Invalid size: {width}x{height}.";
            return false;
        }

        int totalBits = width * height * 2;
        int expectedLen = 2 + (totalBits + 7) / 8;
        if (bytes.Length < expectedLen)
        {
            error = $"Expected at least {expectedLen} bytes for {width}x{height}, got {bytes.Length}.";
            return false;
        }

        gridWidth = width;
        gridHeight = height;

        initialLiveCells.Clear();
        collectibleCells.Clear();
        cursorStartCells.Clear();
        easyModeCoinCells.Clear();
        hardModeCoinCells.Clear();

        int cellIndex = 0;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int value = ReadPackedTwoBits(bytes, cellIndex * 2);
                cellIndex++;
                switch (value)
                {
                    case 1:
                        initialLiveCells.Add(new Vector2Int(col, row));
                        break;
                    case 2:
                        collectibleCells.Add(new Vector2Int(col, row));
                        break;
                    case 3:
                        cursorStartCells.Add(new Vector2Int(col, row));
                        break;
                }
            }
        }

        return true;
    }

    public static bool VerifyRoundTripBase64(string base64, out string error)
    {
        error = null;
        var temp = ScriptableObject.CreateInstance<GameOfLifeLevelPreset>();
        if (!temp.TryFromBase64(base64, out error))
        {
            UnityEngine.Object.DestroyImmediate(temp);
            return false;
        }

        string roundtrip = temp.ToBase64();
        UnityEngine.Object.DestroyImmediate(temp);

        try
        {
            byte[] a = Convert.FromBase64String(NormalizeUrlSafeBase64ForDecode(base64));
            byte[] b = Convert.FromBase64String(NormalizeUrlSafeBase64ForDecode(roundtrip));
            if (!a.SequenceEqual(b))
            {
                error = "Round-trip byte mismatch after decode/re-encode.";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a copy of the initial live cells list (read-only usage).
    /// </summary>
    public IReadOnlyList<Vector2Int> GetInitialLiveCells()
    {
        return initialLiveCells;
    }

    /// <summary>
    /// Read-only view of collectible cell positions.
    /// </summary>
    public IReadOnlyList<Vector2Int> GetCollectibleCells()
    {
        return collectibleCells;
    }

    /// <summary>
    /// Read-only view of cursor start cell positions.
    /// </summary>
    public IReadOnlyList<Vector2Int> GetCursorStartCells()
    {
        return cursorStartCells;
    }

    public IReadOnlyList<Vector2Int> GetEasyModeCoinCells()
    {
        return easyModeCoinCells;
    }

    public IReadOnlyList<Vector2Int> GetHardModeCoinCells()
    {
        return hardModeCoinCells;
    }
}

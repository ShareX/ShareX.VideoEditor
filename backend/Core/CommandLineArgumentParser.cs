#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using System.Text;

namespace ShareX.VideoEditor.Core;

internal static class CommandLineArgumentParser
{
    /// <summary>
    /// Converts the library's legacy argument-string API into discrete arguments.
    /// New internal call sites stay structured after this compatibility boundary.
    /// </summary>
    public static IReadOnlyList<string> Parse(string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);

        var arguments = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        int backslashes = 0;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char character = commandLine[i];
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                current.Append('\\', backslashes / 2);
                if (backslashes % 2 == 1)
                {
                    current.Append('"');
                }
                else
                {
                    quoted = !quoted;
                }

                backslashes = 0;
                continue;
            }

            current.Append('\\', backslashes);
            backslashes = 0;

            if (char.IsWhiteSpace(character) && !quoted)
            {
                AddArgument(arguments, current);
            }
            else
            {
                current.Append(character);
            }
        }

        current.Append('\\', backslashes);
        if (quoted)
        {
            throw new FormatException("FFmpeg arguments contain an unterminated quoted value.");
        }

        AddArgument(arguments, current);
        return arguments;
    }

    private static void AddArgument(List<string> arguments, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        arguments.Add(current.ToString());
        current.Clear();
    }
}

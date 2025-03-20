using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using Uncreated.Warfare.Util;
using UnityEngine;

namespace ConflictScanner;

internal static class TerminalColorHelper
{
    /// <summary>
    /// ANSI escape character for virtual terminal sequences.
    /// </summary>>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public const char ConsoleEscapeCharacter = '\e';

    /// <summary>
    /// Visual ANSI virtual termianl sequence for reseting the foreground color.
    /// </summary>
    public const string ForegroundResetSequence = "\e[39m";

    /// <summary>
    /// Visual ANSI virtual termianl sequence for reseting the background color.
    /// </summary>
    public const string BackgroundResetSequence = "\e[49m";

    private const int DefaultForeground = -9013642;  // gray
    private const int DefaultBackground = -15987700; // black

#pragma warning disable CS8500

    /// <summary>
    /// Wraps text in an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static unsafe string WrapMessageWithTerminalColorSequence(ConsoleColor color, ReadOnlySpan<char> message, bool background = false)
    {
        WrapMessageWithColor8BitState state = default;
        state.Message = &message;
        state.Color = color;
        state.Background = background;
        state.ColorLength = GetTerminalColorSequenceLength(color, background);

        return string.Create(state.ColorLength + message.Length + ForegroundResetSequence.Length, state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.Color, state.Background);
            ReadOnlySpan<char> reset = state.Background ? BackgroundResetSequence : ForegroundResetSequence;
            reset.CopyTo(span.Slice(span.Length - reset.Length, reset.Length));
            state.Message->CopyTo(span[state.ColorLength..]);
        });
    }

    private unsafe struct WrapMessageWithColor8BitState
    {
        public ReadOnlySpan<char>* Message;
        public ConsoleColor Color;
        public bool Background;
        public int ColorLength;
    }

    /// <summary>
    /// Wraps text in an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static string WrapMessageWithTerminalColorSequence(int argb, ReadOnlySpan<char> message, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return WrapMessageWithTerminalColorSequence(color, message, background);
            }

            return WrapMessageWithTerminalColorSequence((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, message, background);
        }
    }

    /// <summary>
    /// Wraps text in an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static unsafe string WrapMessageWithTerminalColorSequence(byte r, byte g, byte b, ReadOnlySpan<char> message, bool background = false)
    {
        WrapMessageWithColorRGBState state = default;
        state.Message = &message;
        state.R = r;
        state.G = g;
        state.B = b;
        state.ColorLength = GetTerminalColorSequenceLength(r, g, b, background);
        state.Background = background;

        return string.Create(state.ColorLength + message.Length + ForegroundResetSequence.Length, state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.R, state.G, state.B, state.Background);
            ReadOnlySpan<char> reset = state.Background ? BackgroundResetSequence : ForegroundResetSequence;
            reset.CopyTo(span.Slice(span.Length - reset.Length, reset.Length));
            state.Message->CopyTo(span[state.ColorLength..]);
        });
    }

    private unsafe struct WrapMessageWithColorRGBState
    {
        public ReadOnlySpan<char>* Message;
        public byte R;
        public byte G;
        public byte B;
        public int ColorLength;
        public bool Background;
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> value to an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static string GetTerminalColorSequence(ConsoleColor color, bool background = false)
    {
        GetTerminalColorSequence8BitState state = default;
        state.Color = color;
        state.Background = background;
        return string.Create(GetTerminalColorSequenceLength(color, background), state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.Color, state.Background);
        });
    }

    private struct GetTerminalColorSequence8BitState
    {
        public ConsoleColor Color;
        public bool Background;
    }

    /// <summary>
    /// Converts an ARGB value to an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static string GetTerminalColorSequence(int argb, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return GetTerminalColorSequence(color, background);
            }

            return GetTerminalColorSequence((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, background);
        }
    }

    /// <summary>
    /// Converts an RGB value to an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static string GetTerminalColorSequence(byte r, byte g, byte b, bool background = false)
    {
        GetTerminalColorSequenceRGBState state = default;
        state.Background = background;
        state.R = r;
        state.G = g;
        state.B = b;
        return string.Create(GetTerminalColorSequenceLength(r, g, b, background), state, static (span, state) =>
        {
            WriteTerminalColorSequence(span, state.R, state.G, state.B, state.Background);
        });
    }

    private struct GetTerminalColorSequenceRGBState
    {
        public byte R;
        public byte G;
        public byte B;
        public bool Background;
    }

    /// <summary>
    /// Gets the length of a <see cref="ConsoleColor"/> value as an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static int GetTerminalColorSequenceLength(ConsoleColor color, bool background = false)
    {
        return background && color is >= ConsoleColor.DarkGray and <= ConsoleColor.White ? 6 : 5;
    }

    /// <summary>
    /// Gets the length of an ARGB value as an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int GetTerminalColorSequenceLength(int argb, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return GetTerminalColorSequenceLength(color, background);
            }

            byte r = (byte)(argb >> 16), g = (byte)(argb >> 8), b = (byte)argb;
            return GetTerminalColorSequenceLength(r, g, b, background);
        }
    }

    /// <summary>
    /// Gets the length of an RGB value as an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int GetTerminalColorSequenceLength(byte r, byte g, byte b, bool background = false)
    {
        return 10 + (r > 9 ? r > 99 ? 3 : 2 : 1) + (g > 9 ? g > 99 ? 3 : 2 : 1) + (b > 9 ? b > 99 ? 3 : 2 : 1);
    }

    /// <summary>
    /// Gets the number used to start a foreground or background color in an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static int GetTerminalColorSequenceCode(ConsoleColor color, bool background = false)
    {
        ReadOnlySpan<int> colorCodes = [30, 34, 32, 36, 31, 35, 33, 37, 90, 94, 92, 96, 91, 95, 93, 97];
        int num = color is < 0 or > ConsoleColor.White ? 39 : colorCodes[(int)color];
        return background ? num + 10 : num;
    }

    /// <summary>
    /// Converts a <see cref="ConsoleColor"/> value to an 8-bit color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If <paramref name="color"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#text-formatting"/>.</remarks>
    public static int WriteTerminalColorSequence(Span<char> data, ConsoleColor color, bool background = false)
    {
        int num = GetTerminalColorSequenceCode(color, background);

        data[0] = '\u001b';
        data[1] = '[';

        if (num <= 99)
        {
            data[2] = (char)(num / 10 + 48);
            data[3] = (char)(num % 10 + 48);
            data[4] = 'm';
            return 5;
        }

        data[2] = (char)(num / 100 + 48);
        data[3] = (char)(num / 10 % 10 + 48);
        data[4] = (char)(num % 10 + 48);
        data[5] = 'm';
        return 6;
    }

    /// <summary>
    /// Converts an ARGB value to an extended color virtual terminal sequence.
    /// <para>If the alpha bits (high 8 bits) are zero, the color will be interpreted as a <see cref="ConsoleColor"/>.</para>
    /// </summary>
    /// <param name="background">If <paramref name="argb"/> should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int WriteTerminalColorSequence(Span<char> data, int argb, bool background = false)
    {
        unchecked
        {
            if ((byte)(argb >> 24) == 0) // console color
            {
                ConsoleColor color = (ConsoleColor)argb;
                return WriteTerminalColorSequence(data, color, background);
            }

            byte r = (byte)(argb >> 16), g = (byte)(argb >> 8), b = (byte)argb;
            return WriteTerminalColorSequence(data, r, g, b, background);
        }
    }

    /// <summary>
    /// Converts an ARGB value to an extended color virtual terminal sequence.
    /// </summary>
    /// <param name="background">If the color should apply to the background of the text instead of the foreground.</param>
    /// <remarks>See <see href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors"/>.</remarks>
    public static int WriteTerminalColorSequence(Span<char> data, byte r, byte g, byte b, bool background = false)
    {
        // https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#extended-colors
        data[0] = ConsoleEscapeCharacter;
        data[1] = '[';
        data[2] = background ? '4' : '3';
        data[3] = '8';
        data[4] = ';';
        data[5] = '2';
        data[6] = ';';
        int index = 6;
        if (r > 99)
            data[++index] = (char)(r / 100 + 48);
        if (r > 9)
            data[++index] = (char)((r % 100) / 10 + 48);
        data[++index] = (char)(r % 10 + 48);
        data[++index] = ';';
        if (g > 99)
            data[++index] = (char)(g / 100 + 48);
        if (g > 9)
            data[++index] = (char)((g % 100) / 10 + 48);
        data[++index] = (char)(g % 10 + 48);
        data[++index] = ';';
        if (b > 99)
            data[++index] = (char)(b / 100 + 48);
        if (b > 9)
            data[++index] = (char)((b % 100) / 10 + 48);
        data[++index] = (char)(b % 10 + 48);
        data[++index] = 'm';
        return index + 1;
    }
#pragma warning restore CS8500

    /// <summary>
    /// Convert to <see cref="ConsoleColor"/> to an int which will be reinterpreted as ARGB later on. This is done by making the alpha value zero.
    /// </summary>
    public static int ToArgbRepresentation(ConsoleColor color) => (int)color;

    /// <summary>
    /// Convert a <see cref="UnityEngine.Color32"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color32 color)
    {
        if (color.a == 0)
            color.a = byte.MaxValue;

        return color.a << 24 |
               color.r << 16 |
               color.g << 8 |
               color.b;
    }

    /// <summary>
    /// Convert a <see cref="System.Drawing.Color"/> to ARGB data.
    /// </summary>
    public static int ToArgb(Color color)
    {
        return (byte)Math.Min(255, Mathf.RoundToInt(color.a * 255)) << 24 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.r * 255)) << 16 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.g * 255)) << 8 |
               (byte)Math.Min(255, Mathf.RoundToInt(color.b * 255));
    }

    /// <summary>
    /// Get the closest <see cref="ConsoleColor"/> to the given ARGB data.
    /// </summary>
    public static ConsoleColor ToConsoleColor(int argb)
    {
        if ((byte)(argb >> 24) == 0)
        {
            return (ConsoleColor)argb;
        }
        int bits = ((argb >> 16) & byte.MaxValue) > 128 || ((argb >> 8) & byte.MaxValue) > 128 || (argb & byte.MaxValue) > 128 ? 8 : 0;
        if (((argb >> 16) & byte.MaxValue) > 180)
            bits |= 4;
        if (((argb >> 8) & byte.MaxValue) > 180)
            bits |= 2;
        if ((argb & byte.MaxValue) > 180)
            bits |= 1;
        return (ConsoleColor)bits;
    }

    /// <summary>
    /// Get a <see cref="Color"/> estimation of <paramref name="color"/>.
    /// </summary>
    public static Color FromConsoleColor(ConsoleColor color)
    {
        int c = (int)color;
        float r = 0f, g = 0f, b = 0f;
        if ((c & 8) == 8)
        {
            r += 0.5f;
            g += 0.5f;
            b += 0.5f;
        }
        if ((c & 4) == 4)
            r += 0.25f;
        if ((c & 2) == 2)
            g += 0.25f;
        if ((c & 1) == 1)
            b += 0.25f;
        return new Color(r, g, b);
    }

    /// <summary>
    /// Effeciently removes any virtual terminal sequences from a string and returns the result as a copy.
    /// </summary>
    public static unsafe string RemoveVirtualTerminalSequences(ReadOnlySpan<char> orig)
    {
        if (orig.Length < 5)
            return orig.ToString();

        bool found = false;
        int l = orig.Length;
        for (int i = 0; i < l; ++i)
        {
            if (orig[i] == ConsoleEscapeCharacter)
            {
                found = true;
            }
        }

        if (!found)
            return orig.ToString();

        // regex: \u001B\[[\d;]*m

        int outpInd = 0;
        char* outp = stackalloc char[l - 3];
        fixed (char* chars = orig)
        {
            int lastCpy = -1;
            for (int i = 0; i < l - 2; ++i)
            {
                if (l <= i + 3 || chars[i] != ConsoleEscapeCharacter || chars[i + 1] != '[' || !char.IsDigit(chars[i + 2]))
                    continue;

                int st = i;
                int c = i + 3;
                for (; c < l; ++c)
                {
                    if (chars[c] != ';' && !char.IsDigit(chars[c]))
                    {
                        if (chars[c] == 'm')
                            i = c;

                        break;
                    }

                    i = c;
                }

                Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (st - lastCpy - 1) * sizeof(char));
                outpInd += st - lastCpy - 1;
                lastCpy += st - lastCpy + (c - st);
            }
            Buffer.MemoryCopy(chars + lastCpy + 1, outp + outpInd, (l - outpInd) * sizeof(char), (l - lastCpy) * sizeof(char));
            outpInd += l - lastCpy;
        }

        return new string(outp, 0, outpInd - 1);
    }

    /// <summary>
    /// Remove rich text tags from text, and replace &lt;color&gt; and &lt;mark&gt; tags with virtual terminal sequences (depending on the current configuration).
    /// </summary>
    /// <param name="options">Tags to check for and remove.</param>
    /// <param name="argbForeground">Color to reset the foreground to.</param>
    /// <param name="argbBackground">Color to reset the background to.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [Pure]
    public static unsafe string ConvertRichTextToVirtualTerminalSequences(string str, StackColorFormatType format, int index = 0, int length = -1, RemoveRichTextOptions options = RemoveRichTextOptions.All, int argbForeground = DefaultForeground, int argbBackground = DefaultBackground)
    {
        FormattingUtility.CheckTags();
        if (index >= str.Length || index < 0)
        {
            if (index == 0 && length <= 0)
                return string.Empty;

            throw new ArgumentOutOfRangeException(nameof(index));
        }
        if (length < 0)
            length = str.Length - index;
        else if (index + length > str.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        else if (length == 0)
            return str;

        const int defaultForegroundStackSize = 2;

        char[] rtn = new char[str.Length + 16];
        int foregroundStackLength = defaultForegroundStackSize;
        int backgroundStackLength = 0;
        Color32* foregroundStack = stackalloc Color32[defaultForegroundStackSize];
        Color32* backgroundStack = null;
        int foregroundStackValuesLength = 0;
        int backgroundStackValuesLength = 0;
        int nextCopyStartIndex = 0;
        int writeIndex = 0;
        bool useColor = format is StackColorFormatType.ExtendedANSIColor or StackColorFormatType.ANSIColor;

        int nonDefaults = 1 | 2;
        if (useColor)
            AppendDefaults(nonDefaults, ref writeIndex, ref rtn, argbBackground, argbForeground, format);

        fixed (char* mainPtr = str)
        {
            char* ptr = mainPtr + index;
            for (int i = 0; i < length; ++i)
            {
                char current = ptr[i];
                if (current != '<')
                    continue;

                bool pushColor = false;
                bool background = false;
                bool isEndTag = i != length - 1 && ptr[i + 1] == '/';
                int endIndex = -1;
                for (int j = i + (isEndTag ? 2 : 1); j < length; ++j)
                {
                    if (ptr[j] != '>')
                        continue;

                    endIndex = j;
                    break;
                }

                if (endIndex == -1)
                    continue;

                if (!isEndTag && useColor)
                {
                    int colorIndex = -1;
                    int colorLength = 0;
                    // <color=#etc>
                    if (ptr[i + 1] is 'c' or 'C' && i + 7 <= endIndex && (options & RemoveRichTextOptions.Color) != 0 &&
                        ptr[i + 2] is 'o' or 'O' &&
                        ptr[i + 3] is 'l' or 'L' &&
                        ptr[i + 4] is 'o' or 'O' &&
                        ptr[i + 5] is 'r' or 'R' &&
                        ptr[i + 6] == '=')
                    {
                        colorIndex = i + 7;
                        colorLength = endIndex - (i + 7);
                    }
                    else if (ptr[i + 1] == '#' && (options & RemoveRichTextOptions.Color) != 0 && i + 2 <= endIndex)
                    {
                        colorIndex = i + 1;
                        colorLength = endIndex - (i + 1);
                    }
                    else if ((options & RemoveRichTextOptions.Mark) != 0 &&
                             ptr[i + 1] is 'm' or 'M' && i + 6 <= endIndex &&
                             ptr[i + 2] is 'a' or 'A' &&
                             ptr[i + 3] is 'r' or 'R' &&
                             ptr[i + 4] is 'k' or 'K' &&
                             ptr[i + 5] == '=')
                    {
                        colorIndex = i + 6;
                        colorLength = endIndex - (i + 6);
                        background = true;
                    }
                    else if (!FormattingUtility.CompareRichTextTag(ptr, endIndex, i, options))
                        continue;

                    if (colorIndex >= 0 && HexStringHelper.TryParseColor32(new ReadOnlySpan<char>(ptr + colorIndex, colorLength), CultureInfo.InvariantCulture, out Color32 color))
                    {
                        pushColor = true;
                        if (!background)
                        {
                            if (foregroundStackValuesLength >= foregroundStackLength)
                            {
                                // ReSharper disable once StackAllocInsideLoop (won't happen much)
                                Color32* newAlloc = stackalloc Color32[foregroundStackValuesLength + 1];
                                if (foregroundStackValuesLength > 0)
                                    Buffer.MemoryCopy(foregroundStack, newAlloc, foregroundStackValuesLength * sizeof(Color32), foregroundStackValuesLength * sizeof(Color32));
                                foregroundStackLength = foregroundStackValuesLength + 1;
                                foregroundStack = newAlloc;
                            }

                            foregroundStack[foregroundStackValuesLength] = color;
                            ++foregroundStackValuesLength;
                        }
                        else
                        {
                            if (backgroundStackValuesLength >= backgroundStackLength)
                            {
                                // ReSharper disable once StackAllocInsideLoop (won't happen much)
                                Color32* newAlloc = stackalloc Color32[backgroundStackLength + 1];
                                if (backgroundStackValuesLength > 0)
                                    Buffer.MemoryCopy(backgroundStack, newAlloc, backgroundStackValuesLength * sizeof(Color32), backgroundStackValuesLength * sizeof(Color32));
                                backgroundStackLength = backgroundStackValuesLength + 1;
                                backgroundStack = newAlloc;
                            }

                            backgroundStack![backgroundStackValuesLength] = color;
                            ++backgroundStackValuesLength;
                        }
                    }
                }
                else if (useColor && (options & RemoveRichTextOptions.Color) != 0 &&
                         ptr[i + 2] is 'c' or 'C' &&
                         ptr[i + 3] is 'o' or 'O' &&
                         ptr[i + 4] is 'l' or 'L' &&
                         ptr[i + 5] is 'o' or 'O' &&
                         ptr[i + 6] is 'r' or 'R')
                {
                    if (foregroundStackValuesLength > 0)
                        --foregroundStackValuesLength;
                    pushColor = true;
                }
                else if (useColor && (options & RemoveRichTextOptions.Mark) != 0 &&
                         ptr[i + 2] is 'm' or 'M' &&
                         ptr[i + 3] is 'a' or 'A' &&
                         ptr[i + 4] is 'r' or 'R' &&
                         ptr[i + 5] is 'k' or 'K')
                {
                    if (backgroundStackValuesLength > 0)
                        --backgroundStackValuesLength;
                    pushColor = true;
                    background = true;
                }
                else if (!FormattingUtility.CompareRichTextTag(ptr, endIndex, i, options))
                    continue;

                Append(ref rtn, new ReadOnlySpan<char>(ptr + nextCopyStartIndex, i - nextCopyStartIndex), writeIndex);
                writeIndex += i - nextCopyStartIndex;
                nextCopyStartIndex = endIndex + 1;
                i = endIndex;
                if (pushColor)
                {
                    int len = background ? backgroundStackValuesLength : foregroundStackValuesLength;
                    if (len > 0)
                    {
                        Color32* nextColor = (background ? backgroundStack : foregroundStack) + (len - 1);

                        if (format == StackColorFormatType.ExtendedANSIColor)
                            writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, nextColor->r, nextColor->g, nextColor->b, background);
                        else
                            writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, ToConsoleColor(ToArgb(*nextColor)), background);
                        nonDefaults |= background ? 2 : 1;
                    }
                    else
                    {
                        AppendDefaults(nonDefaults, ref writeIndex, ref rtn, argbBackground, argbForeground, format);
                    }
                }
            }
            Append(ref rtn, new ReadOnlySpan<char>(ptr + nextCopyStartIndex, str.Length - nextCopyStartIndex), writeIndex);
            writeIndex += str.Length - nextCopyStartIndex;
            if (useColor)
                AppendDefaults(nonDefaults, ref writeIndex, ref rtn, argbBackground, argbForeground, format);
        }

        return new string(rtn, 0, writeIndex);

        static void AppendDefaults(int nonDefaults, ref int writeIndex, ref char[] rtn, int argbBackground, int argbForeground, StackColorFormatType format)
        {
            if ((nonDefaults & 2) != 0)
            {
                if (argbBackground == DefaultBackground)
                {
                    Append(ref rtn, BackgroundResetSequence.AsSpan(), writeIndex);
                    writeIndex += BackgroundResetSequence.Length;
                }
                else if (format == StackColorFormatType.ExtendedANSIColor && (byte)(argbBackground >> 24) != 0)
                    writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, (byte)(argbBackground >> 16), (byte)(argbBackground >> 8), (byte)argbBackground, true);
                else
                {
                    ConsoleColor consoleColor = ToConsoleColor(argbBackground);
                    if (consoleColor == ConsoleColor.Black)
                    {
                        Append(ref rtn, BackgroundResetSequence.AsSpan(), writeIndex);
                        writeIndex += BackgroundResetSequence.Length;
                    }
                    else
                        writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, consoleColor, true);
                }
            }

            if ((nonDefaults & 1) != 0)
            {
                if (argbForeground == DefaultForeground)
                {
                    Append(ref rtn, ForegroundResetSequence.AsSpan(), writeIndex);
                    writeIndex += ForegroundResetSequence.Length;
                }
                else if (format == StackColorFormatType.ExtendedANSIColor && (byte)(argbForeground >> 24) != 0)
                    writeIndex += AppendExtANSIForegroundCode(ref rtn, writeIndex, (byte)(argbForeground >> 16), (byte)(argbForeground >> 8), (byte)argbForeground, false);
                else
                {
                    ConsoleColor consoleColor = ToConsoleColor(argbForeground);
                    if (consoleColor == ConsoleColor.Gray)
                    {
                        Append(ref rtn, ForegroundResetSequence.AsSpan(), writeIndex);
                        writeIndex += ForegroundResetSequence.Length;
                    }
                    else
                        writeIndex += AppendANSIForegroundCode(ref rtn, writeIndex, consoleColor, false);
                }
            }

            nonDefaults = 0;
        }
    }

    private static void Append(ref char[] arr, ReadOnlySpan<char> data, int index)
    {
        if (data.Length == 0) return;

        if (index + data.Length > arr.Length)
        {
            char[] old = arr;
            arr = new char[index + data.Length];
            Buffer.BlockCopy(old, 0, arr, 0, old.Length * sizeof(char));
        }

        data.CopyTo(arr.AsSpan(index));
    }

    private static int AppendANSIForegroundCode(ref char[] data, int index, ConsoleColor color, bool background)
    {
        int len = background && (int)color >> 2 != 0 ? 6 : 5;
        Span<char> ptr = stackalloc char[len];
        WriteTerminalColorSequence(ptr, color, background);
        Append(ref data, ptr, index);
        return len;
    }

    private static int AppendExtANSIForegroundCode(ref char[] data, int index, byte r, byte g, byte b, bool background)
    {
        int l = 10 + GetTerminalColorSequenceLength(r, g, b, background);
        Span<char> ptr = stackalloc char[l];
        WriteTerminalColorSequence(ptr, r, g, b, background);
        Append(ref data, ptr, index);
        return l;
    }
}
/// <summary>
/// Describes the color formatting behavior of <see cref="StackTraceCleaner"/>
/// </summary>
public enum StackColorFormatType
{
    /// <summary>
    /// No color formatting, just raw text.
    /// </summary>
    None,
    /// <summary>
    /// Sets the <see cref="Console.ForegroundColor"/> for each section. Only applicable when printed to console.
    /// </summary>
    ConsoleColor,
    /// <summary>
    /// UnityEngine rich text tags.<br/>
    /// <seealso href="https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/StyledText.html"/>
    /// </summary>
    UnityRichText,
    /// <summary>
    /// TextMeshPro rich text tags.<br/>
    /// <seealso href="http://digitalnativestudios.com/textmeshpro/docs/rich-text/"/>
    /// </summary>
    TextMeshProRichText,
    /// <summary>
    /// ANSI Text formatting codes.<br/>
    /// <seealso href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN#text-formatting"/>
    /// </summary>
    ANSIColor,
    /// <summary>
    /// Will not work on all terminals.<br/>
    /// <seealso href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN#extended-colors"/>
    /// </summary>
    ExtendedANSIColor,
    /// <summary>
    /// Text is colored with &lt;span&gt; tags.
    /// </summary>
    /// <remarks>Use classes instead of constant CSS styles by setting <see cref="StackCleanerConfiguration.HtmlUseClassNames"/> to <see langword="true"/>.</remarks>
    Html,
    /// <summary>
    /// ANSI Text formatting codes without bright colors (3-bit).<br/>
    /// <seealso href="https://learn.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences?redirectedfrom=MSDN#text-formatting"/>
    /// </summary>
    ANSIColorNoBright
}
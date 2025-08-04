using System;
using System.Collections.Generic;
using System.Linq;
using PeasyMotion;

public class JumpLabelAlgorithm
{
    private readonly string   allowedKeys;
    private readonly JumpMode jumpMode;
    private readonly int      caretPositionSensivity;
    private readonly string   nCharSearchJumpKeys;
    private const    int      MinimumDistanceBetweenLabels = 3;
    private readonly bool     vimOrBulkyCaretPresent;

    public JumpLabelAlgorithm(string allowedKeys, JumpMode jumpMode, int caretPositionSensivity,
                              string nCharSearchJumpKeys    = null,
                              bool   vimOrBulkyCaretPresent = true)
    {
        this.allowedKeys            = allowedKeys;
        this.jumpMode               = jumpMode;
        this.caretPositionSensivity = caretPositionSensivity;
        this.nCharSearchJumpKeys    = nCharSearchJumpKeys;
        this.vimOrBulkyCaretPresent = vimOrBulkyCaretPresent;
    }

    public List<JumpTarget> CollectJumpTargets(string text, int cursorPosition, int startPosition, int endPosition)
    {
        var context = new JumpTargetContext
        {
            JumpTargets  = new List<JumpTarget>(),
            LastJumpPos  = -100,
            EolWindows   = false,
            EolCharCount = 0
        };

        int adjustedCursor = AdjustCursorPosition(cursorPosition);
        ProcessTextRange(text, startPosition, endPosition, adjustedCursor, context);

        return context.JumpTargets;
    }

    private int AdjustCursorPosition(int cursorPosition)
    {
        if (caretPositionSensivity <= 0) return cursorPosition;
        int dc = caretPositionSensivity + 1;
        return (cursorPosition / dc) * dc + (dc / 2);
    }

    private void ProcessTextRange(string            text, int startPosition, int endPosition, int adjustedCursor,
                                  JumpTargetContext context)
    {
        bool prevIsLetterOrDigit = false, prevIsControl = false, prevNewLine = false;

        for (int i = startPosition; i <= endPosition && i < text.Length; i++)
        {
            var chars = GetCharContext(text, i, endPosition);
            var (isEol, eolWindows, eolCharCount) =
                chars.Current.IsEOL(chars.Previous, chars.Next, context.EolWindows, context.EolCharCount);
            context.EolWindows   = eolWindows;
            context.EolCharCount = eolCharCount;

            int jumpPosModifier = jumpMode == JumpMode.LineBeginingJump ? 1 : 0;
            if (jumpMode == JumpMode.LineBeginingJump && (i == startPosition || isEol))
            {
                jumpPosModifier = FindNextNonEmptyPosition(text, i + 1, endPosition, context) - i;
            }

            bool isCandidate = IsCandidateLabel(chars, prevIsLetterOrDigit, isEol, i, startPosition, text);
            bool isValidDistance = (context.LastJumpPos + jumpPosModifier + MinimumDistanceBetweenLabels) < i ||
                                   (prevNewLine && isEol);

            if (isCandidate && isValidDistance && i < endPosition)
            {
                int jumpPos = Math.Min(i + jumpPosModifier, endPosition);
                context.JumpTargets.Add(new JumpTarget(
                                                       position: jumpPos,
                                                       text: chars.Current.ToString(),
                                                       distanceToCursor: Math.Abs(jumpPos - adjustedCursor),
                                                       metadata: null
                                                      ));
                context.LastJumpPos = isEol ? -100 : jumpPos;
            }

            prevIsLetterOrDigit = char.IsLetterOrDigit(chars.Current);
            prevIsControl       = char.IsControl(chars.Current);
            prevNewLine         = isEol;
        }
    }

    private (char Current, char Previous, char Next) GetCharContext(string text, int index, int endPosition)
    {
        return (
            Current: text[index],
            Previous: index > 0 ? text[index                                  - 1] : '\0',
            Next: index < text.Length - 1 && index < endPosition ? text[index + 1] : '\0'
        );
    }

    private int FindNextNonEmptyPosition(string text, int start, int endPosition, JumpTargetContext context)
    {
        for (int j = start; j <= endPosition && j < text.Length; j++)
        {
            var ch     = text[j];
            var nextCh = j < text.Length - 1 ? text[j + 1] : '\0';
            var (isEol, eolWindows, eolCharCount) = ch.IsEOL(ch, nextCh, context.EolWindows, context.EolCharCount);
            context.EolWindows                    = eolWindows;
            context.EolCharCount                  = eolCharCount;
            if (ch != ' ' && !isEol) return j;
            if (isEol) break;
        }

        return start;
    }

    private bool IsCandidateLabel((char Current, char Previous, char Next) chars, bool prevIsLetterOrDigit,
                                  bool newLine, int index, int startPosition, string text)
    {
        bool curIsLetterOrDigit = char.IsLetterOrDigit(chars.Current);
        switch (jumpMode)
        {
            case JumpMode.LineJumpToWordBegining:
                return curIsLetterOrDigit && !prevIsLetterOrDigit;
            case JumpMode.LineJumpToWordEnding:
                return vimOrBulkyCaretPresent
                    ? curIsLetterOrDigit  && !char.IsLetterOrDigit(chars.Next)
                    : prevIsLetterOrDigit && !curIsLetterOrDigit;
            case JumpMode.LineBeginingJump:
                return (index == startPosition || newLine) && index < text.Length;
            case JumpMode.TwoCharJump:
                return index                                < text.Length - 1         &&
                       char.ToLowerInvariant(chars.Current) == nCharSearchJumpKeys[0] &&
                       char.ToLowerInvariant(chars.Next)    == nCharSearchJumpKeys[1];
            case JumpMode.OneCharJump:
                return char.ToLowerInvariant(chars.Current) == nCharSearchJumpKeys[0];
            default:
                return (curIsLetterOrDigit              && !prevIsLetterOrDigit) ||
                       (char.IsControl(chars.Previous)  && newLine)              ||
                       (!char.IsControl(chars.Previous) && !prevIsLetterOrDigit && newLine);
        }
    }

    private class JumpTargetContext
    {
        public List<JumpTarget> JumpTargets  { get; set; }
        public int              LastJumpPos  { get; set; }
        public bool             EolWindows   { get; set; }
        public int              EolCharCount { get; set; }
    }

    public List<JumpLabel> AssignLabels(List<JumpTarget> targets)
    {
        var sortedTargets = targets.OrderBy(t => t.DistanceToCursor).ToList();
        var labels        = new List<JumpLabel>();
        ComputeGroups(0, sortedTargets.Count - 1, allowedKeys, "", sortedTargets, labels);
        return labels;
    }

    private void ComputeGroups(int startIndex, int endIndex, string keys, string prefix, List<JumpTarget> targets,
                               List<JumpLabel> labels)
    {
        var wordCount = endIndex - startIndex + 1;
        if (wordCount <= 0) return;

        var keyCounts     = new int[keys.Length];
        var keyCountsKeys = new Dictionary<char, int>();
        for (var j = 0; j < keys.Length; j++)
        {
            keyCounts[j]           = 0;
            keyCountsKeys[keys[j]] = j;
        }

        var targetsLeft = wordCount;
        var level       = 0;
        while (targetsLeft > 0)
        {
            var childrenCount = level == 0 ? 1 : keys.Length - 1;
            foreach (var key in keys)
            {
                keyCounts[keyCountsKeys[key]] += childrenCount;
                targetsLeft                   -= childrenCount;
                if (targetsLeft <= 0)
                {
                    keyCounts[keyCountsKeys[key]] += targetsLeft;
                    break;
                }
            }

            level++;
        }

        var k = 0;
        Array.Reverse(keyCounts);
        for (var i = 0; i < keys.Length; i++)
        {
            var keyCount = keyCounts[i];
            if (keyCount > 1)
            {
                ComputeGroups(startIndex + k, startIndex + k + keyCount - 1, Reverse(keys), prefix + keys[i], targets,
                              labels);
            }
            else if (keyCount == 1)
            {
                labels.Add(new JumpLabel(targets[startIndex + k], prefix + keys[i]));
            }

            k += keyCount;
        }
    }

    public List<JumpLabel> FilterLabels(List<JumpLabel> labels, string input) =>
        labels.Where(l => l.Label.StartsWith(input, StringComparison.InvariantCulture)).ToList();

    private static string Reverse(string s)
    {
        var charArray = s.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }
}
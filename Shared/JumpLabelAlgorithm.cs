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
        var  jumpTargets         = new List<JumpTarget>();
        var  lastJumpPos         = -100;
        var  prevIsLetterOrDigit = false;
        var  prevIsControl       = false;
        var  prevNewLine         = false;
        bool EOL_Windows         = false;
        int  EOL_charCount       = 0;

        for (var i = startPosition; i <= endPosition && i < text.Length; i++)
        {
            var ch                 = text[i];
            var nextCh             = i < text.Length - 1 ? text[i + 1] : '\0';
            var prevCh             = i > 0 ? text[i               - 1] : '\0';
            var curIsLetterOrDigit = char.IsLetterOrDigit(ch);
            var curIsControl       = char.IsControl(ch);
            var nextIsControl      = char.IsControl(nextCh);
            var candidateLabel     = false;
            int jumpPosModifier    = jumpMode == JumpMode.LineBeginingJump ? 1 : 0;

            // EOL detection
            if (EOL_charCount == 0 && curIsControl)
            {
                if (prevCh == '\r' && ch == '\n')
                {
                    EOL_charCount = 2;
                    EOL_Windows   = true;
                }
                else if (ch == '\r' && nextCh == '\n')
                {
                    EOL_charCount = 2;
                    EOL_Windows   = true;
                }
                else if (ch == '\r' && !prevIsControl && (nextCh == '\r' || !nextIsControl))
                {
                    EOL_charCount = 1;
                }
                else if (ch == '\n' && !prevIsControl && (nextCh == '\n' || !nextIsControl))
                {
                    EOL_charCount = 1;
                }
            }

            bool newLine = (EOL_Windows  && prevCh == '\r' && ch == '\n') ||
                           (!EOL_Windows && (ch == '\n' || ch == '\r'));

            switch (jumpMode)
            {
                case JumpMode.LineJumpToWordBegining:
                    candidateLabel = curIsLetterOrDigit && !prevIsLetterOrDigit;
                    break;
                case JumpMode.LineJumpToWordEnding:
                    if (vimOrBulkyCaretPresent)
                    {
                        candidateLabel = curIsLetterOrDigit && !char.IsLetterOrDigit(nextCh);
                    }
                    else
                    {
                        candidateLabel = prevIsLetterOrDigit && !curIsLetterOrDigit;
                    }

                    break;
                case JumpMode.LineBeginingJump:
                {
                    bool isLineStart = i == startPosition || newLine;
                    candidateLabel  = isLineStart && i < endPosition;
                    jumpPosModifier = 0;
                    if (isLineStart)
                    {
                        int j = i + 1;
                        while (j <= endPosition && j < text.Length)
                        {
                            var ch_j     = text[j];
                            var nextCh_j = j < text.Length - 1 ? text[j + 1] : '\0';
                            bool isEOL = (EOL_Windows  && ch_j == '\r' && nextCh_j == '\n') ||
                                         (!EOL_Windows && (ch_j == '\n' || ch_j == '\r'));

                            if (ch_j != ' ' && !isEOL)
                            {
                                jumpPosModifier = j - i;
                                break;
                            }
                            else if (isEOL)
                            {
                                break;
                            }

                            j++;
                        }
                    }
                }
                    break;
                case JumpMode.TwoCharJump:
                    candidateLabel = i                             < text.Length - 1         &&
                                     char.ToLowerInvariant(ch)     == nCharSearchJumpKeys[0] &&
                                     char.ToLowerInvariant(nextCh) == nCharSearchJumpKeys[1];
                    break;
                case JumpMode.OneCharJump:
                    candidateLabel = char.ToLowerInvariant(ch) == nCharSearchJumpKeys[0];
                    break;
                default:
                    candidateLabel = curIsLetterOrDigit && !prevIsLetterOrDigit;
                    break;
            }

            bool distanceToPrevLabelAcceptable = (lastJumpPos + jumpPosModifier + MinimumDistanceBetweenLabels) < i ||
                                                 (prevNewLine && newLine);

            if (candidateLabel && distanceToPrevLabelAcceptable && i < endPosition)
            {
                var adjustedCursor = cursorPosition;
                if (caretPositionSensivity > 0)
                {
                    var dc = caretPositionSensivity + 1;
                    adjustedCursor = (cursorPosition / dc) * dc + (dc / 2);
                }

                var jumpPosModified = (i + jumpPosModifier) < endPosition ? (i + jumpPosModifier) : i;
                jumpTargets.Add(new JumpTarget(
                                               position: jumpPosModified,
                                               text: ch.ToString(),
                                               distanceToCursor: Math.Abs(jumpPosModified - adjustedCursor),
                                               metadata: null
                                              ));
                lastJumpPos = newLine ? -100 : jumpPosModified;
            }

            prevIsLetterOrDigit = curIsLetterOrDigit;
            prevIsControl       = curIsControl;
            prevNewLine         = newLine;
        }

        return jumpTargets;
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
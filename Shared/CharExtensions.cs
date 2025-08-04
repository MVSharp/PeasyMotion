public static class CharExtensions
{
    // EOL convention reminder | Windows = CR LF \r\n | Unix = LF \n | Mac = CR \r
    private const char CR = '\r';
    private const char LF = '\n';
    public static bool IsEOL(this char c) => (c == CR && c == LF) || c == '\n' || c == '\r';

    public static (bool IsEol, bool EolWindows, int EolCharCount) IsEOL(this char ch,         char prevCh, char nextCh,
                                                                        bool      eolWindows, int  eolCharCount)
    {
        if (eolCharCount == 0 && char.IsControl(ch))
        {
            if (prevCh == '\r' && ch == '\n')
            {
                eolCharCount = 2;
                eolWindows   = true;
            }
            else if (ch == '\r' && nextCh == '\n')
            {
                eolCharCount = 2;
                eolWindows   = true;
            }
            else if (ch == '\r' && !char.IsControl(prevCh) && (nextCh == '\r' || !char.IsControl(nextCh)))
            {
                eolCharCount = 1;
            }
            else if (ch == '\n' && !char.IsControl(prevCh) && (nextCh == '\n' || !char.IsControl(nextCh)))
            {
                eolCharCount = 1;
            }
        }

        bool isEol = (eolWindows && prevCh == '\r' && ch == '\n') || (!eolWindows && (ch == '\n' || ch == '\r'));
        return (isEol, eolWindows, eolCharCount);
    }
}
// AT:   Elipese
// Date: 2023/2/12

using System.Diagnostics;
using System.Text;

namespace GScript.Analyzer.Util;

public enum SplitTokenType
{
    Content,
    SplitSeparator
}
public readonly struct SplitToken
{
    public readonly string TokenString { get; }
    public readonly SplitTokenType Type { get; }

    public SplitToken(string token, SplitTokenType type)
    {
        TokenString = token;
        Type = type;
    }
}
public class StringSplit
{
    List<SplitToken> m_tokens;

    string m_rawstring;
    char m_split;

    public string[] SplitUnit => m_tokens.Where(x => x.Type == SplitTokenType.Content).Select(x => x.TokenString).ToArray();

    public IReadOnlyList<SplitToken> Tokens => m_tokens.AsReadOnly();

    public StringSplit(string str, char splitSeparator, bool strictParenthesisType = true)
    {
        m_rawstring = str;
        m_split = splitSeparator;

        Stack<ParenthesisType> pars = new();

        List<SplitToken> units = [];
        StringBuilder sb = new();

        bool inSeparator = false;
        foreach (char c in str)
        {
            if (c != m_split && inSeparator)
            {
                inSeparator = false;
                units.Add(new SplitToken(sb.ToString(), SplitTokenType.SplitSeparator));
                sb.Clear();
            }
            if (StrParenthesis.GetCharHalfParenthesisType(c) is ParenthesisType t && !t.HasFlag(ParenthesisType.Unknown))
            {
                if(pars.TryPeek(out var peek) && (!strictParenthesisType || ((peek ^ ParenthesisType.Left) == (t ^ ParenthesisType.Right))))
                {
                    pars.Pop();
                }
                else
                {
                    pars.Push(t);
                }

                sb.Append(c);
            }
            else if (c == m_split)
            {
                if(pars.Count > 0)
                {
                    sb.Append(c);
                }
                else
                {
                    if(!inSeparator)
                    {
                        units.Add(new(sb.ToString(), SplitTokenType.Content));
                        sb.Clear();
                        inSeparator = true;
                    }
                    sb.Append(c);
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if(sb.Length > 0)
        {
            units.Add(new(sb.ToString(), sb[0] == m_split ? SplitTokenType.SplitSeparator : SplitTokenType.Content));
        }

        m_tokens = units;
    }

    public string this[int index]
    {
        get
        {
            try
            {
                return SplitUnit[index];
            }
            catch
            {
                return "";
            }
        }
    }

    public SplitToken this[long index]
    {
        get
        {
            try
            {
                return m_tokens[(int)index];
            }
            catch
            {
                return default;
            }
        }
    }

    public string[] this[Range range]
    {
        get
        {
            return SplitUnit[range];
        }
    }
}

public class StringSplitEx
{
    List<string> m_splitunits = new();
    List<SplitToken> m_tokens;
    public List<string> SplitUnit => m_splitunits;
    public StringSplitEx(string str, char splitchar, int depth)
    {
        int count = 0;
        int index = 0;
        StringBuilder current = new();
        foreach(char c in str)
        {
            if(c != splitchar)
            {
                current.Append(c);
            }
            else
            {
                count++;
                bool canbr = false;
                if (count == depth)
                {
                    current.Append(splitchar + str[(index + 1)..]);
                    canbr = true;
                }
                m_splitunits.Add(current.ToString());
                current.Clear();
                if (canbr)
                    break;
            }
            index++;
        }
        if(count < depth)
            m_splitunits.Add(current.ToString());
    }

    public string this[int index]
    {
        get
        {
            return m_splitunits[index];
        }
    }

    public string[] this[Range range]
    {
        get
        {
            return m_splitunits.ToArray()[range];
        }
    }
}

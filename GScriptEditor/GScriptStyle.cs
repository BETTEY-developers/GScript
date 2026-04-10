using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GScript.Editor;

internal struct ColorSet
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }

    public override int GetHashCode()
    {
        return ~(R+1) * ~(G<<2+1) * ~(B<<4+1);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj.GetHashCode() == GetHashCode();
    }

    public static implicit operator Color(ColorSet set)
    {
        // Clac. HashCode R: -1; G: -1; B: -1;
        const int NoneColorSetHashCode = -217;
        if (set.GetHashCode() == NoneColorSetHashCode)
            return Color.Black;
        return Color.FromArgb(set.R, set.G, set.B);
    }
}

internal class ColorStyle
{
    public ColorSet ForegroundColor { get; set; }
    public ColorSet BackgroundColor { get; set; }
}

internal class ColorStyleEx
{
    public Regex Rule { get; set; }
    public ColorSet ForegroundColor { get; set; }
    public ColorSet BackgroundColor { get; set; }
}

internal class StyleTable
{
    public string InterpreterPath { get; set; }
    public Dictionary<KeyType, ColorStyle> ColorStyle { get; set; } = new ();
    //public Dictionary<string, ColorStyleEx> CustomConstantColorRule { get; set; }

    public List<NormalKeyUnit> CritialVariable { get; set; } = new();
    public List<NormalKeyUnit> Control { get; set; } = new();
    public List<KnownTypeKeyUnit> KnownType { get; set; } = new();
    public List<NormalKeyUnit> Operator { get; set; } = new();
    public List<NormalKeyUnit> Tag { get; set; } = new();
    public List<NormalKeyUnit> Definition { get; set; } = new();
    public List<NormalKeyUnit> Special { get; set; } = new();

    public List<KeyUnit> GetList()
    {
        var list = new List<KeyUnit>();
        CritialVariable.ForEach(x => list.Add(new() { ConstantType = null, RawString = x.RawString, Type = x.Type }));
        Control.ForEach(x => list.Add(new() { ConstantType = null, RawString = x.RawString, Type = x.Type }));
        Operator.ForEach(x => list.Add(new() { ConstantType = null, RawString = x.RawString, Type = x.Type }));
        Tag.ForEach(x => list.Add(new() { ConstantType = null, RawString = x.RawString, Type = x.Type }));
        Definition.ForEach(x => list.Add(new() { ConstantType = null, RawString = x.RawString, Type = x.Type }));
        Special.ForEach(x => list.Add(new() { ConstantType = null, RawString = x.RawString, Type = x.Type }));
        // 'flag' type always analyzed as a known type, so we add it here.
        return [..list.Concat(KnownType.Concat([new() { RawString = "flag", Type = KeyType.KnownType}]).Select(x => (KeyUnit)x))];
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GScript.Editor;

internal struct KeyUnit
{
    public KeyType Type { get; set; }
    public string RawString { get; set; }
    public KeyType? ConstantType { get; set; }
}

internal struct KnownTypeKeyUnit
{
    public KeyType Type { get; set; }
    public string RawString { get; set; }
    public KeyType ConstantType { get; set; }

    public static explicit operator KeyUnit(KnownTypeKeyUnit unit)
    {
        return new KeyUnit
        {
            Type = unit.Type,
            RawString = unit.RawString,
            ConstantType = unit.ConstantType
        };
    }
}

internal struct NormalKeyUnit
{
    public KeyType Type { get; set; }
    public string RawString { get; set; }
}
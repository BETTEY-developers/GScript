using EUtility.ValueEx;
using GScript.Analyzer;
using GScript.Analyzer.Exception;
using GScript.Analyzer.Executing;
using GScript.Analyzer.InternalType;
using GScript.Analyzer.Parser;
using GScript.Analyzer.Util;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Transactions;

namespace GScript.Standard;
using Arguments = ICollection<ScriptObject>;

internal class Entry
{
    public const int GSBE_INLINEFUNCINVAILD = 0x1000;
    public const int GSBE_FUNCISNTEXISTS = 0x1001;
    public const int GSBE_INVAILDCRITIALVARIABLEOPERATOR = 0x2001;
    public const int GSBE_VARIABLEISEXISTS = 0x2002;
    public const int GSBE_VARIABLEISNTEXISTS = 0x2003;
    public const int GSBE_STACKPOINTEROUTOFRANGE = 0x8000;


    public const string M_CallRet = "$RET$";
    public const string M_Argument1 = "$ARG$";
    public const string M_Argument2 = "$ARG1$";
    public const string M_Argument3 = "$ARG2$";
    public const string M_Argument4 = "$ARG3$";
    public const string M_Argument5 = "$ARG4$";
    public const string M_Argument6 = "$ARG5$";
    public const string M_Argument7 = "$ARG6$";
    public const string M_Argument8 = "$ARG7$";
    public const string M_SelfObject = "$SELF$";
    public const string M_ValueStackTop = "$VST$";
    public const string M_ValueStackPointer = "$VSP$";
    public const string M_CommandResult = "$CRET$";


    public Entry(string path)
    {
        _script = new();

        InitializeCommands(in _script);
        InitialzeParsers(in _script);

        _script.OpenWithContent(path);

        _script.Vars.Add(M_CallRet, new("$RET$"));

        _script.Vars.Add(M_Argument1, new("$ARG$"));
        _script.Vars.Add(M_Argument2, new("$ARG1$"));
        _script.Vars.Add(M_Argument3, new("$ARG2$"));
        _script.Vars.Add(M_Argument4, new("$ARG3$"));
        _script.Vars.Add(M_Argument5, new("$ARG4$"));
        _script.Vars.Add(M_Argument6, new("$ARG5$"));
        _script.Vars.Add(M_Argument7, new("$ARG6$"));
        _script.Vars.Add(M_Argument8, new("$ARG7$"));
        _script.Vars.Add(M_SelfObject, new(M_SelfObject));
        _script.Vars.Add(M_ValueStackTop, new(M_ValueStackTop));
        _script.Vars.Add(M_ValueStackPointer, new(M_ValueStackPointer));
        _script.Vars.Add(M_CommandResult, new(M_CommandResult));

        for (int i = 0; i < 1024 * 8; i++)
        {
            _valueStack.Add(new());
        }

    Execute:

        var result = _script.Execute();

        if (!result)
        {
            Console.WriteLine(ExceptionOperator.GetLastError());
            Console.WriteLine(ExceptionOperator.GetException());
            Console.WriteLine(ExceptionOperator.GetErrorData().Message);
            Console.WriteLine("unhandle exception");
        }

        if (!_funcTable.ContainsKey("main"))
        {
            ExceptionOperator.SetLastErrorEx(new(0, "", new InvaildScriptSegmentException(), "No entry."));
            return;
        }

        _noRun = false;
        _callStack.Push(_funcTable["main"].Start.Value);
        _script.CurrentLine = _funcTable["main"].Start.Value;
        goto Execute;
    }

    private Script _script;

    private Dictionary<string, Range> _funcTable = new();

    private bool _inFuncBlock = false;

    private bool _inClassBlock = false;

    private (string, Range) _currentFunc = new();

    private ClassTemplate _currentClass = null;

    private Stack<int> _callStack = new(1024 * 8);

    private List<object> _valueStack = new(1024 * 8);

    private Dictionary<Tag, int> _tags = new();

    private Dictionary<string, ClassTemplate> _classTemplateTable = new();

    private bool _noRun = true;

    private string[] _alwaysRun = new string[]
    {
        "func",
        "funcEnd",
        "class",
        "classEnd",
        "prop",
        "MFunc",
        "MFuncEnd",
        "ctor",
        "ctorEnd",
        "tag"
    };

    private string[] _attributes = new string[]
    {
        "NoRun"
    };

    private void InitializeCommands(in Script script)
    {
        ExecuteResult OutCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            var cmd = context.Command;

            var arg = context.Command.Args;

            if (arg.Count == 1)
            {
                switch (context.Command.TypeArgPairs[arg[0]])
                {
                    case GScript.Analyzer.Util.ParenthesisType.Small:
                    case GScript.Analyzer.Util.ParenthesisType.Middle:
                        string s = arg[0].Value.ToString();
                        Console.Write(StringHelper.ToCSString(s));
                        break;
                    default:
                        ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                        ExceptionOperator.SetException(new ArgFormatException("Argument parenthesis should small or middle"));
                        result.Complated = false;
                        break;
                }

            }
            else if (arg.Count == 2)
            {
                if (context.Command.TypeArgPairs[arg[0]] == GScript.Analyzer.Util.ParenthesisType.Small)
                {
                    if (context.Command.TypeArgPairs[arg[0]] == GScript.Analyzer.Util.ParenthesisType.Small)
                        Console.WriteLine(arg[0].Value);
                }
                Console.WriteLine(arg[0].Value);
            }
            result.Complated = true;

            return result;
        }

        ExecuteResult InputCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            var cmd = context.Command;

            var arg = context.Command.Args;

            if (context.Command.TypeArgPairs[arg[0]] == GScript.Analyzer.Util.ParenthesisType.Small)
            {
                //GSScript.CurrentScript.Vars[(arg[0] as GSVar).Name].Value = Console.ReadLine();
                arg[0].Value = Console.ReadLine();
                result.Complated = true;
            }
            else
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
            }

            return result;
        }

        ExecuteResult DefFuncCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            var cmd = context.Command;
            if (_inFuncBlock)
            {
                ExceptionOperator.SetLastError(GSBE_INLINEFUNCINVAILD);
            }

            if (_callStack.Count > 0)
                result.Complated = true;

            _inFuncBlock = true;

            var name = (context.Command.Args[0].Value.ToString());

            _currentFunc.Item1 = name;
            _currentFunc.Item2 = new(context.Line, context.Line);
            result.Complated = true;

            return result;
        }

        ExecuteResult DefFuncEndCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            var cmd = context.Command;

            if (_inFuncBlock)
            {
                _inFuncBlock = false;

                _currentFunc.Item2 = new(_currentFunc.Item2.Start, context.Line);

                _funcTable.Add(_currentFunc.Item1, _currentFunc.Item2);
            }

            if (_callStack.Count > 0)
            {
                if (context.Command.Args.Count > 0)
                    Script.CurrentScript.SetVar(M_CallRet, context.Command.Args[0].Value);

                result.Line = _callStack.Pop();
            }

            result.Complated = true;
            return result;
        }

        ExecuteResult CallCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);

            if (!_funcTable.ContainsKey((context.Command.Args[0].Value as string)))
            {
                ExceptionOperator.SetLastError(GSBE_FUNCISNTEXISTS);
                result.Complated = false;
                return result;
            }

            if (context.Command.Args.Count > 1)
            {
                for (int i = 1; i < context.Command.Args.Count; i++)
                {
                    Script.CurrentScript.SetVar($"$ARG{i}$", context.Command.Args[i].Value);
                }
            }

            _callStack.Push(context.Line);
            var name = (context.Command.Args[0].Value as string);
            result.Line = _funcTable[name].Start.Value;

            result.Complated = true;

            return result;
        }

        ExecuteResult SysTypeStaticCallCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);

            ObjectType? Type = (context.Command.Args[0] as ObjectType);
            Type t = Type.Value as Type;
            var method = t.GetMethod(context.Command.Args[1].Value as string, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, context.Command.Args.Count > 2 ? context.Command.Args.ToArray()[2..].Select(x => DeBox(x).GetType()).ToArray() : Array.Empty<Type>());

            List<object> param = new();

            int i = 0;
            foreach (var pi in method.GetParameters())
            {
                param.Add(Convert.ChangeType(context.Command.Args[2 + i].Value, pi.ParameterType));
                i++;
            }

            if (method.ReturnType == typeof(void))
            {
                method.Invoke(null, param.ToArray());
            }
            else
            {
                Script.CurrentScript.SetVar(M_CommandResult, method.Invoke(context.Command.Args[0].Value, param.ToArray()));
            }

            result.Complated = true;

            return result;
        }

        ExecuteResult AssignCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (context.Command.Args[0] as Analyzer.InternalType.Variable != null && (context.Command.Args[0] as Analyzer.InternalType.Variable).Name == M_ValueStackTop)
            {
                ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                result.Complated = false;
            }
            context.Command.Args[0].Value = context.Command.Args[1].Value;

            return result;
        }

        ExecuteResult VarCommand(ExecuteContext context)
        {
            Analyzer.InternalType.Variable variable = context.Command.Args[0] as Analyzer.InternalType.Variable;
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (Script.CurrentScript.Vars.ContainsKey(variable.Name))
            {
                ExceptionOperator.SetLastError(GSBE_VARIABLEISEXISTS);
                result.Complated = false;
                return result;
            }
            Script.CurrentScript.AddVar(variable.Name);
            if (context.Command.Args.Count == 2)
            {
                Script.CurrentScript.SetVar(variable.Name, context.Command.Args[1].Value);
            }
            return result;
        }

        ExecuteResult DeVarCommand(ExecuteContext context)
        {
            Analyzer.InternalType.Variable variable = context.Command.Args[0] as Analyzer.InternalType.Variable;
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (!Script.CurrentScript.Vars.ContainsKey(variable.Name))
            {
                ExceptionOperator.SetLastError(GSBE_VARIABLEISNTEXISTS);
                result.Complated = false;
                return result;
            }
            Script.CurrentScript.RemoveVar(variable.Name);
            return result;
        }

        #region Operator
        ExecuteResult AddCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            Dictionary<(Type, Type), Func<object, object, bool>> AddProc = new()
            {
                [(TypeField.String, TypeField.Char)] = AddString,
                [(TypeField.String, TypeField.String)] = AddString,
                [(TypeField.Char, TypeField.String)] = AddString,
                [(TypeField.Char, TypeField.Char)] = AddString,
                [(TypeField.Long, TypeField.Long)] = AddNumber_LL,
                [(TypeField.Numeric.Int128, TypeField.Numeric.Int128)] = AddNumber_L128,
                [(TypeField.Long, TypeField.Numeric.Int128)] = AddNumber_L128,
                [(TypeField.Numeric.Int128, TypeField.Long)] = AddNumber_L128,
                [(TypeField.Numeric.BigInteger, TypeField.Numeric.BigInteger)] = AddNumber_NL,
                [(TypeField.Long, TypeField.Numeric.BigInteger)] = AddNumber_NL,
                [(TypeField.Numeric.BigInteger, TypeField.Long)] = AddNumber_NL,
                [(TypeField.Numeric.Int128, TypeField.Numeric.BigInteger)] = AddNumber_NL,
                [(TypeField.Numeric.BigInteger, TypeField.Numeric.Int128)] = AddNumber_NL
            };

            bool AddString(object a, object b)
            {
                string aStr = a.ToString();
                string bStr = b.ToString();
                Script.CurrentScript.SetVar(M_CommandResult, aStr + bStr);
                return true;
            }

            bool AddNumber_LL(object a, object b)
            {
                long aNumber = (long)a;
                long bNumber = (long)b;
                try
                {
                    Script.CurrentScript.SetVar(M_CommandResult, aNumber + bNumber);
                    return true;
                }
                catch (OverflowException e)
                {
                    ErrorData ed = new(context.Line, context.Command.ToCommandString(), e, "Add operation overflow (number + number).");
                    ExceptionOperator.SetLastErrorEx(ed);
                    return false;
                }
            }

            bool AddNumber_L128(object a, object b)
            {
                Int128 aLongNumber = (Int128)a;
                Int128 bLongNumber = (Int128)b;
                try
                {
                    Script.CurrentScript.SetVar(M_CommandResult, aLongNumber + bLongNumber);
                    return true;
                }
                catch (OverflowException e)
                {
                    ErrorData ed = new(context.Line, context.Command.ToCommandString(), e, "Add operation overflow (number + number).");
                    ExceptionOperator.SetLastErrorEx(ed);
                    return false;
                }
            }

            bool AddNumber_NL(object a, object b)
            {
                BigInteger aLargerNumber = (BigInteger)a;
                BigInteger bLargerNumber = (BigInteger)b;
                try
                {
                    Script.CurrentScript.SetVar(M_CommandResult, aLargerNumber + bLargerNumber);
                    return true;
                }
                catch (OverflowException e)
                {
                    ErrorData ed = new(context.Line, context.Command.ToCommandString(), e, "Add operation overflow (number + number).");
                    ExceptionOperator.SetLastErrorEx(ed);
                    return false;
                }
            }

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            object avt = (~a.ValueType.Clone().As<Union<string, System.Type>>());
            object bvt = (~b.ValueType.Clone().As<Union<string, System.Type>>());

            Type aType = avt as Type ?? typeof(void);
            Type bType = bvt as Type ?? typeof(void);

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (long)v.Value + (long)b.Value);
                        result.Complated = true;
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (AddProc.TryGetValue((aType, bType), out Func<object, object, bool> func))
            {
                result.Complated = func(a.Value, b.Value);
            }
            else
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), ExceptionOperator.GErrorCode.GSE_WRONGARG, "invaild arg.");
                ExceptionOperator.SetLastErrorEx(ed);
            }

            //Script.CurrentScript.SetVar(M_CommandResult, (long)a.Value + (long)b.Value);
            return result;
        }

        ExecuteResult SubCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (long)v.Value - (long)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Long || b.ValueType != TypeField.Long)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (long)a.Value - (long)b.Value);
            return result;
        }

        ExecuteResult MulCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (long)v.Value * (long)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Long || b.ValueType != TypeField.Long)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (long)a.Value * (long)b.Value);
            return result;
        }

        ExecuteResult DivCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (long)v.Value / (long)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Long || b.ValueType != TypeField.Long)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (long)a.Value / (long)b.Value);
            return result;
        }

        ExecuteResult AddFCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Double || b.ValueType != TypeField.Double)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (double)a.Value + (double)b.Value);
            return result;
        }

        ExecuteResult SubFCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Double || b.ValueType != TypeField.Double)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (double)a.Value - (double)b.Value);
            return result;
        }

        ExecuteResult MulFCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result; ;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Double || b.ValueType != TypeField.Double)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (double)a.Value * (double)b.Value);
            return result;
        }

        ExecuteResult DivFCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Double || b.ValueType != TypeField.Double)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (double)a.Value / (double)b.Value);
            return result;
        }

        ExecuteResult AddLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (Int128)v.Value + (Int128)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.Int128 || b.ValueType != TypeField.Numeric.Int128)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (Int128)a.Value + (Int128)b.Value);
            return result;
        }

        ExecuteResult SubLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (Int128)v.Value - (Int128)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.Int128 || b.ValueType != TypeField.Numeric.Int128)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (Int128)a.Value - (Int128)b.Value);
            return result;
        }

        ExecuteResult MulLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (Int128)v.Value * (Int128)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.Int128 || b.ValueType != TypeField.Numeric.Int128)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (Int128)a.Value * (Int128)b.Value);
            return result;
        }

        ExecuteResult DivLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (Int128)v.Value / (Int128)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.Int128 || b.ValueType != TypeField.Numeric.Int128)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (Int128)a.Value / (Int128)b.Value);
            return result;
        }

        ExecuteResult AddBLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (BigInteger)v.Value + (BigInteger)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.BigInteger || b.ValueType != TypeField.Numeric.BigInteger)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (BigInteger)a.Value + (BigInteger)b.Value);
            return result;
        }

        ExecuteResult SubBLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (BigInteger)v.Value - (BigInteger)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.BigInteger || b.ValueType != TypeField.Numeric.BigInteger)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (BigInteger)a.Value - (BigInteger)b.Value);
            return result;
        }

        ExecuteResult MulBLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (BigInteger)v.Value * (BigInteger)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.BigInteger || b.ValueType != TypeField.Numeric.BigInteger)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (BigInteger)a.Value * (BigInteger)b.Value);
            return result;
        }

        ExecuteResult DivBLCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            var a = context.Command.Args[0];
            var b = context.Command.Args[1];

            if (a is Analyzer.InternalType.Variable v)
            {
                switch (v.Name)
                {
                    case M_ValueStackPointer:
                        _script.SetVar(M_ValueStackPointer, (BigInteger)v.Value / (BigInteger)b.Value);
                        return result;
                    case M_ValueStackTop:
                        ExceptionOperator.SetLastError(GSBE_INVAILDCRITIALVARIABLEOPERATOR);
                        result.Complated = false;
                        return result;
                    default:
                        break;
                }
            }

            if (a.ValueType != TypeField.Numeric.BigInteger || b.ValueType != TypeField.Numeric.BigInteger)
            {
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                ExceptionOperator.SetException(new ArgumentException("Different type execute 'Add' operation is invaild."));
                result.Complated = false;
                return result;
            }

            Script.CurrentScript.SetVar(M_CommandResult, (BigInteger)a.Value / (BigInteger)b.Value);
            return result;
        }

        #endregion

        int GetValueStackVaildValueLength(List<object> valueStack)
        {
            for (int i = 0; i < valueStack.Count; i++)
            {
                if (valueStack[i].GetType() == TypeField.Object)
                    return i;
            }
            return 0;
        }

        ExecuteResult PushCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            int Pointer = (int)_script.Vars[M_ValueStackPointer].Value;
            int length = GetValueStackVaildValueLength(_valueStack);
            if (Pointer > length - 1 || 0 > Pointer)
            {
                ExceptionOperator.SetLastError(GSBE_STACKPOINTEROUTOFRANGE);
                result.Complated = false;
                return result;
            }
            else if (Pointer < length - 1)
            {
                for (int i = length - 1; i > (length - 1 - Pointer); i--)
                {
                    (_valueStack[i], _valueStack[i + 1]) = (_valueStack[i + 1], _valueStack[i]);
                }
            }

            _valueStack[Pointer] = context.Command.Args[0].Value;
            return result;
        }

        ExecuteResult PullCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            int Pointer = (int)_script.Vars[M_ValueStackPointer].Value;
            int length = GetValueStackVaildValueLength(_valueStack);
            object pv = new();
            if (Pointer < length - 1 || Pointer < 0)
            {
                ExceptionOperator.SetLastError(GSBE_STACKPOINTEROUTOFRANGE);
                result.Complated = false;
                return result;
            }
            pv = _valueStack[Pointer];
            if (length - 1 > Pointer)
            {
                for (int i = (length - 1 - Pointer); i < length; i++)
                {
                    if (i == 0)
                        continue;

                    // swap value of them
                    // inventor of this sentence is so sky god!!
                    (_valueStack[i], _valueStack[i - 1]) = (_valueStack[i - 1], _valueStack[i]);
                }
            }
            else
            {
                _valueStack[Pointer] = new();
            }

            _script.SetVar(M_CommandResult, pv);
            return result;
        }

        ExecuteResult PeekCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            int Pointer = (int)_script.Vars[M_ValueStackPointer].Value;
            int length = GetValueStackVaildValueLength(_valueStack);
            if (Pointer < length - 1 || Pointer < 0)
            {
                ExceptionOperator.SetLastError(GSBE_STACKPOINTEROUTOFRANGE);
                result.Complated = false;
                return result;
            }

            _script.SetVar(M_CommandResult, _script.Vars[M_ValueStackPointer].Value);
            return result;
        }

        ExecuteResult TagCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            _tags.Add(context.Command.Args[0] as Tag, context.Line);
            return result;
        }

        bool JmpInternal(object arg, string commandstring, ref int line)
        {
            if (arg is Tag tag)
            {
                line = _tags.First(x => x.Key.TagName == tag.TagName).Value;
            }
            else if (arg is ScriptObject elsetag && elsetag.ValueType == TypeField.Int)
            {
                line = (int)elsetag.Value;
            }
            else
            {
                ErrorData ed = new(line, commandstring, new ArgumentException("Arg $0 type is wrong."), "Arg $0 type is wrong.");
                ExceptionOperator.SetLastErrorEx(ed);
                return false;
            }
            return true;
        }

        ExecuteResult JmpCommand(ExecuteContext context)
        {
            int line = context.Line;

            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = JmpInternal(context.Command.Args[0], context.Command.ToCommandString(), ref line);
            result.Line = line;

            return result;
        }

        ExecuteResult JmpcCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            int line = result.Line;

            switch (context.Command.Args.Count)
            {
                case 2:
                    if (context.Command.Args[0].Value is bool c && c)
                    {
                        bool complate = JmpInternal(context.Command.Args[1], context.Command.ToCommandString(), ref line);
                        if (complate)
                        {
                            result.Line = line;
                        }
                        else
                        {
                            result.Complated = false;
                            return result;
                        }
                    }
                    ErrorData ed = new(context.Line, context.Command.ToCommandString(), new ArgumentException("Arg type is wrong"), "Arg type is wrong");
                    ExceptionOperator.SetLastErrorEx(ed);
                    break;
                case 3:
                    if (context.Command.Args[2].Value is bool iselse)
                    {
                        if (context.Command.Args[0].Value is bool c2 && c2 == !iselse)
                        {
                            bool complate = JmpInternal(context.Command.Args[1], context.Command.ToCommandString(), ref line);
                            if (complate)
                            {
                                result.Line = line;
                            }
                            else
                            {
                                result.Complated = false;
                                return result;
                            }
                        }
                        return result;
                    }
                    ErrorData ed2 = new(context.Line, context.Command.ToCommandString(), new ArgumentException("Arg type is wrong"), "Arg type is wrong");
                    ExceptionOperator.SetLastErrorEx(ed2);
                    break;
            }
            result.Complated = false;
            return result;
        }

        ExecuteResult NoRunTrueCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            _noRun = true;
            return result;
        }

        ExecuteResult NoRunFalseCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            _noRun = false;
            return result;
        }

        bool CompareInternal(object left, object right, string symbol)
        {
            try
            {
                switch (symbol)
                {
                    case "==":
                        return left.Equals(right);
                    case "!=":
                        return !left.Equals(right);
                    case ">":
                        return (left as IComparable).CompareTo(right) > 0;
                    case ">=":
                        return (left as IComparable).CompareTo(right) is > 0 or 0;
                    case "<":
                        return (left as IComparable).CompareTo(right) < 0;
                    case "<=":
                        return (left as IComparable).CompareTo(right) is < 0 or 0;
                }
            }
            catch { }
            return false;
        }

        ExecuteResult CompCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            Script.CurrentScript.SetVar(M_CommandResult, CompareInternal(context.Command.Args[1].Value, context.Command.Args[2].Value, (context.Command.Args[0] as Flag).FlagValue));
            return result;
        }

        ExecuteResult ExitCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (context.Command.Args.Count == 1 && context.Command.Args[0].Value is int exitcode)
            {
                Environment.Exit(exitcode);
            }
            else
            {
                Environment.Exit(0);
            }
            return result;
        }

        ExecuteResult DefClassCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (_inClassBlock || _inFuncBlock)
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new InvaildScriptSegmentException("Class in class."), "Class in class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            _inClassBlock = true;
            ClassTemplate ct = new((string)(context.Command.Args[0].Value as Unknown), context.Line);
            _currentClass = ct;
            return result;
        }

        ExecuteResult PropCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;
            if (!_inClassBlock || _inFuncBlock)
            {
                result.Complated = false;
                return result;
            }
            ClassProperty cp = new(context.Command.Args[0].Value as string, (context.Command.Args[1] as ObjectType).Value as Type);
            _currentClass.RegisterProp(cp.Name, cp);
            return result;
        }

        string GeneratorFunctionSignature(Arguments args, bool inDef = true)
        {
            List<string> ArgTypes = new List<string>();

            foreach (var arg in args)
            {
                if (arg is ObjectType objectType)
                {
                    ArgTypes.Add(inDef ? objectType.Value.ToString() : typeof(ObjectType).FullName);
                }
                else
                {
                    var T = ~arg.ValueType.Clone().As<Union<string, Type>>();
                    ArgTypes.Add(T.ToString());
                }
            }
            return string.Join('@', ArgTypes);
        }

        ExecuteResult CtorCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (!_inClassBlock || _inFuncBlock)
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new InvaildScriptSegmentException("Ctor. without class."), "Ctor. without class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            ClassFunction ctorfunc = new($".{_currentClass.Name}@{(context.Command.Args.Count > 0 ? GeneratorFunctionSignature(context.Command.Args.ToArray()) : "")}.ctor", Privation.Public, context.Line);
            _inFuncBlock = true;

            _currentClass.RegisterFunc(ctorfunc.Name, ctorfunc);

            return result;
        }

        ExecuteResult CtorEndCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (_callStack.Count > 0)
            {
                if (context.Command.Args.Count > 0)
                {
                    Script.CurrentScript.SetVar(M_CallRet, context.Command.Args[0]);
                }
                result.Line = _callStack.Pop();
                return result;
            }
            if (_inFuncBlock && _inClassBlock)
            {
                _inFuncBlock = false;
                return result;
            }
            else
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new InvaildScriptSegmentException("Ctor. end without class."), "Ctor. end without class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }
        }


        ExecuteResult DefCFuncCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (!_inClassBlock || _inFuncBlock)
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new InvaildScriptSegmentException("Member function without class."), "Member function without class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            if (_callStack.Count > 0)
                return result;

            ClassFunction func = new($".{_currentClass.Name}@{(context.Command.Args.Count > 2 ? GeneratorFunctionSignature(context.Command.Args.ToArray()[2..]) : "")}.{context.Command.Args[0].Value}", (Privation)Enum.Parse(typeof(Privation), context.Command.Args[1].Value as string), context.Line);

            _currentClass.RegisterFunc(func.Name, func);

            _inFuncBlock = true;

            return result;
        }

        ExecuteResult DefCFuncEndCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (_callStack.Count > 0)
            {
                if (context.Command.Args.Count > 0)
                {
                    Script.CurrentScript.SetVar(M_CallRet, context.Command.Args[0]);
                }
                result.Line = _callStack.Pop();
                return result;
            }

            if (_inFuncBlock && _inClassBlock)
            {
                _inFuncBlock = false;
                return result;
            }

            ErrorData ed = new(context.Line, context.Command.ToCommandString(), new InvaildScriptSegmentException("Member function without class."), "Member function without class.");
            ExceptionOperator.SetLastErrorEx(ed);
            result.Complated = false;
            return result;
        }

        // callM Variable(ValueType = Type) Type MemberName Args
        ExecuteResult CallMemberCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            int orgline = context.Line;

            if ((context.Command.Args[1] as ObjectType).Exists)
            {
                Type t = (context.Command.Args[1] as ObjectType).Value as Type;
                var method = t.GetMethod(context.Command.Args[2].Value as string, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, context.Command.Args.Count > 3 ? context.Command.Args.ToArray()[3..].Select(x => DeBox(x).GetType()).ToArray() : Array.Empty<Type>());

                List<object> param = new();

                int i = 0;
                foreach (var pi in method.GetParameters())
                {
                    param.Add(Convert.ChangeType(context.Command.Args[3 + i].Value, pi.ParameterType));
                }

                if (method.ReturnType == typeof(void))
                {
                    method.Invoke(context.Command.Args[0].Value, param.ToArray());
                }
                else
                {
                    Script.CurrentScript.SetVar(M_CommandResult, method.Invoke(context.Command.Args[0].Value, param.ToArray()));
                }

                return result;
            }

            if (VariableIsDelcarClass(context.Command))
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new ArgumentException("SELF object is not delcar. to this class."), "SELF object is not delcar. to this class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            var instance = (context.Command.Args[0].Value as ClassInstance);
            ScriptObject[] argArray = context.Command.Args.ToArray();
            int line = context.Line;

            bool complate = instance.Invoke($".{(context.Command.Args[1] as ObjectType).Value as string}@{(context.Command.Args.Count > 3 ? GeneratorFunctionSignature(context.Command.Args.ToArray()[3..]) : "")}.{context.Command.Args[2]}", ref line, context.Command.Args.Count > 3 ? argArray[3..] : null);
            if (complate)
            {
                result.Line = line;
                _callStack.Push(orgline);
            }
            else
            {
                result.Complated = false;
            }
            return result;
        }

        // getProp Variable Type MemberName
        ExecuteResult GetPropCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if ((context.Command.Args[1] as ObjectType).Exists)
            {
                Type t = (context.Command.Args[1] as ObjectType).Value as Type;
                Script.CurrentScript.SetVar(M_CommandResult, t.GetProperty(context.Command.Args[2].Value as string).GetValue(context.Command.Args[0].Value));
                return result;
            }
            if (VariableIsDelcarClass(context.Command))
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new ArgumentException("SELF object is not delcar. to this class."), "SELF object is not delcar. to this class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            var instance = (context.Command.Args[0].Value as ClassInstance);
            try
            {
                Script.CurrentScript.SetVar(M_CommandResult, instance.GetProperty(context.Command.Args[2].Value as string));
            }
            catch (KeyNotFoundException e)
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), e, "Member in " + instance.ClassName + "  no found.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }
            return result;
        }

        static bool VariableIsDelcarClass(Command cmd)
        {
            return ((cmd.Args[1] as ObjectType).Value as string) != (DeBox(cmd.Args[0]) as ClassInstance).ClassName;
        }

        static object DeBox(ScriptObject scriptObject)
        {
            if (scriptObject is not Analyzer.InternalType.Variable)
            {
                return scriptObject.Value;
            }
            else
            {
                if (scriptObject.Value is not ScriptObject)
                    return scriptObject.Value;
                return DeBox(scriptObject.Value as ScriptObject);
            }
        }

        ExecuteResult SetPropCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if ((context.Command.Args[1] as ObjectType).Exists)
            {
                Type t = (context.Command.Args[1] as ObjectType).Value as Type;
                t.GetProperty(context.Command.Args[2].Value as string).SetValue(context.Command.Args[0].Value, DeBox(context.Command.Args[3]));
                return result;
            }

            if (VariableIsDelcarClass(context.Command))
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new ArgumentException("SELF object is not delcar. to this class."), "SELF object is not delcar. to this class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            var instance = (context.Command.Args[0].Value as ClassInstance);
            result.Complated = instance.SetProperty(context.Command.Args[2].Value as string, DeBox(context.Command.Args[3]));
            return result;
        }

        ExecuteResult DefClassEndCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            if (!_inClassBlock || _inFuncBlock)
            {
                ErrorData ed = new(context.Line, context.Command.ToCommandString(), new InvaildScriptSegmentException("Ctor. without class."), "Ctor. without class.");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            _currentClass.ClassEnd(context.Line);
            _classTemplateTable.Add(_currentClass.Name, _currentClass);


            var ctorPairList =
                _currentClass
                .GetFunctions()
                .ToList();

            ctorPairList.RemoveAll(x => !x.Key.Contains(".ctor"));

            var ctorList = ctorPairList.Select(x => x.Value);

            DefaultCommandParser.RegisterCustomTypeConverter(_currentClass.Name, (args) =>
            {
                if (!ctorList.Any(x => x.Name == $".{_currentClass.Name}@{GeneratorFunctionSignature(context.Command.Args.ToArray())}..ctor"))
                {
                    return null;
                }

                var ctor = ctorList.First(x => x.Name == $".{_currentClass.Name}@{GeneratorFunctionSignature(context.Command.Args.ToArray())}..ctor");

                InvokeFrame frame = InvokeFrame.CreateFrameFromScript(Script.CurrentScript);
                Script orgEnv = Script.CurrentScript;
                Script subEnv = new Script();
                InvokeFrame.SetScriptStatusFromFrame(subEnv, frame);
                subEnv.Commands = orgEnv.Commands.GetRange(_currentClass.ClassArea.Start.Value - 1, _currentClass.ClassArea.End.Value - 1 - _currentClass.ClassArea.Start.Value - 1);

                var instance = _currentClass.CreateInstance("");
                int index = 0;
                foreach (var arg in args)
                {
                    subEnv.SetVar($"$ARG{(index > 0 ? index : "")}$", arg);
                }
                subEnv.SetVar(M_SelfObject, instance);

                subEnv.CurrentLine = ctor.Entry;

                var complate = subEnv.Execute();
                return complate ?
                new ScriptObject()
                {
                    Value = instance,
                    ValueType = _currentClass.Name
                }
                :
                null;
            });
            _currentClass = null;

            _inClassBlock = false;
            return result;
        }

        // Variable Type Args
        ExecuteResult InitCommand(ExecuteContext context)
        {
            ExecuteResult result = ExecuteResult.CreateFromContext(context);
            result.Complated = true;

            int orgline = context.Line;

            if (!_classTemplateTable.ContainsKey((context.Command.Args[1] as ObjectType).Value as string))
            {
                ErrorData ed = new ErrorData(context.Line, context.Command.ToCommandString(), new ArgumentException("Class " + ((context.Command.Args[1] as ObjectType).Value as string) + " not in this script. (maybe not include?)"), "Class " + ((context.Command.Args[1] as ObjectType).Value as string) + " not in this script. (maybe not include?)");
                ExceptionOperator.SetLastErrorEx(ed);
                result.Complated = false;
                return result;
            }

            var instance = _classTemplateTable[(context.Command.Args[1] as ObjectType).Value as string].CreateInstance(context.Command.Args[0].Value as string);

            ScriptObject[] argArray = context.Command.Args.ToArray();
            int line = context.Line;
            bool complate = instance.Invoke($".{(context.Command.Args[1] as ObjectType).Value as string}@{(context.Command.Args.Count > 2 ? GeneratorFunctionSignature(context.Command.Args.ToArray()[2..]) : "")}.ctor", ref line, context.Command.Args.Count > 2 ? argArray[2..] : null);
            if (!complate)
            {
                result.Complated = false;
                return result;
            }

            _callStack.Push(orgline);

            context.Command.Args[0].Value = instance;
            context.Command.Args[0].ValueType = typeof(ClassInstance);

            result.Line = line;
            result.Complated = complate;
            return result;
        }

        void GlobalHandler(Command cmd, ref bool cancel, ref int line)
        {
            if (_alwaysRun.Contains(cmd.Name))
                return;

            foreach (var attributeitem in _attributes)
            {
                if (cmd.Name.Contains(attributeitem))
                    return;
            }

            cancel = _inFuncBlock || _noRun;
        }

        script.RegisterGlobalCommandHandler(GlobalHandler);

        #region Register Handles

        script.RegisterCommandHandler("out", (
            OutCommand,
            new CommandArgumentOptions()
            {
                VaildArgumentParenthesis = false,
                VaildArgumentCount = true,
                VaildArgumentType = false,
                CountRange = new Range(1, 1)
            }
        ));

        // input command
        script.RegisterCommandHandler("input", (
            InputCommand,
            new CommandArgumentOptions()
            {
                VaildArgumentType = false,
                VaildArgumentCount = true,
                VaildArgumentParenthesis = true,
                CountRange = new Range(1, 1),
                ArgumentParenthesisTypePairs = new List<GScript.Analyzer.Util.ParenthesisType>()
                { GScript.Analyzer.Util.ParenthesisType.Small}
            }
        ));

        script.RegisterCommandHandler("func", (
            DefFuncCommand,
            new CommandArgumentOptions()
            {
                VaildArgumentCount = true,
                VaildArgumentParenthesis = true,
                VaildArgumentType = true,
                CountRange = new Range(1, 1),
                ArgumentParenthesisTypePairs = new List<GScript.Analyzer.Util.ParenthesisType>()
                { GScript.Analyzer.Util.ParenthesisType.Middle },
                ArgumentTypePairs = new()
                {
                    "function"
                }
            }
        ));

        script.RegisterCommandHandler("funcEnd", (
            DefFuncEndCommand,
            new CommandArgumentOptions()
            {
                VaildArgumentCount = true,
                VaildArgumentParenthesis = false,
                VaildArgumentType = false,
                CountRange = new Range(0, 1),
            }
        ));

        script.RegisterCommandHandler("call", (
            CallCommand,
            new CommandArgumentOptions()
            {
                VaildArgumentCount = true,
                VaildArgumentParenthesis = true,
                VaildArgumentType = true,
                CountRange = new Range(1, 9),
                ArgumentParenthesisTypePairs = new List<GScript.Analyzer.Util.ParenthesisType>()
                { GScript.Analyzer.Util.ParenthesisType.Middle },
                ArgumentTypePairs = new()
                {
                    "function",
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object
                }
            }
        ));

        script.RegisterCommandHandler("assign", (
            AssignCommand,
            new CommandArgumentOptions()
            {
                VaildArgumentCount = true,
                VaildArgumentParenthesis = false,
                VaildArgumentType = false,
                CountRange = new Range(2, 2)
            }
        ));


        script.RegisterCommandHandler("deVar", (
            DeVarCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(1, 1),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new List<GScript.Analyzer.Util.ParenthesisType>
                { GScript.Analyzer.Util.ParenthesisType.Small }
            }
        ));

        script.RegisterCommandHandler("var", (
            VarCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(1, 1),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new List<GScript.Analyzer.Util.ParenthesisType>()
                { GScript.Analyzer.Util.ParenthesisType.Small }
            }
        ));


        script.RegisterCommandHandler("pull", (
            PullCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("push", (
            PushCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(1, 1),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("peek", (
            PeekCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));

        script.RegisterCommandHandler("add", (
            AddCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false,
            }
        ));

        script.RegisterCommandHandler("sub", (
            SubCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Long, TypeField.Long }
            }
        ));

        script.RegisterCommandHandler("mul", (
            MulCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Long, TypeField.Long }
            }
        ));

        script.RegisterCommandHandler("div", (
            DivCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Long, TypeField.Long }
            }
        ));

        script.RegisterCommandHandler("addF", (
            AddFCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Double, TypeField.Double }
            }
        ));

        script.RegisterCommandHandler("subF", (
            SubFCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Double, TypeField.Double }
            }
        ));

        script.RegisterCommandHandler("mulF", (
            MulFCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Double, TypeField.Double }
            }
        ));

        script.RegisterCommandHandler("divF", (
            DivFCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Double, TypeField.Double }
            }
        ));

        script.RegisterCommandHandler("addL", (
            AddLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.Int128, TypeField.Numeric.Int128 }
            }
        ));

        script.RegisterCommandHandler("subL", (
            SubLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.Int128, TypeField.Numeric.Int128 }
            }
        ));

        script.RegisterCommandHandler("mulL", (
            MulLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.Int128, TypeField.Numeric.Int128 }
            }
        ));

        script.RegisterCommandHandler("divL", (
            DivLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.Int128, TypeField.Numeric.Int128 }
            }
        ));

        script.RegisterCommandHandler("addBL", (
            AddBLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.BigInteger, TypeField.Numeric.BigInteger }
            }
        ));

        script.RegisterCommandHandler("subBL", (
            SubBLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.BigInteger, TypeField.Numeric.BigInteger }
            }
        ));

        script.RegisterCommandHandler("mulBL", (
            MulBLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.BigInteger, TypeField.Numeric.BigInteger }
            }
        ));

        script.RegisterCommandHandler("divBL", (
            DivBLCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                { TypeField.Numeric.BigInteger, TypeField.Numeric.BigInteger }
            }
        ));


        script.RegisterCommandHandler("tag", (
            TagCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(1, 1),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                {
                    typeof(Tag)
                }
            }
        ));


        script.RegisterCommandHandler("jmp", (
            JmpCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(1, 1),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("jmpc", (
            JmpcCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 3),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("comp", (
            CompCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(3, 3),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = false,
                ArgumentTypePairs = new()
                {
                    typeof(Object), TypeField.Object, TypeField.Object
                }
            }
        ));


        script.RegisterCommandHandler("exit", (
            ExitCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 1),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("class", (
            DefClassCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(1, 1),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Middle
                },
                ArgumentTypePairs = new()
                {
                    "class"
                }
            }
        ));


        script.RegisterCommandHandler("ctor", (
            CtorCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 8),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big
                }
            }
        ));


        script.RegisterCommandHandler("ctorEnd", (
            CtorEndCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("MFunc", (
            DefCFuncCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 10),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Middle,
                    ParenthesisType.None,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big,
                    ParenthesisType.Big
                },
                ArgumentTypePairs = new()
                {
                    "function",
                    "Literal",
                    typeof(ObjectType),
                    typeof(ObjectType),
                    typeof(ObjectType),
                    typeof(ObjectType),
                    typeof(ObjectType),
                    typeof(ObjectType),
                    typeof(ObjectType),
                    typeof(ObjectType)
                }
            }
        ));


        script.RegisterCommandHandler("MFuncEnd", (
            DefCFuncEndCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("prop", (
            PropCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 2),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Middle,
                    ParenthesisType.Big
                },
                ArgumentTypePairs = new()
                {
                    "property",
                    typeof(ObjectType)
                }
            }
        ));


        script.RegisterCommandHandler("callM", (
            CallMemberCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(3, 11),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Small,
                    ParenthesisType.Big,
                    ParenthesisType.Middle,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown
                },
                ArgumentTypePairs = new()
                {
                    typeof(object),
                    typeof(ObjectType),
                    "function",
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object
                }
            }
        ));


        script.RegisterCommandHandler("sysTypeStaticCall", (
            SysTypeStaticCallCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 10),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Big,
                    ParenthesisType.Middle,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown
                },
                ArgumentTypePairs = new()
                {
                    typeof(ObjectType),
                    "function",
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object,
                    TypeField.Object
                }
            }
        ));


        script.RegisterCommandHandler("getProp", (
            GetPropCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(3, 3),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Small,
                    ParenthesisType.Big,
                    ParenthesisType.Middle
                },
                ArgumentTypePairs = new()
                {
                    typeof(object),
                    typeof(ObjectType),
                    "property"
                }
            }
        ));


        script.RegisterCommandHandler("setProp", (
            SetPropCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(4, 4),
                VaildArgumentCount = true,
                VaildArgumentType = true,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Small,
                    ParenthesisType.Big,
                    ParenthesisType.Middle,
                    ParenthesisType.Unknown
                },
                ArgumentTypePairs = new()
                {
                    typeof(object),
                    typeof(ObjectType),
                    "property",
                    TypeField.Object
                }
            }
        ));


        script.RegisterCommandHandler("init", (
            InitCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(2, 10),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = true,
                ArgumentParenthesisTypePairs = new()
                {
                    ParenthesisType.Small,
                    ParenthesisType.Big,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown,
                    ParenthesisType.Unknown
                }
            }
        ));

        script.RegisterCommandHandler("classEnd", (
            DefClassEndCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));

        script.RegisterCommandHandler("$$NoRun?Enabled=True$$", (
            NoRunTrueCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));


        script.RegisterCommandHandler("$$NoRun?Enabled=False$$", (
            NoRunFalseCommand,
            new CommandArgumentOptions()
            {
                CountRange = new Range(0, 0),
                VaildArgumentCount = true,
                VaildArgumentType = false,
                VaildArgumentParenthesis = false
            }
        ));

        #endregion
    }

    private void InitialzeParsers(in Script script)
    {
        script.RegisterCommandParser("MFunc", new DefaultCommandParser("MFunc", true));
    }
}

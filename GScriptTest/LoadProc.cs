﻿using GScript.Analyzer;
using GScript.Analyzer.Exception;
namespace GScriptTest;

internal partial class Program
{
    public static Stack<int> whilestack = new Stack<int>();

    public static void LoadProc(in Script gss)
    {
        // out command
        gss.RegisterCommandHandler("out", (
            OutFunction, 
            new CommandArgumentOptions()
            {
                VaildArgumentParenthesis = false,
                VaildArgumentCount = true,
                VaildArgumentType = false,
                CountRange = new Range(1, 2)
            }
        ));

        // input command
        gss.RegisterCommandHandler("input", (
            InputFunction,
            new CommandArgumentOptions()
            {
                VaildArgumentType = false,
                VaildArgumentCount = true,
                VaildArgumentParenthesis = true,
                CountRange = new Range(1, 1),
                ArgumentParenthesisTypePairs = new List<GScriptAnalyzer.Util.ParenthesisType>()
                { GScriptAnalyzer.Util.ParenthesisType.Small}
            }
        ));

        // while command
        gss.RegisterCommandHandler("while", (
            WhileFunction,
            new CommandArgumentOptions()
            {
                VaildArgumentCount = true,
                VaildArgumentParenthesis = true,
                VaildArgumentType = true,
                CountRange = new Range(1,1),
                ArgumentTypePairs = new () { typeof(long) },
                ArgumentParenthesisTypePairs = new List<GScriptAnalyzer.Util.ParenthesisType> 
                { GScriptAnalyzer.Util.ParenthesisType.Middle}
            }
        ));

        // break command
        gss.RegisterCommandHandler("break", (
            BreakFunction,
            new CommandArgumentOptions(){
                VaildArgumentCount = true,
                VaildArgumentParenthesis = false,
                VaildArgumentType = false,
                CountRange = new Range(0,0)
            }
        ));

        // if command
        gss.RegisterCommandHandler("if", (
            IfFunction,
            new CommandArgumentOptions()
            {
                VaildArgumentCount = true,
                VaildArgumentParenthesis = false,
                VaildArgumentType = false,
                CountRange = new Range(4,4)
            }
        ));

        // goto command

        static bool Compare(ScriptObject a, ScriptObject b, string compflag)
        {
            switch(compflag)
            {
                case "==":
                    return Convert.ToString(a.Value) == Convert.ToString(b.Value);
                case "!=":
                    return Convert.ToString(a.Value) != Convert.ToString(b.Value);
                case ">":
                    return Convert.ToInt64(a.Value) > Convert.ToInt64(b.Value);
                case ">=":
                    return Convert.ToInt64(a.Value) >= Convert.ToInt64(b.Value);
                case "<":
                    return Convert.ToInt64(a.Value) < Convert.ToInt64(b.Value);
                case "<=":
                    return Convert.ToInt64(a.Value) <= Convert.ToInt64(b.Value);
                default:
                    return false;
            }
        }

        //if [number:1] [flag:iff:==] [number:2] [number:3]
        static bool IfFunction(Command cmd, ref int line)
        {
            try
            {
                var arg = cmd.Args;
                var result = Compare(arg[0], arg[2], (arg[1] as Flag).FlagValue);
                if (!result)
                {
                    line = arg[3].As<int>();
                }
                return true;
            }
            catch(Exception ex)
            {
                ExceptionOperator.SetException(ex);
                return false;
            }
        
        }

        static bool BreakFunction(Command cmd, ref int line)
        {
            if(whilestack.Count == 0)
            {
                ExceptionOperator.SetException(new Exception("Current have not while stack."));
                ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_ILLEGALCOMMAND);
                return false;
            }
            line = whilestack.Pop();
            return true;
        }

        static bool WhileFunction(Command cmd, ref int line)
        {
            var arg = cmd.Args;
            if(!whilestack.Contains(line))
                whilestack.Push(line);
            line = arg[0].As<int>();
            return true;
        }

        static bool InputFunction(Command cmd, ref int line)
        {
            var arg = cmd.Args;
            if (cmd.TypeArgPairs[arg[0]] == GScriptAnalyzer.Util.ParenthesisType.Small)
            {
                Script.CurrentScript.Vars[(arg[0] as Variable).Name].Value = Console.ReadLine();
                return true;
            }
            ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
            return false;
        }

        static bool OutFunction(Command cmd, ref int line)
        {
            var arg = cmd.Args;
            if (arg.Count == 1)
            {
                switch (cmd.TypeArgPairs[arg[0]])
                {
                    case GScriptAnalyzer.Util.ParenthesisType.Small:
                        Console.WriteLine(Script.CurrentScript.Vars[(arg[0] as Variable).Name].Value);
                        return true;
                    case GScriptAnalyzer.Util.ParenthesisType.Middle:
                        Console.WriteLine(arg[0].Value);
                        return true;
                    default:
                        ExceptionOperator.SetLastError(ExceptionOperator.GErrorCode.GSE_WRONGARG);
                        ExceptionOperator.SetException(new ArgFormatException("Argument parenthesis should small or middle"));
                        return false;
                }

            }
            else if (arg.Count == 2)
            {
                if (cmd.TypeArgPairs[arg[0]] == GScriptAnalyzer.Util.ParenthesisType.Small)
                {
                    if (cmd.TypeArgPairs[arg[0]] == GScriptAnalyzer.Util.ParenthesisType.Small)
                        Console.WriteLine(Script.CurrentScript.Vars[(arg[0] as Variable).Name].Value);
                    return true;
                }
                Console.WriteLine(arg[0].Value);
            }
            return true;
        }
    }
}

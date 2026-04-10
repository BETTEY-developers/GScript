using EUtility.ValueEx;
using System.Text.Json;
using System.Text.RegularExpressions;
using TrueColorConsole;
using System.Drawing;
using GScript.Analyzer.InternalType;
using GScript.Analyzer.Util;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using GScript.Editor.Helpers;
using System.Diagnostics;

namespace GScript.Editor;

internal partial class Editor
{
    public int TextAreaPaddingLeft => 6;
    public int TabSize => 4;

    public StyleTable ConfigStyle { get; set; }
    public Dictionary<string, KeyUnit> Keys { get; private set; }
    public List<List<KeyUnit>> Units { get; private set; }
    public List<string> Raw { get; private set; }
    public Stack<ContentFrame> History { get; private set; }
    public Stack<ContentFrame> BackTemp { get; private set; }
    public string FileName { get; set; } = string.Empty;

    private string currentString = string.Empty;
    private int tabCount = 0;
    private bool isPreviewMode = false;
    private int col;
    private int row;
    private int Previewpage
    {
        get;
        set
        {
            field = value;
            doUpdate = true;
        }
    }
    private int Editpage
    {
        get;
        set
        {
            field = value;
            doUpdate = true;
        }
    }
    private bool doUpdate = true;
    private int EditRow => row + Editpage * Console.WindowHeight;

    public Editor(StyleTable configStyle)
    {
        ConfigStyle = configStyle ?? throw new ArgumentNullException(nameof(configStyle));
        Keys = new();
        var all = ConfigStyle.GetList();
        all.ForEach(x => Keys.Add(x.RawString, x));

        Units = new List<List<KeyUnit>> { new() };
        Raw = Enumerable.Repeat(string.Empty, 4000).ToList();
        History = new();
        BackTemp = new();
        col = TextAreaPaddingLeft;
        row = 0;
        Previewpage = 0;
        Editpage = 0;
    }

    public Editor(string configPath)
        : this(JsonSerializer.Deserialize<StyleTable>(File.ReadAllText(configPath), new JsonSerializerOptions { WriteIndented = true })!)
    {
    }

    public void Run()
    {
        while (true)
        {
            try
            {
                if(doUpdate)
                    Update(isPreviewMode ? Previewpage : Editpage);
                
                VTConsole.CursorPosition(row + 1, col + 1);
                var key = Console.ReadKey();

                if (HandleMenu(key)) continue;
                if (HandleSave(key)) continue;
                if (HandleSaveAs(key)) continue;
                if (HandleUndo(key)) continue;
                if (HandleRedo(key)) continue;
                if (HandlePaste(key)) continue;
                if (HandleOtherKey(key)) continue;
                if ((doUpdate = false) || HandleNavigation(key)) continue;
                if (!(doUpdate = true) || HandleEdit(key)) continue;

                // 普通字符输入
                isPreviewMode = false;
                if (col - TextAreaPaddingLeft == currentString.Length)
                    currentString += key.KeyChar;
                else
                    currentString = currentString.Insert(col - TextAreaPaddingLeft, key.KeyChar.ToString());
                col++;

                UpdateLine();
            }
            catch
            {
            }
        }
    }

    private bool HandleMenu(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.F1)
        {
            ShowMenu(isPreviewMode ? Previewpage : Editpage);
            VTConsole.SetScrollingRegion(1, 1);
            return true;
        }
        return false;
    }

    private bool HandleSave(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.S && key.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift))
        {
            var content = GetContentArea();
            if (string.IsNullOrEmpty(FileName))
            {
                var file = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                if (file.IsVoid())
                    return true;
                File.WriteAllLines((file as FileInfo).FullName, content);
            }
            else
            {
                if (!File.Exists(FileName))
                    File.Create(FileName).Close();
                File.WriteAllLines(FileName, content);
            }
            return true;
        }
        return false;
    }

    private bool HandleSaveAs(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.S && key.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift))
        {
            var content = GetContentArea();
            var file = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (file.IsVoid())
                return true;
            File.WriteAllLines((file as FileInfo).FullName, content);
            return true;
        }
        return false;
    }

    private bool HandleUndo(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Z && key.Modifiers == ConsoleModifiers.Control && History.Count != 0)
        {
            (var u, List<string> ru, int r, int c, int p) = History.Pop();
            BackTemp.Push((u, ru, r, c, p));
            Units = u;
            Raw = ru;
            row = r;
            col = c;
            Editpage = p;
            currentString = Raw[EditRow];
            return true;
        }
        return false;
    }

    private bool HandleRedo(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Y && key.Modifiers == ConsoleModifiers.Control && BackTemp.Count != 0)
        {
            (var u, List<string> ru, int r, int c, int p) = BackTemp.Pop();
            History.Push((u, ru, r, c, p));
            Units = u;
            Raw = ru;
            row = r;
            col = c;
            Editpage = p;
            currentString = Raw[EditRow];
            return true;
        }
        return false;
    }

    private bool HandlePaste(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.V && key.Modifiers == (ConsoleModifiers.Shift | ConsoleModifiers.Control))
        {
            string clipboardContent = Native.GetCurrentClipboardContent();
            if (clipboardContent == null)
                return true;

            const string NewLine = "\uABCD";
            string replacedContent = clipboardContent.Replace("\r\n", NewLine)
                                                     .Replace("\r", NewLine)
                                                     .Replace("\n", NewLine);

            string[] contentLines = replacedContent.Split(NewLine);

            string afterString = string.Empty;
            int realcol = col - TextAreaPaddingLeft - 1;

            if (realcol > 0 && Raw[EditRow].Length > realcol)
            {
                afterString = Raw[EditRow][realcol..];
                Raw[EditRow] = Raw[EditRow][..(realcol - 1)];
            }

            int insertLineCount = contentLines.Length - 2;
            if (insertLineCount < 0)
                insertLineCount = 0;

            Units[EditRow] = GetUnits(Raw[EditRow]);
            Raw[EditRow] += contentLines[0];

            if (contentLines.Length == 1)
                return true;

            string[] insertLines = contentLines[1..^1];

            if (insertLineCount != 0)
            {
                Raw.InsertRange(EditRow + 1, insertLines);

                if (Units.Count < insertLines.Length)
                    Units.AddRange(insertLines.Select(x => GetUnits(x)));
                else
                    Units.InsertRange(EditRow + 1, insertLines.Select(x => GetUnits(x)));
            }

            if (insertLineCount > -1)
            {
                string lastLine = contentLines[^1] + afterString;
                if (Units.Count < EditRow + 1)
                    Units.Add(GetUnits(lastLine));
                else
                    Units.Insert(EditRow + 1 + insertLineCount, GetUnits(lastLine));
                Raw[EditRow + 1 + insertLineCount] = lastLine;
            }

            currentString = Raw[EditRow];
            col += contentLines[0].Length;
            return true;
        }
        return false;
    }

    private bool HandleOtherKey(ConsoleKeyInfo key)
    {
        if (key.KeyChar is ' ' or '\n' || key.KeyChar >= '0')
        {
            if (BackTemp.Count != 0)
                BackTemp.Clear();
        }
        return false;
    }

    private bool HandleNavigation(ConsoleKeyInfo key)
    {
        // 如果没有内容可导航或在最上方无法上移
        if ((IsArrowKey(key.Key) && Units.Count == 0)
            || (key.Key == ConsoleKey.UpArrow && row == 0 && Editpage == 0))
        {
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.DownArrow:
                HandleDownArrow();
                return true;

            case ConsoleKey.UpArrow:
                HandleUpArrow();
                return true;

            case ConsoleKey.LeftArrow:
                HandleLeftArrow();
                return true;

            case ConsoleKey.RightArrow:
                HandleRightArrow();
                return true;

            case ConsoleKey.End:
                HandleEndKey();
                return true;

            case ConsoleKey.Home:
                HandleHomeKey();
                return true;

            case ConsoleKey.PageUp:
                HandlePageUp();
                return true;

            case ConsoleKey.PageDown:
                HandlePageDown();
                return true;
        }

        return false;
    }

    #region 小方法拆分

    // 判断是否为方向键
    private bool IsArrowKey(ConsoleKey key) =>
        key is ConsoleKey.DownArrow
            or ConsoleKey.UpArrow
            or ConsoleKey.LeftArrow
            or ConsoleKey.RightArrow;

    // 处理“↓”键
    private void HandleDownArrow()
    {
        isPreviewMode = false;
        row++;

        // 滚动到下一“编辑页”
        if (row >= Console.WindowHeight - 1)
        {
            Editpage++;
            row = 0;
            return;
        }

        // 在可见区域内上下移动
        if (row < Console.WindowHeight)
        {
            return;
        }

        AdjustCursorAfterDownMove();
    }

    // 处理“↑”键
    private void HandleUpArrow()
    {
        isPreviewMode = false;
        row--;

        // 滚动到上一“编辑页”
        if (row < 0 && Editpage > 0)
        {
            Editpage--;
            row = Console.WindowHeight - 1;

            if (Raw[EditRow].Length == 0)
            {
                col = TextAreaPaddingLeft;
            }
            return;
        }

        // 到达文档顶部后回退
        if (row < 0)
        {
            row++;
            return;
        }

        AdjustCursorAfterUpMove();
    }

    // 处理“←”键
    private void HandleLeftArrow()
    {
        isPreviewMode = false;
        col--;
        if (col < TextAreaPaddingLeft)
        {
            col = TextAreaPaddingLeft;
        }
    }

    // 处理“→”键
    private void HandleRightArrow()
    {
        isPreviewMode = false;
        col++;
        int maxCol = Raw[EditRow].Length + TextAreaPaddingLeft;
        if (col >= maxCol)
        {
            col = maxCol;
        }
    }

    // 处理“End”键
    private void HandleEndKey()
    {
        isPreviewMode = false;
        col = Raw[EditRow].Length + TextAreaPaddingLeft;
    }

    // 处理“Home”键
    private void HandleHomeKey()
    {
        isPreviewMode = false;
        col = TextAreaPaddingLeft;
    }

    // 处理“PageUp”键
    private void HandlePageUp()
    {
        if (isPreviewMode && Previewpage > 0)
        {
            Previewpage--;
        }
        else
        {
            Previewpage = Editpage - 1;
            isPreviewMode = true;
        }
    }

    // 处理“PageDown”键
    private void HandlePageDown()
    {
        if (isPreviewMode)
        {
            Previewpage++;
        }
        else
        {
            Previewpage = Editpage + 1;
            isPreviewMode = true;
        }
    }

    // 向下翻页后调整光标位置
    private void AdjustCursorAfterDownMove()
    {
        int length = Raw[EditRow].Length;
        if (length != 0 && length + TextAreaPaddingLeft < col)
        {
            VTConsole.CursorAbsoluteVertical(length - 2);
            col = length + TextAreaPaddingLeft;
        }
        else if (length == 0)
        {
            col = TextAreaPaddingLeft;
        }
    }

    // 向上翻页后调整光标位置
    private void AdjustCursorAfterUpMove()
    {
        int length = Raw[EditRow].Length;
        if (length != 0 && length + TextAreaPaddingLeft < col)
        {
            VTConsole.CursorAbsoluteVertical(length + TextAreaPaddingLeft);
            col = length + TextAreaPaddingLeft;
        }
        else if (length == 0)
        {
            col = TextAreaPaddingLeft;
        }
    }

    #endregion

    private bool HandleEdit(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                isPreviewMode = false;
                if (row == Console.WindowHeight - 1)
                {
                    Units.Add(new());
                    row = 0;
                    Editpage++;
                    col = tabCount * TabSize + TextAreaPaddingLeft;
                    currentString = Raw[EditRow];
                    return true;
                }
                if (col < currentString.Length + TextAreaPaddingLeft)
                {
                    string sub = currentString.Substring(col - TextAreaPaddingLeft);
                    string front = currentString[..(col - TextAreaPaddingLeft)];
                    var l1 = GetUnits(sub);
                    var l2 = GetUnits(front);
                    Units[EditRow] = l2;
                    Raw[EditRow] = front;
                    Raw[row + 1] = sub;
                    if (Units.Count - 1 < row + 1)
                    {
                        Units.Add(Enumerable.Repeat(new KeyUnit() { RawString = "    ", Type = KeyType.Text }, tabCount).ToList());
                        Raw.Add(string.Join(string.Empty, Enumerable.Repeat("    ", tabCount)));
                        Units.Add(l1);
                    }
                    else
                    {
                        Raw.Insert(row + 1, string.Join(string.Empty, Enumerable.Repeat("    ", tabCount)));
                        Units.Insert(row + 1, Enumerable.Repeat(new KeyUnit() { RawString = "    ", Type = KeyType.Text }, tabCount).ToList());
                        Units[row + 1].AddRange(l1);
                    }
                    row++;
                    col = tabCount * TabSize + TextAreaPaddingLeft;
                    currentString = Raw[EditRow];
                    return true;
                }
                else if (col == TextAreaPaddingLeft)
                {
                    if (Units.Count <= row + Console.WindowHeight * Editpage + 1)
                    {
                        Units.Add(Enumerable.Repeat(new KeyUnit() { RawString = "    ", Type = KeyType.Text }, tabCount).ToList());
                        Raw.Add(string.Join(string.Empty, Enumerable.Repeat("    ", tabCount)));
                    }
                    row++;
                    col = tabCount * TabSize + TextAreaPaddingLeft;
                    currentString = Raw[EditRow];
                    return true;
                }
                if (Units.Count < row + 1)
                {
                    Units.Add(new());
                    Raw.Insert(row + 1, string.Join(string.Empty, Enumerable.Repeat("    ", tabCount)));
                    Units.Insert(row + 1, Enumerable.Repeat(new KeyUnit() { RawString = "    ", Type = KeyType.Text }, tabCount).ToList());
                    row++;
                    col = tabCount * TabSize + TextAreaPaddingLeft;
                    currentString = Raw[EditRow];
                    return true;
                }
                else if (Units.Count >= row + 1)
                {
                    Raw.Insert(row + 1, string.Join(string.Empty, Enumerable.Repeat("    ", tabCount)));
                    Units.Insert(row + 1, Enumerable.Repeat(new KeyUnit() { RawString = "    ", Type = KeyType.Text }, tabCount).ToList());
                    row++;
                    col = tabCount * TabSize + TextAreaPaddingLeft;
                    currentString = Raw[EditRow];
                }
                col = tabCount * TabSize + TextAreaPaddingLeft;
                return true;
            case ConsoleKey.Tab:
                isPreviewMode = false;
                if (Units.Count <= row)
                {
                    Units.Add(new());
                    Raw.Insert(EditRow, string.Empty);
                }
                if (Units[EditRow].Count == 0)
                {
                    Units[EditRow].Add(MakeUnit("    ", KeyType.Text));
                    Raw[EditRow] += "    ";
                }
                else if ((col - TextAreaPaddingLeft) % TabSize == 0)
                {
                    Units[EditRow].Add(MakeUnit("    ", KeyType.Text));
                    Raw[EditRow] = Raw[EditRow].Insert(col - TextAreaPaddingLeft, "    ");
                }
                else if ((col - TextAreaPaddingLeft) % TabSize != 0)
                {
                    return true;
                }
                else if (IsOnlyType(Raw[EditRow][..(col - TextAreaPaddingLeft)], new(@"\s+")))
                {
                    Units[EditRow].Add(MakeUnit("    ", KeyType.Text));
                    Raw[EditRow] = Raw[EditRow].Insert(col - TextAreaPaddingLeft, "    ");
                }
                col += TabSize;
                tabCount++;
                return true;
            case ConsoleKey.Backspace:
            case ConsoleKey.Delete:
                return HandleBackspaceDelete(key);
        }
        return false;
    }

    private bool HandleBackspaceDelete(ConsoleKeyInfo key)
    {
        isPreviewMode = false;
        if (col == TextAreaPaddingLeft && row == 0 && Editpage > 0)
        {
            if (Raw[EditRow].Length == 0)
            {
                Units.RemoveAt(EditRow);
                if (Raw[row + Editpage * Console.WindowHeight - 1].Length > 0)
                {
                    col = Raw[row - 1].Length + TextAreaPaddingLeft;
                }
            }
            row = Console.WindowHeight - 1;
            Editpage--;
            return true;
        }

        if (col != TextAreaPaddingLeft && (col - TextAreaPaddingLeft) % TabSize == 0 && Units[EditRow].Count >= (col == TextAreaPaddingLeft ? 0 : (col - TextAreaPaddingLeft) / TabSize) && Units[EditRow][(col - TextAreaPaddingLeft) / TabSize - 1].RawString == "    ")
        {
            if (tabCount > 0)
                tabCount--;
            Units[EditRow].RemoveAt((col - TextAreaPaddingLeft) / TabSize - 1);
            Raw[EditRow] = Raw[EditRow].Remove((col - TextAreaPaddingLeft) / TabSize - 1, TabSize);
            col -= TabSize;
            return true;
        }
        if (row == 0 && currentString.Length == 0)
        {
            if (Units.Count == 0)
                return true;
            Units.Remove(Units[EditRow]);
            return true;
        }
        else if (col == TextAreaPaddingLeft)
        {
            if (Raw[EditRow].Length == 0 && row > 0)
            {
                Units.RemoveAt(EditRow);
                row--;
                if (Raw[EditRow].Length > 0)
                {
                    col = Raw[EditRow].Length + TextAreaPaddingLeft;
                }
                return true;
            }
            string bf = Raw[row - 1];
            bf += Raw[EditRow];
            Raw[EditRow] = string.Empty;
            if (row < Units.Count)
                Units.RemoveAt(EditRow);
            Raw[row - 1] = bf;
            currentString = bf;
            row--;
            col = bf.Length + TextAreaPaddingLeft;
            var l = GetUnits(currentString);
            Units[EditRow] = l;
            return true;
        }
        if (currentString.Length == 0)
        {
            if (Units.Count - 1 >= row && Units[EditRow] != null)
            {
                Units[EditRow] = null;
                Units.Remove(Units[EditRow]);
            }
            col = Raw[row - 1].Length + TextAreaPaddingLeft;
            row--;
            return true;
        }
        else if (col < currentString.Length + TextAreaPaddingLeft)
        {
            currentString = currentString.Remove(col - 7, 1);
            col--;
        }
        else
        {
            currentString = currentString[..^1];
            col--;
        }
        UpdateLine();
        return true;
    }

    private void UpdateLine()
    {
        List<KeyUnit> ku = GetUnits(currentString);
        if (Units.Count - 1 < row)
            Units.Add(new());
        Units[EditRow] = ku;
        Raw[EditRow] = currentString;
        if (!ku.Exists((x) => x.Type == KeyType.Text))
            History.Push((Units, Raw, EditRow, col, Editpage));
    }

    [DebuggerStepThrough]
    public KeyUnit MakeUnit(string rawString, KeyType keyType)
        => new() { Type = keyType, RawString = rawString };

    public bool IsOnlyType(string str, Regex r)
        => r.Match(str).Value.Length == str.Length;

    public List<KeyUnit> GetUnits(string currentString, bool hasCommand = true)
        => GetUnits(currentString, Keys, TabSize, hasCommand);

    /// <summary>
    /// Converts a string into a list of syntax-highlighted KeyUnit objects based on the defined rules.
    /// </summary>
    /// <param name="currentString">The string to parse</param>
    /// <param name="keys">Dictionary of keywords with their corresponding KeyUnit</param>
    /// <param name="tabSize">Number of spaces per tab (4)</param>
    /// <param name="hasCommand">Whether to treat the first token as a command</param>
    /// <returns>List of KeyUnit objects representing the syntax components</returns>
    public List<KeyUnit> GetUnits(string currentString, Dictionary<string, KeyUnit> keys, int tabSize, bool hasCommand = true)
    {
        List<KeyUnit> ku = new();

        // 1. Process leading spaces into tabs and remaining spaces
        ProcessLeadingSpaces(ku, currentString, tabSize);

        // 2. Split the string into tokens (parts separated by spaces)
        StringSplit split = new(currentString, ' ', true);

        // 3. Handle the first token if we're parsing commands
        if (hasCommand)
        {
            HandleFirstCommandToken(ku, split, keys);
        }

        // 4. Process remaining tokens (after the command) with their parenthesis types
        ProcessRemainingTokens(ku, split, keys, tabSize);

        return ku;
    }

    /// <summary>
    /// Processes leading spaces in the string and converts them into tab units.
    /// </summary>
    private void ProcessLeadingSpaces(List<KeyUnit> ku, string currentString, int tabSize)
    {
        if (string.IsNullOrEmpty(currentString))
            return;

        // Get leading whitespace using regex
        var leadingMatch = StartWhiteSpace().Match(currentString);
        int leadingSpacesCount = leadingMatch.Length;

        // Calculate how many full tabs and remaining spaces there are
        int tabCount = leadingSpacesCount / tabSize;
        int remainingSpaces = leadingSpacesCount % tabSize;

        // Add tab units for each full tab size
        for (int i = 0; i < tabCount; i++)
        {
            ku.Add(MakeUnit("    ", KeyType.Text));
        }

        // Add remaining spaces if any
        if (remainingSpaces > 0)
        {
            ku.Add(MakeUnit(new string(' ', remainingSpaces), KeyType.Text));
        }

        // Remove processed leading spaces from the string
        currentString = currentString.Substring(leadingSpacesCount);
    }

    /// <summary>
    /// Handles the first token when hasCommand is true.
    /// </summary>
    private void HandleFirstCommandToken(List<KeyUnit> ku, StringSplit split, Dictionary<string, KeyUnit> keys)
    {
        // If no tokens after splitting, return empty list
        if (split.SplitUnit.Length == 0)
            return;

        // Check if first token is a known command
        if (keys.TryGetValue(split[0].Trim(), out KeyUnit value))
        {
            ku.Add(value);
        }
        else
        {
            ku.Add(MakeUnit(split[0], KeyType.Text));
        }

        // Add a space separator if it's not the end of the string
        if (split.SplitUnit.Length > 1)
        {
            ku.Add(MakeUnit(split.Tokens[1].TokenString, KeyType.Split));
        }
    }

    /// <summary>
    /// Processes remaining tokens after the command token.
    /// </summary>
    private void ProcessRemainingTokens(List<KeyUnit> ku, StringSplit split, Dictionary<string, KeyUnit> keys, int tabSize)
    {
        int startIndex = split.SplitUnit.Length > 0 ? 2 : 0;

        // Process each remaining token
        for (long index = startIndex; index < split.Tokens.Count; index++)
        {
            SplitToken tokenRaw = split[index];
            ProcessSingleToken(ku, keys, tabSize, tokenRaw);
        }
    }

    private bool CheckInnering(string token)
    {
        int checkCount = 0;

        int currentIndex = 0;
        foreach (char c in token)
        {
            if (c == '(' || c == '[' || c == '{')
            {
                checkCount++;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                checkCount--;
                if (checkCount == 0 && currentIndex < token.Length - 1)
                    return true;
            }
            currentIndex++;
        }
        return false;
    }

    private void ProcessSingleToken(List<KeyUnit> ku, Dictionary<string, KeyUnit> keys, int tabSize, SplitToken tokenRaw, bool checkInner = false)
    {
        if(checkInner)
        {
            if (CheckInnering(tokenRaw.TokenString))
            {
                ku.Add(MakeUnit(tokenRaw.TokenString, KeyType.Text));
                return;
            }
        }

        if (tokenRaw.Type == SplitTokenType.SplitSeparator)
        {
            ku.Add(MakeUnit(tokenRaw.TokenString, KeyType.Split));
            return;
        }

        string token = tokenRaw.TokenString;
        // Determine parenthesis type of the token
        var part = StrParenthesis.GetStringParenthesisType(token);

        if (part == ParenthesisType.Unknown)
        {
            // If the token is not a parenthesis, treat it as text
            ProcessUnknownArguemnt(ku, token, keys);
            return;
        }

        // If the token is a half parenthesis, we need to handle it differently
        if (part.HasFlag(ParenthesisType.Half))
        {
            ku.Add(MakeUnit(token, KeyType.Parenthesis));
            return;
        }

        string parContent = token[1..(part.HasFlag(ParenthesisType.Half) ? ^0 : ^1)];

        switch (part.HasFlag(ParenthesisType.Half) ? part ^ ParenthesisType.Half : part)
        {
            case ParenthesisType.Big:
                ku.Add(new() { RawString = token, Type = KeyType.Type });
                break;

            case ParenthesisType.Middle:
                ProcessMiddleParenthesis(ku, token, part, keys);
                break;

            case ParenthesisType.Small:
                ProcessSmallParenthesis(ku, token, part, keys);
                break;

            case ParenthesisType.Sharp:
                ProcessSharpParenthesis(ku, token, part, keys, tabSize);
                break;

            default:
                ku.Add(MakeUnit(token, KeyType.Text));
                break;
        }
    }

    private void ProcessUnknownArguemnt(List<KeyUnit> ku, string token, Dictionary<string, KeyUnit> keys)
    {
        List<KeyUnit> result = new();
        if (StrParenthesis.GetCharHalfParenthesisType(token[0]) is ParenthesisType t && t.HasFlag(ParenthesisType.Unknown))
        {
            ku.Add(MakeUnit(token, KeyType.Text));
            return;
        }

        result.Add(MakeUnit(token[0].ToString(), KeyType.Parenthesis));
        if(t.HasFlag(ParenthesisType.Right))
        {
            result.Add(MakeUnit(token[1..], KeyType.Text));
            ku.AddRange(result);
            return;
        }


        ParenthesisType? lastToken = StrParenthesis.GetStringParenthesisType(token[^1].ToString()) is ParenthesisType t1 && t1 != ParenthesisType.Unknown ?
            t1 ^ ParenthesisType.Half : null;

        switch (StrParenthesis.GetStringParenthesisType(token[0].ToString()) ^ ParenthesisType.Half)
        {
            case ParenthesisType.Small:
                if (lastToken.HasValue)
                {
                    if (lastToken.Value == ParenthesisType.Small)
                    {
                        if (keys.TryGetValue(token[1..^1].Trim(), out KeyUnit variable) && variable.Type == KeyType.CritialVariable)
                            result.Add(MakeUnit(token[1..^1], KeyType.CritialVariable));
                        else
                            result.Add(MakeUnit(token[1..^1], KeyType.Variable));
                        if (lastToken.HasValue)
                            result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                    }
                    else
                    {
                        result.Clear();
                        result.Add(MakeUnit(token, KeyType.Text));
                    }
                }
                else
                {
                    if (keys.TryGetValue(token[1..].Trim(), out KeyUnit variable) && variable.Type == KeyType.CritialVariable)
                        result.Add(MakeUnit(token[1..], KeyType.CritialVariable));
                    else
                        result.Add(MakeUnit(token[1..], KeyType.Variable));
                    if (lastToken.HasValue)
                        result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                }
                break;
            case ParenthesisType.Middle:
                if (lastToken.HasValue)
                {
                    if (lastToken.Value == ParenthesisType.Middle)
                    {
                        ProcessIncompleteMiddleParenthesis(result, token[1..^1], keys);
                        if (lastToken.HasValue)
                            result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                    }
                    else
                    {
                        result.Clear();
                        result.Add(MakeUnit(token, KeyType.Text));
                    }
                }
                else
                {
                    ProcessIncompleteMiddleParenthesis(result, token[1..], keys);
                    if (lastToken.HasValue)
                        result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                }
                break;
            case ParenthesisType.Big:
                if (lastToken.HasValue)
                {
                    if (lastToken.Value == ParenthesisType.Big)
                    {
                        result.Add(MakeUnit(token[1..^1], KeyType.Type));
                        if (lastToken.HasValue)
                            result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                    }
                    else
                    {
                        result.Clear();
                        result.Add(MakeUnit(token, KeyType.Text));
                    }
                }
                else
                {
                    result.Add(MakeUnit(token[1..], KeyType.Type));
                    if (lastToken.HasValue)
                        result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                }
                break;
            case ParenthesisType.Sharp:
                if (lastToken.HasValue)
                {
                    ProcessIncompleteSharpParenthesis(result, token[1..^1], keys);
                    if (lastToken.HasValue)
                        result.Add(MakeUnit(token[^1].ToString(), KeyType.Parenthesis));
                }
                else
                {
                    ProcessIncompleteSharpParenthesis(result, token[1..], keys);
                }
                break;
        }

        ku.AddRange(result);
    }

    private void ProcessIncompleteMiddleParenthesis(List<KeyUnit> ku, string token, Dictionary<string, KeyUnit> keys)
    {
        StringBuilder sb = new();

        bool isFlagFlag = false;
        int count = 0;

        KeyType? valueType = null;
        foreach (char c in token)
        {
            if (c == ':')
            {
                string current = sb.ToString();
                if (current.Trim() == "flag" && count <= 1)
                {
                    if (isFlagFlag)
                    {
                        ku.Add(MakeUnit(current, KeyType.Tag));
                        goto ProcessIncompleteMiddleParenthesisForeachEnd;
                    }
                    else if (count == 0)
                    {
                        isFlagFlag = true;
                        ku.Add(MakeUnit(current, KeyType.KnownType));
                        goto ProcessIncompleteMiddleParenthesisForeachEnd;
                    }
                }
                if (count == 0)
                {
                    if (keys.TryGetValue(current.Trim(), out var unit) && unit.Type == KeyType.KnownType)
                    {
                        ku.Add(MakeUnit(current, KeyType.KnownType));
                        valueType = unit.ConstantType;
                    }
                    else
                    {
                        ku.Add(MakeUnit(current, KeyType.Type));
                    }
                }
                else
                {
                    sb.Append(c);
                    continue;
                }

            ProcessIncompleteMiddleParenthesisForeachEnd:
                count++;
                sb.Clear();

                ku.Add(MakeUnit(":", KeyType.Symbol));
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length == 0)
            return;

        if (count == 0)
        {
            if (keys.TryGetValue(sb.ToString().Trim(), out var unit) && unit.Type == KeyType.KnownType)
            {
                ku.Add(MakeUnit(sb.ToString(), KeyType.KnownType));
            }
            else
            {
                ku.Add(MakeUnit(sb.ToString(), KeyType.Type));
            }
        }
        else if (count == 1)
        {
            if (isFlagFlag)
                ku.Add(MakeUnit(sb.ToString(), KeyType.Tag));
            else
            {
                var inCompletedUnits = new StringSplitEx(token, ':', 2).SplitUnit;
                if (valueType.HasValue && inCompletedUnits.Count == 2 && valueType.Value != KeyType.Unknown && CodeHighlightRuleSet.ValueVaildRegularExpressions[valueType.Value].Match(inCompletedUnits[1].Trim()).Value.Length == inCompletedUnits[1].Trim().Length)
                {
                    ku.Add(MakeUnit(sb.ToString(), valueType.Value));
                }
                else
                {
                    ku.Add(MakeUnit(sb.ToString(), KeyType.Text));
                }
            }
        }
        else if(count == 2)
        {
            ku.Add(MakeUnit(sb.ToString(), KeyType.String));
        }
    }

    private void ProcessIncompleteSharpParenthesis(List<KeyUnit> ku, string token, Dictionary<string, KeyUnit> keys)
    {
        if (token.Length == 0)
            return;

        StringSplitEx parts = new(token, ':', 2);
        if (parts.SplitUnit.Count >= 1)
        {
            if (keys.TryGetValue(parts[0].Trim(), out KeyUnit tag) && tag.Type == KeyType.KnownType)
            {
                ku.Add(MakeUnit(parts[0], KeyType.KnownType));
            }
            else
            {
                ku.Add(MakeUnit(parts[0], KeyType.Type));
            }
        }
        if (parts.SplitUnit.Count == 2)
        {
            ku.Add(MakeUnit(":", KeyType.Symbol));
        }
        else
        {
            return;
        }

        ProcessLeadingSpaces(ku, parts[1], TabSize);

        string[] partElementsPre = parts[1].Split(',');

        StringWhiteSpaceAround[] partElementsSlices = Array.ConvertAll(partElementsPre, input =>
        {
            StringWhiteSpaceAround swa = new();
            int inputTrimedLen = input.Trim().Length;
            swa[0] = new(' ', input.TrimEnd().Length - inputTrimedLen);
            swa[1] = input.Trim();
            swa[2] = new(' ', input.TrimStart().Length - inputTrimedLen);
            return swa;
        });

        List<KeyUnit>[] elementKeyUnits = new List<KeyUnit>[partElementsSlices.Length];

        for(int i = 0; i < partElementsSlices.Length; i++)
        {
            StringWhiteSpaceAround element = partElementsSlices[i];
            List<KeyUnit> currentElementKeyUnits = new List<KeyUnit>();
            if (!string.IsNullOrEmpty(element[0]))
                currentElementKeyUnits.Add(MakeUnit(element[0], KeyType.Text));

            if (!string.IsNullOrEmpty(element[1]))
                ProcessSingleToken(currentElementKeyUnits, keys, TabSize, new SplitToken(element[1], SplitTokenType.Content), true);

            if (!string.IsNullOrEmpty(element[2]))
                currentElementKeyUnits.Add(MakeUnit(element[2], KeyType.Text));

            elementKeyUnits[i] = currentElementKeyUnits;
        };

        ku.AddRange(elementKeyUnits.FlatIndirectInsert(MakeUnit(",", KeyType.Split)));
    }

    [InlineArray(3)]
    struct StringWhiteSpaceAround { private string _inner; }



    /// <summary>
    /// Processes tokens with middle parentheses ([...]).
    /// </summary>
    private void ProcessMiddleParenthesis(List<KeyUnit> ku, string token, ParenthesisType part, Dictionary<string, KeyUnit> keys)
    {
        // Add opening bracket
        ku.Add(MakeUnit("[", KeyType.Parenthesis));

        // If token is just the opening bracket, add closing and return
        if (token.Length == 1)
        {
            return;
        }

        // Process content inside the brackets
        string content = token[1..^1];

        if (string.IsNullOrWhiteSpace(content))
        {
            if (content.Length > 0)
            {
                ku.Add(MakeUnit(content, KeyType.Text));
            }
            ku.Add(MakeUnit("]", KeyType.Parenthesis));
            return;
        }
        // Split content by colon
        var sp = new StringSplit(content, ':');

        // Process first part after colon
        if (keys.TryGetValue(sp[0].Trim(), out KeyUnit type) && type.Type == KeyType.KnownType)
        {
            ku.Add(MakeUnit(sp[0], KeyType.KnownType));
        }
        else
        {
            ku.Add(MakeUnit(sp[0], KeyType.Type));
        }

        if(sp.Tokens.Count <= 1)
        {
            ku.Add(MakeUnit("]", KeyType.Parenthesis));
            return;
        }

        // Add colon symbol
        ku.Add(MakeUnit(":", KeyType.Symbol));

        // Handle "flag" type specifically
        if (sp[0].Trim() == "flag")
        {
            ProcessFlagType(ku, token);
        }
        // For other types, check if they have constant type and validate content
        else if (keys.ContainsKey(sp[0].Trim()) &&
                 type.ConstantType.HasValue &&
                 type.ConstantType.Value != KeyType.Unknown &&
                 sp.SplitUnit.Length > 1)
        {
            var ct = type.ConstantType.Value;
            bool canParse = IsOnlyType(string.Concat(sp[1..]).Trim(), CodeHighlightRuleSet.ValueVaildRegularExpressions[ct]);
            ku.Add(MakeUnit(sp[1], canParse ? ct : KeyType.Text));
        }
        else if (sp.SplitUnit.Length > 1)
        {
            ku.Add(MakeUnit(string.Concat(sp[1..]), KeyType.Text));
        }
        ku.Add(MakeUnit("]", KeyType.Parenthesis));
    }

    /// <summary>
    /// Processes "flag" type tokens inside middle parentheses.
    /// </summary>
    private void ProcessFlagType(List<KeyUnit> ku, string token)
    {
        // Split the content after the first colon by another colon
        var sp2 = new StringSplitEx(token[1..^1], ':', 3);

        if(sp2.SplitUnit.Count >= 2)
        {
            // Add tag and symbol
            ku.Add(MakeUnit(sp2[1], KeyType.Tag));
        }
        
        if(sp2.SplitUnit.Count > 2)
        {
            ku.Add(MakeUnit(":", KeyType.Symbol));
            ku.Add(MakeUnit(sp2[2], KeyType.String));
        }
    }

    /// <summary>
    /// Processes tokens with small parentheses ((...)).
    /// </summary>
    private void ProcessSmallParenthesis(List<KeyUnit> ku, string token, ParenthesisType part, Dictionary<string, KeyUnit> keys)
    {
        // Add opening parenthesis
        ku.Add(MakeUnit("(", KeyType.Parenthesis));

        // Process content inside the parentheses
        string content = token[1..(part.HasFlag(ParenthesisType.Half) ? token.Length - 1 : token.Length - 1)];

        if (keys.TryGetValue(content.Trim(), out KeyUnit variable) && variable.Type == KeyType.CritialVariable)
        {
            ku.Add(MakeUnit(content, KeyType.CritialVariable));
        }
        else
        {
            ku.Add(MakeUnit(content, KeyType.Variable));
        }

        // Add closing parenthesis if needed
        if (!part.HasFlag(ParenthesisType.Half))
        {
            ku.Add(MakeUnit(")", KeyType.Parenthesis));
        }
    }

    /// <summary>
    /// Processes tokens with sharp parentheses (<...>).
    /// </summary>
    private void ProcessSharpParenthesis(List<KeyUnit> ku, string token, ParenthesisType part, Dictionary<string, KeyUnit> keys, int tabSize)
    {
        // Add opening angle bracket
        ku.Add(MakeUnit("<", KeyType.Parenthesis));

        ProcessIncompleteSharpParenthesis(ku, token[1..^1], keys);

        //// Split content by commas and process each item
        //var aisp = new StringSplit(token[1..^1], ',');

        //for (int aispi = 0; aispi < aisp.SplitUnit.Length; aispi++)
        //{
        //    string asu = aisp.SplitUnit[aispi];

        //    // Add leading whitespace if any
        //    var startMatch = StartWhiteSpace().Match(asu);
        //    if (startMatch.Success && startMatch.Length > 0)
        //    {
        //        ku.Add(MakeUnit(startMatch.Value, KeyType.Split));
        //        asu = asu.Substring(startMatch.Length);
        //    }

        //    // Process the actual content of the array element
        //    List<KeyUnit> arrItem = GetUnits(asu.Trim(' '), keys, tabSize, false);
        //    if (arrItem.Count > 0)
        //        ku.AddRange(arrItem);

        //    // Add trailing whitespace if any
        //    var endMatch = EndWhiteSpace().Match(asu);
        //    if (endMatch.Success && endMatch.Length > 0)
        //    {
        //        ku.Add(MakeUnit(endMatch.Value, KeyType.Split));
        //    }

        //    // Add comma if not the last element
        //    if (aispi < aisp.SplitUnit.Length - 1)
        //    {
        //        ku.Add(MakeUnit(",", KeyType.Symbol));
        //    }
        //}

        ku.Add(MakeUnit(">", KeyType.Parenthesis));
    }


    public List<string> GetContentArea()
    {
        List<string> line = new();
        int whitespacecount = 0;

        foreach (var l in Raw)
        {
            if (whitespacecount > 8)
            {
                line.RemoveRange(line.Count - 1 - 8, 8);
                break;
            }
            if (string.IsNullOrWhiteSpace(l))
            {
                whitespacecount++;
            }
            line.Add(l);
        }

        return line;
    }
    private TextWriter defaultConsoleBufferOut = Console.Out;

    public void Update(int page)
    {

        VTConsole.SetColorBackground((Color)ConfigStyle.ColorStyle[KeyType.Text].BackgroundColor);
        VTConsole.SetColorForeground((Color)ConfigStyle.ColorStyle[KeyType.Text].ForegroundColor);

        int start = page * Console.WindowHeight;
        int end = start + Console.WindowHeight;
        int line = 1 + start;
        if (start > Units.Count)
            return;

        StringWriter consoleBufferText = new();
        Console.SetOut(consoleBufferText);

        var list = Units ?? new();
        foreach (var l in list.ToArray()[start..Math.Min(end, list.Count)])
        {
            VTConsole.Write("      ");
            VTConsole.CursorAbsoluteHorizontal(1);
            VTConsole.Write(line.ToString());
            VTConsole.CursorAbsoluteHorizontal(TextAreaPaddingLeft + 1);
            foreach (var u in l)
            {
                if (ConfigStyle.ColorStyle[u.Type].ForegroundColor.Equals(CodeHighlightRuleSet._none))
                {
                    VTConsole.SetColorForeground((Color)CodeHighlightRuleSet._fore);
                }
                else
                {
                    VTConsole.SetColorForeground((Color)ConfigStyle.ColorStyle[u.Type].ForegroundColor);
                }

                if (ConfigStyle.ColorStyle[u.Type].BackgroundColor.Equals(CodeHighlightRuleSet._none))
                {
                    VTConsole.SetColorBackground((Color)CodeHighlightRuleSet._back);
                }
                else
                {
                    VTConsole.SetColorBackground((Color)ConfigStyle.ColorStyle[u.Type].BackgroundColor);
                }
                VTConsole.Write(u.RawString);
            }
            if (line != end)
                VTConsole.WriteLine();
            line++;
        }

        Console.SetOut(defaultConsoleBufferOut);

        Console.Clear();
        Console.Write("\x1b[3J");

        Console.SetCursorPosition(0, 0);
        Console.Write(consoleBufferText.ToString());

        consoleBufferText.Close();
    }

    [GeneratedRegex("^\\s+")]
    public static partial Regex StartWhiteSpace();

    [GeneratedRegex("$\\s+")]
    public static partial Regex EndWhiteSpace();
}

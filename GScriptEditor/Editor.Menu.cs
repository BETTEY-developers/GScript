using EUtility.ValueEx;
using System.Diagnostics;
using TrueColorConsole;
using GScript.Editor.Controls.Menu.Primitives;
using GScript.Editor.Controls.Menu;

namespace GScript.Editor;

internal partial class Editor
{
    public void ShowMenu(int page)
    {
        // 构建菜单项
        var menuGroups = new List<MenuGroup>
        {
            new MenuGroup("File", new List<IMenuItem>
            {
                new TextMenuItem("Open", () =>
                {
                    var item = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    if (item.IsVoid())
                        return;

                    FileInfo fi = item as FileInfo;
                    if (string.Concat(this.Raw).Length != 0)
                    {
                        long ync = 0x3L;
                        long infomationicon = 0x00000040L;

                        int result = Native.MessageBoxA(new(0), $"This editor has content, do you want to save this to {(string.IsNullOrEmpty(this.FileName) ? "untitled.gs" : this.FileName)}?", "Caption", (uint)(ync | infomationicon));

                        int yes = this.TextAreaPaddingLeft;
                        int no = 7;
                        int cancel = 2;

                        if (result == cancel)
                            return;

                        if (result == yes)
                        {
                            List<string> content = this.GetContentArea();
                            if (string.IsNullOrEmpty(this.FileName))
                            {
                                var file = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                                if (file.IsVoid())
                                    return;

                                File.WriteAllLines((file as FileInfo).FullName, content);
                            }
                            else
                            {
                                if (!File.Exists(this.FileName))
                                    File.Create(this.FileName).Close();
                                File.WriteAllLines(this.FileName, content);
                            }
                        }
                    }

                    var t = File.ReadAllLines(fi.FullName);
                    this.Units = new();
                    this.Raw = new();
                    for (int p = 0; p < 4000; p++)
                    {
                        this.Raw.Add(string.Empty);
                    }

                    this.Keys = new();
                    var all = this.ConfigStyle.GetList();
                    all.ForEach(x => this.Keys.Add(x.RawString, x));
                    int i = 0;
                    foreach (var line in t)
                    {
                        var lineunits = this.GetUnits(line);
                        this.Raw[i] = line;
                        this.Units.Add(lineunits);
                        i++;
                    }
                    this.FileName = fi.FullName;
                }),
                new SeparationItem(),
                new TextMenuItem("Save", () =>
                {
                    var content = this.GetContentArea();
                    if (string.IsNullOrEmpty(this.FileName))
                    {
                        var file = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                        if (file.IsVoid())
                            return;

                        File.WriteAllLines((file as FileInfo).FullName, content);
                    }
                    else
                    {
                        if (!File.Exists(this.FileName))
                            File.Create(this.FileName).Close();
                        File.WriteAllLines(this.FileName, content);
                    }
                }),
                new TextMenuItem("Save as", () =>
                {
                    var content = this.GetContentArea();

                    var file = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    if (file.IsVoid())
                        return;

                    FileInfo fi = file as FileInfo;
                    File.WriteAllLines(fi.FullName, content);
                    this.FileName = fi.FullName;
                })
            }),
            new MenuGroup("Run", new List<IMenuItem>
            {
                new TextMenuItem("Script", () =>
                {
                    var content = this.GetContentArea();
                    string path = this.ConfigStyle.InterpreterPath;
                    if (string.IsNullOrEmpty(this.FileName))
                    {
                        var file = ~FileList.FileDialog(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                        if (file.IsVoid())
                            return;

                        File.WriteAllLines((file as FileInfo).FullName, content);
                        this.FileName = (file as FileInfo).FullName;
                    }
                    else
                    {
                        if (!File.Exists(this.FileName))
                            File.Create(this.FileName).Close();
                        File.WriteAllLines(this.FileName, content);
                    }

                    Process.Start(path, new[] { "r", this.FileName });
                })
            })
        };

        // 创建菜单控件
        var menu = new TopMenu(menuGroups);

        // 弹出菜单并等待选择
        var result = menu.Ask(()=>Update(isPreviewMode ? Previewpage : Editpage));
    }
}

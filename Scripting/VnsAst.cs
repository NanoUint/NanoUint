namespace NanoUint.Scripting;

#region .vns AST节点类型
// .vns 文本脚本语言的 AST 节点类型。

/// <summary>源文件中的位置（行，列）</summary>
public readonly record struct SourceLocation(int Line, int Column, string FilePath = "")
{
    public override string ToString() => $"{FilePath}:{Line}:{Column}";
}

/// <summary>顶层 .vns 文档</summary>
public class VnsDocument
{
    public string FilePath { get; set; } = "";
    public List<VnsCompileDirective> Directives { get; set; } = new();
    public List<VnsBlock> Blocks { get; set; } = new();
    public List<string> Imports { get; set; } = new();
}

// ---- 编译时指令（以 @@ 为前缀） ----

public abstract class VnsCompileDirective { }

/// <summary>@@outline("章节名称") —— 文档大纲标题</summary>
public class OutlineDirective : VnsCompileDirective
{
    public string Title { get; set; } = "";
}

/// <summary>@@alias(short = target) —— 命令别名定义</summary>
public class AliasDirective : VnsCompileDirective
{
    public string ShortName { get; set; } = "";
    public string TargetCommand { get; set; } = "";
}

/// <summary>@@import("path/to/file.vns") —— 导入另一个脚本</summary>
public class ImportDirective : VnsCompileDirective
{
    public string Path { get; set; } = "";
}

// ---- 块 ----

public abstract class VnsBlock
{
    public SourceLocation Location;
}

/// <summary>#labelName —— 命名标签（跳转目标）</summary>
public class LabelBlock : VnsBlock
{
    public string Name { get; set; } = "";
}

/// <summary>普通文本行：旁白（: 文本）或角色对白（名称: 文本）</summary>
public class TextBlock : VnsBlock
{
    public string? Speaker { get; set; }       // null 表示旁白
    public string Text { get; set; } = "";
}

/// <summary>@commandName(arg1, arg2, key: value) —— 可执行命令</summary>
public class CommandBlock : VnsBlock
{
    public string CommandName { get; set; } = "";
    public List<VnsArgument> Arguments { get; set; } = new();
    public Dictionary<string, VnsValue> NamedArguments { get; set; } = new();
    public VnsFlowBindings? FlowBindings { get; set; }
}

/// <summary>@if / @elif / @else / @end —— 条件结构</summary>
public class IfBlock : VnsBlock
{
    public string? Condition { get; set; }   // null 表示 @else
    public List<VnsBlock> Body { get; set; } = new();
    public List<IfBlock> ElseIfs { get; set; } = new();
    public List<VnsBlock>? ElseBody { get; set; }
}

/// <summary>@choice() ... @end —— 玩家选择块</summary>
public class ChoiceBlock : VnsBlock
{
    public List<ChoiceOption> Options { get; set; } = new();
}

public class ChoiceOption
{
    public string Text { get; set; } = "";
    public string? TargetLabel { get; set; }
}

// ---- 参数与值 ----

public abstract class VnsArgument
{
    public abstract VnsValue Value { get; set; }
}

public class PositionalArg : VnsArgument
{
    public override VnsValue Value { get; set; } = new VnsNull();
}

public class NamedArg : VnsArgument
{
    public string Name { get; set; } = "";
    public override VnsValue Value { get; set; } = new VnsNull();
}

// ---- 值类型 ----

public abstract class VnsValue { }

public class VnsString : VnsValue
{
    public string Value { get; set; } = "";
    public override string ToString() => $"\"{Value}\"";
}

public class VnsBool : VnsValue
{
    public bool Value { get; set; }
    public override string ToString() => Value ? "true" : "false";
}

public class VnsNull : VnsValue
{
    public override string ToString() => "null";
}

public class VnsNumber : VnsValue
{
    public double Value { get; set; }
    public override string ToString() => Value.ToString();
}

public class VnsColor : VnsValue
{
    public string Hex { get; set; } = "#FFFFFFFF";
    public override string ToString() => Hex;
}

public class VnsVector2 : VnsValue
{
    public double X { get; set; }
    public double Y { get; set; }
    public override string ToString() => $"({X}, {Y})";
}

public class VnsLabelRef : VnsValue
{
    public string LabelName { get; set; } = "";
    public override string ToString() => $"#{LabelName}";
}

public class VnsVariable : VnsValue
{
    public string Scope { get; set; } = "";   // "$" 局部, "global." 全局, "temp." 临时
    public string Name { get; set; } = "";
    public override string ToString() => Scope == "$" ? $"${Name}" : $"{Scope}{Name}";
}

public class VnsResourceRef : VnsValue
{
    public string ResourceType { get; set; } = "";  // "texture", "audio", "style", "ui" 等
    public string Path { get; set; } = "";
    public override string ToString() => $"&{ResourceType}(\"{Path}\")";
}

// ---- 流程绑定（-> output:target） ----

public class VnsFlowBindings
{
    public string? DefaultTarget { get; set; }                        // -> #label
    public Dictionary<string, string> NamedTargets { get; set; } = new();  // -> output:#label
    public Dictionary<string, string> VariableBindings { get; set; } = new(); // -> output:$var
}

// ---- 编译结果 ----

/// <summary>编译后的单个可执行脚本步骤</summary>
public class ScriptStep
{
    public SourceLocation Location;
    public ScriptStepType Type { get; set; }
    public string? Label { get; set; }                 // 如果此步骤是标签定义
    public string? CommandName { get; set; }           // 用于命令步骤
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public string? Text { get; set; }                  // 用于文本步骤（旁白/对白）
    public string? Speaker { get; set; }               // 用于文本步骤
    public List<string>? Choices { get; set; }         // 用于选择步骤
    public List<ScriptStep>? IfBody { get; set; }      // 用于条件块
    public List<ScriptStep>? ElseBody { get; set; }
    public string? Condition { get; set; }             // 用于 if/elif
    public List<(string Condition, List<ScriptStep> Body)>? ElseIfs { get; set; }
}

public enum ScriptStepType
{
    Label,
    Text,
    Command,
    Choice,
    Jump,
    If,
    EndIf
}

#endregion

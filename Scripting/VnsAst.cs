namespace NanoUint.Scripting;

// ============================================================
// AST node types for the .vns text script language.
// Mirrors VoidNovelEngine's script structure.
// ============================================================

/// <summary>Position in source file (line, column)</summary>
public readonly record struct SourceLocation(int Line, int Column, string FilePath = "")
{
    public override string ToString() => $"{FilePath}:{Line}:{Column}";
}

/// <summary>Top-level .vns document</summary>
public class VnsDocument
{
    public string FilePath { get; set; } = "";
    public List<VnsCompileDirective> Directives { get; set; } = new();
    public List<VnsBlock> Blocks { get; set; } = new();
    public List<string> Imports { get; set; } = new();
}

// ---- Compile-time directives (prefixed with @@) ----

public abstract class VnsCompileDirective { }

/// <summary>@@outline("Chapter Name") — document outline title</summary>
public class OutlineDirective : VnsCompileDirective
{
    public string Title { get; set; } = "";
}

/// <summary>@@alias(short = target) — command alias definition</summary>
public class AliasDirective : VnsCompileDirective
{
    public string ShortName { get; set; } = "";
    public string TargetCommand { get; set; } = "";
}

/// <summary>@@import("path/to/file.vns") — import another script</summary>
public class ImportDirective : VnsCompileDirective
{
    public string Path { get; set; } = "";
}

// ---- Blocks ----

public abstract class VnsBlock
{
    public SourceLocation Location;
}

/// <summary>#labelName — a named label (jump target)</summary>
public class LabelBlock : VnsBlock
{
    public string Name { get; set; } = "";
}

/// <summary>A normal text line: either narration (: text) or character dialogue (Name: text)</summary>
public class TextBlock : VnsBlock
{
    public string? Speaker { get; set; }       // null for narration
    public string Text { get; set; } = "";
}

/// <summary>@commandName(arg1, arg2, key: value) — executable command</summary>
public class CommandBlock : VnsBlock
{
    public string CommandName { get; set; } = "";
    public List<VnsArgument> Arguments { get; set; } = new();
    public Dictionary<string, VnsValue> NamedArguments { get; set; } = new();
    public VnsFlowBindings? FlowBindings { get; set; }
}

/// <summary>@if / @elif / @else / @end — conditional structure</summary>
public class IfBlock : VnsBlock
{
    public string? Condition { get; set; }   // null for @else
    public List<VnsBlock> Body { get; set; } = new();
    public List<IfBlock> ElseIfs { get; set; } = new();
    public List<VnsBlock>? ElseBody { get; set; }
}

/// <summary>@choice() ... @end — player choice block</summary>
public class ChoiceBlock : VnsBlock
{
    public List<ChoiceOption> Options { get; set; } = new();
}

public class ChoiceOption
{
    public string Text { get; set; } = "";
    public string? TargetLabel { get; set; }
}

// ---- Arguments & Values ----

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

// ---- Value types ----

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
    public string Scope { get; set; } = "";   // "$" local, "global." global, "temp." temp
    public string Name { get; set; } = "";
    public override string ToString() => Scope == "$" ? $"${Name}" : $"{Scope}{Name}";
}

public class VnsResourceRef : VnsValue
{
    public string ResourceType { get; set; } = "";  // "texture", "audio", "style", "ui", etc.
    public string Path { get; set; } = "";
    public override string ToString() => $"&{ResourceType}(\"{Path}\")";
}

// ---- Flow bindings (-> output:target) ----

public class VnsFlowBindings
{
    public string? DefaultTarget { get; set; }                        // -> #label
    public Dictionary<string, string> NamedTargets { get; set; } = new();  // -> output:#label
    public Dictionary<string, string> VariableBindings { get; set; } = new(); // -> output:$var
}

// ---- Compiled result ----

/// <summary>A single executable script step after compilation</summary>
public class ScriptStep
{
    public SourceLocation Location;
    public ScriptStepType Type { get; set; }
    public string? Label { get; set; }                 // If this step is a label definition
    public string? CommandName { get; set; }           // For Command steps
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public string? Text { get; set; }                  // For Text steps (narration/dialogue)
    public string? Speaker { get; set; }               // For Text steps
    public List<string>? Choices { get; set; }         // For Choice steps
    public List<ScriptStep>? IfBody { get; set; }      // For conditional blocks
    public List<ScriptStep>? ElseBody { get; set; }
    public string? Condition { get; set; }             // For if/elif
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

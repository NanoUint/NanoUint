namespace NanoUint.Scripting;

/// <summary>
/// Recursive-descent parser for .vns script files.
/// Takes tokens from the lexer and builds a VnsDocument AST.
/// </summary>
public class VnsParser
{
    private readonly List<Token> _tokens;
    private int _pos;
    private readonly string _filePath;

    public List<string> Errors { get; } = new();

    public VnsParser(List<Token> tokens, string filePath = "")
    {
        _tokens = tokens;
        _filePath = filePath;
    }

    public VnsDocument Parse()
    {
        var doc = new VnsDocument { FilePath = _filePath };
        ParseDirectives(doc);
        ParseBlocks(doc);
        return doc;
    }

    // ---- Directives (top of file only) ----

    private void ParseDirectives(VnsDocument doc)
    {
        while (!IsAtEnd() && (Current.Type == TokenType.DoubleAt || Current.Type == TokenType.Comment || Current.Type == TokenType.NewLine))
        {
            if (Current.Type == TokenType.NewLine || Current.Type == TokenType.Comment)
            {
                Advance();
                continue;
            }
            if (Current.Type == TokenType.DoubleAt)
            {
                var dir = ParseDirective();
                if (dir != null)
                {
                    if (dir is ImportDirective imp) doc.Imports.Add(imp.Path);
                    else doc.Directives.Add(dir);
                }
            }
        }
    }

    private VnsCompileDirective? ParseDirective()
    {
        Expect(TokenType.DoubleAt);
        if (!Check(TokenType.Identifier)) { Error("Expected directive name after @@"); return null; }
        var name = Advance().Value;

        Expect(TokenType.LParen);
        switch (name)
        {
            case "outline":
                var outline = new OutlineDirective();
                if (CheckNamed("name") || CheckNamed("title")) { Advance(); Expect(TokenType.Equals); } // skip name=
                if (Check(TokenType.String)) outline.Title = Advance().Value;
                Expect(TokenType.RParen);
                return outline;

            case "alias":
                var alias = new AliasDirective();
                if (Check(TokenType.Identifier)) alias.ShortName = Advance().Value;
                Expect(TokenType.Equals);
                if (Check(TokenType.Identifier)) alias.TargetCommand = Advance().Value;
                Expect(TokenType.RParen);
                return alias;

            case "import":
                var import = new ImportDirective();
                if (CheckNamed("target") || CheckNamed("path") || CheckNamed("file")) { Advance(); Expect(TokenType.Equals); }
                if (Check(TokenType.String)) import.Path = Advance().Value;
                Expect(TokenType.RParen);
                return import;

            default:
                SkipTo(TokenType.RParen);
                return null;
        }
    }

    // ---- Blocks ----

    private void ParseBlocks(VnsDocument doc)
    {
        SkipNewlines();
        while (!IsAtEnd())
        {
            var block = ParseBlock();
            if (block != null) doc.Blocks.Add(block);
            SkipNewlines();
        }
    }

    private VnsBlock? ParseBlock()
    {
        return Current.Type switch
        {
            TokenType.Hash => ParseLabel(),
            TokenType.AtSign => ParseCommandOrIfOrChoice(),
            TokenType.Colon => ParseText(),
            TokenType.Identifier => ParseTextOrLabel(),
            TokenType.Comment => Consume(),
            TokenType.NewLine => Consume(),
            _ => Consume()
        };
    }

    private LabelBlock ParseLabel()
    {
        var loc = Loc();
        Expect(TokenType.Hash);
        var name = Expect(TokenType.Identifier).Value;
        return new LabelBlock { Name = name, Location = loc };
    }

    private VnsBlock? ParseTextOrLabel()
    {
        // Look ahead: if identifier is followed by ':', it's a character dialogue line
        var saved = _pos;
        var ident = Expect(TokenType.Identifier);
        if (Check(TokenType.Colon))
        {
            Advance(); // ':'
            SkipWhitespaceTokens();
            string text = "";
            if (Check(TokenType.String)) text = Advance().Value;
            else if (Check(TokenType.Identifier))
            {
                // Read rest of line as text
                text = ReadRestOfLine();
            }
            return new TextBlock { Speaker = ident.Value, Text = text, Location = Loc() };
        }
        else
        {
            // It's just a bare identifier — treat as text or ignore
            _pos = saved;
            return null;
        }
    }

    private TextBlock ParseText()
    {
        var loc = Loc();
        Expect(TokenType.Colon);
        SkipWhitespaceTokens();
        string text = "";
        if (Check(TokenType.String)) text = Advance().Value;
        else if (!Check(TokenType.NewLine) && !Check(TokenType.Eof)) text = ReadRestOfLine();
        return new TextBlock { Speaker = null, Text = text, Location = loc };
    }

    private VnsBlock? ParseCommandOrIfOrChoice()
    {
        Expect(TokenType.AtSign);
        SkipNewlines(); // in case of stray newline after @

        var cmdName = Expect(TokenType.Identifier).Value;

        return cmdName switch
        {
            "if" => ParseIf(),
            "elif" => null, // handled inside ParseIf
            "else" => null,
            "end" => null,
            "choice" => ParseChoice(),
            "jump" => ParseSimpleJump(cmdName),
            _ => ParseCommand(cmdName)
        };
    }

    private IfBlock ParseIf()
    {
        var loc = Loc();
        // Parse condition
        string? condition = null;
        if (Check(TokenType.LParen))
        {
            Advance();
            condition = ReadCondition();
            Expect(TokenType.RParen);
        }

        SkipNewlines();
        var body = ParseInnerBlocks("elif", "else", "end");
        var block = new IfBlock { Condition = condition, Body = body, Location = loc };

        // Parse elifs
        while (Check(TokenType.AtSign) && PeekValue(1) == "elif")
        {
            Advance(); Advance(); // @ elif
            string elifCond = "";
            if (Check(TokenType.LParen)) { Advance(); elifCond = ReadCondition(); Expect(TokenType.RParen); }
            SkipNewlines();
            var elifBody = ParseInnerBlocks("elif", "else", "end");
            block.ElseIfs.Add(new IfBlock { Condition = elifCond, Body = elifBody });
        }

        // Parse else
        if (Check(TokenType.AtSign) && PeekValue(1) == "else")
        {
            Advance(); Advance(); // @ else
            SkipNewlines();
            block.ElseBody = ParseInnerBlocks("elif", "else", "end");
        }

        // Consume @end
        if (Check(TokenType.AtSign) && PeekValue(1) == "end") { Advance(); Advance(); }
        SkipNewlines();

        return block;
    }

    private ChoiceBlock ParseChoice()
    {
        var loc = Loc();
        // Optional parens with prompt
        if (Check(TokenType.LParen))
        {
            while (!Check(TokenType.RParen) && !IsAtEnd()) Advance();
            if (Check(TokenType.RParen)) Advance();
        }

        SkipNewlines();
        var choice = new ChoiceBlock { Location = loc };

        while (!IsAtEnd() && !(Check(TokenType.AtSign) && PeekValue(1) == "end"))
        {
            if (Check(TokenType.Dash))
            {
                Advance();
                // Parse option: - "Text" -> #Target
                string optText = "";
                if (Check(TokenType.String)) optText = Advance().Value;
                else optText = ReadRestOfLine().Trim();

                string? target = null;
                if (Check(TokenType.Arrow))
                {
                    Advance();
                    if (Check(TokenType.Hash)) { Advance(); target = Expect(TokenType.Identifier).Value; }
                }

                choice.Options.Add(new ChoiceOption { Text = optText, TargetLabel = target });
            }
            else if (Check(TokenType.NewLine) || Check(TokenType.Comment))
            {
                Advance();
            }
            else
            {
                Advance(); // skip unknown
            }
        }

        // Consume @end
        if (Check(TokenType.AtSign)) { Advance(); if (Check(TokenType.Identifier) && Current.Value == "end") Advance(); }

        return choice;
    }

    private CommandBlock ParseSimpleJump(string cmdName)
    {
        var cmd = new CommandBlock { CommandName = cmdName, Location = Loc() };
        if (Check(TokenType.LParen))
        {
            Advance();
            if (Check(TokenType.Hash)) { Advance(); } // skip #
            if (Check(TokenType.Identifier)) cmd.Arguments.Add(new PositionalArg { Value = new VnsLabelRef { LabelName = Advance().Value } });
            Expect(TokenType.RParen);
        }
        return cmd;
    }

    private CommandBlock ParseCommand(string cmdName)
    {
        var cmd = new CommandBlock { CommandName = cmdName, Location = Loc() };
        if (Check(TokenType.LParen))
        {
            Advance();
            ParseCommandArgs(cmd);
            Expect(TokenType.RParen);
        }

        // Parse flow bindings (-> output:#label or -> $var)
        ParseFlowBindings(cmd);

        return cmd;
    }

    private void ParseCommandArgs(CommandBlock cmd)
    {
        while (!Check(TokenType.RParen) && !IsAtEnd())
        {
            if (Check(TokenType.Comma)) { Advance(); continue; }

            // Named arg: key: value or key = value
            if (Check(TokenType.Identifier) && (PeekType(1) == TokenType.Colon || PeekType(1) == TokenType.Equals))
            {
                var name = Advance().Value;
                Advance(); // : or =
                var val = ParseValue();
                cmd.NamedArguments[name] = val;
            }
            else
            {
                var val = ParseValue();
                cmd.Arguments.Add(new PositionalArg { Value = val });
            }

            if (Check(TokenType.Comma)) Advance();
        }
    }

    private void ParseFlowBindings(CommandBlock cmd)
    {
        if (!Check(TokenType.Arrow)) return;
        Advance(); // ->

        cmd.FlowBindings = new VnsFlowBindings();

        if (Check(TokenType.Hash))
        {
            Advance();
            cmd.FlowBindings.DefaultTarget = Expect(TokenType.Identifier).Value;
            return;
        }

        // Parse output bindings: out_name:#label, out_name:$var
        while (!Check(TokenType.NewLine) && !Check(TokenType.Eof) && !Check(TokenType.AtSign))
        {
            if (Check(TokenType.Identifier))
            {
                var outName = Advance().Value;
                if (Check(TokenType.Colon)) Advance();

                if (Check(TokenType.Hash))
                {
                    Advance();
                    cmd.FlowBindings.NamedTargets[outName] = Expect(TokenType.Identifier).Value;
                }
                else if (Check(TokenType.Dollar) || Check(TokenType.Identifier))
                {
                    var varName = ParseValue();
                    if (varName is VnsVariable v)
                        cmd.FlowBindings.VariableBindings[outName] = v.Name;
                    else if (varName is VnsString s)
                        cmd.FlowBindings.VariableBindings[outName] = s.Value;
                }
            }
            else if (Check(TokenType.Comma)) { Advance(); }
            else break;
        }
    }

    private VnsValue ParseValue()
    {
        return Current.Type switch
        {
            TokenType.String => new VnsString { Value = Advance().Value },
            TokenType.Number => new VnsNumber { Value = double.Parse(Advance().Value) },
            TokenType.Dollar => ParseVariable(),
            TokenType.Ampersand => ParseResourceRef(),
            TokenType.Hash => new VnsLabelRef { LabelName = (Advance(), Expect(TokenType.Identifier).Value).Item2 },
            TokenType.LParen => ParseVector(),
            TokenType.Identifier when Current.Value is "true" or "false" or "null" => ParseLiteral(),
            _ => new VnsString { Value = Advance().Value }
        };
    }

    private VnsValue ParseLiteral()
    {
        var v = Advance().Value;
        return v switch
        {
            "true" => new VnsBool { Value = true },
            "false" => new VnsBool { Value = false },
            "null" => new VnsNull(),
            _ => new VnsString { Value = v }
        };
    }

    private VnsVariable ParseVariable()
    {
        Expect(TokenType.Dollar);
        var name = Expect(TokenType.Identifier).Value;
        return new VnsVariable { Scope = "$", Name = name };
    }

    private VnsResourceRef ParseResourceRef()
    {
        Expect(TokenType.Ampersand);
        var type = Expect(TokenType.Identifier).Value;
        Expect(TokenType.LParen);
        var path = Expect(TokenType.String).Value;
        Expect(TokenType.RParen);
        return new VnsResourceRef { ResourceType = type, Path = path };
    }

    private VnsVector2 ParseVector()
    {
        Expect(TokenType.LParen);
        double x = 0, y = 0;
        if (Check(TokenType.Number)) x = double.Parse(Advance().Value);
        if (Check(TokenType.Comma)) Advance();
        if (Check(TokenType.Number)) y = double.Parse(Advance().Value);
        Expect(TokenType.RParen);
        return new VnsVector2 { X = x, Y = y };
    }

    // ---- Inner block parsing (for if/choice bodies) ----

    private List<VnsBlock> ParseInnerBlocks(params string[] stopCommands)
    {
        var blocks = new List<VnsBlock>();
        SkipNewlines();
        while (!IsAtEnd())
        {
            if (Check(TokenType.AtSign))
            {
                var saved = _pos;
                Advance();
                if (Check(TokenType.Identifier) && stopCommands.Contains(Current.Value))
                {
                    _pos = saved;
                    break;
                }
                _pos = saved;
            }
            var block = ParseBlock();
            if (block != null) blocks.Add(block);
            SkipNewlines();
        }
        return blocks;
    }

    // ---- Helpers ----

    private string ReadRestOfLine()
    {
        var sb = new System.Text.StringBuilder();
        while (!Check(TokenType.NewLine) && !Check(TokenType.Eof))
        {
            sb.Append(Advance().Value);
            if (sb.Length > 0 && !Check(TokenType.NewLine) && !Check(TokenType.Eof)) sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private string ReadCondition()
    {
        var sb = new System.Text.StringBuilder();
        int depth = 0; // track nested parens
        while (!IsAtEnd())
        {
            if (Check(TokenType.RParen) && depth == 0) break;
            if (Check(TokenType.LParen)) depth++;
            if (Check(TokenType.RParen)) depth--;
            var t = Advance();
            if (t.Type != TokenType.Comma) sb.Append(t.Value);
            sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.NewLine) || Check(TokenType.Comment)) Advance();
    }

    private void SkipWhitespaceTokens()
    {
        while (Check(TokenType.NewLine) || Check(TokenType.Comment)) Advance();
    }

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : new Token { Type = TokenType.Eof };
    private Token Advance() => _pos < _tokens.Count ? _tokens[_pos++] : new Token { Type = TokenType.Eof };
    private bool Check(TokenType t) => Current.Type == t;
    private bool CheckNamed(string name) => Check(TokenType.Identifier) && Current.Value == name;
    private TokenType PeekType(int offset) => _pos + offset < _tokens.Count ? _tokens[_pos + offset].Type : TokenType.Eof;
    private string? PeekValue(int offset) => _pos + offset < _tokens.Count ? _tokens[_pos + offset].Value : null;
    private bool IsAtEnd() => Current.Type == TokenType.Eof;

    private Token Expect(TokenType type)
    {
        if (Check(type)) return Advance();
        // Skip unexpected tokens gracefully
        Advance();
        return new Token { Type = type, Value = "" };
    }

    private SourceLocation Loc() => new(Current.Line, Current.Column, _filePath);

    private VnsBlock? Consume() { Advance(); return null; }

    private void Error(string msg) => Errors.Add($"[{_filePath}:{Current.Line}:{Current.Column}] {msg}");
    private void SkipTo(TokenType type)
    {
        while (!Check(type) && !IsAtEnd()) Advance();
        if (Check(type)) Advance();
    }
}

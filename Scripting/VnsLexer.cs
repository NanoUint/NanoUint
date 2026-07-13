namespace NanoUint.Scripting;

/// <summary>.vns 脚本词法分析的 Token 类型</summary>
public enum TokenType
{
    Eof, NewLine,
    AtSign,           // @
    Hash,             // #
    Colon,            // :
    Dash,             // -
    Arrow,            // ->
    Comma,            // ,
    LParen, RParen,   // ( )
    Equals,           // =
    Dollar,           // $
    Dot,              // .
    Ampersand,        // &
    String,           // "文本"
    Identifier,       // 字母数字名称
    Number,           // 123 或 3.14
    DoubleAt,         // @@
    Comment,          // ; 或 // 注释
    Semicolon,        // ;
    DoubleSlash,      // //
}

/// <summary>词法分析器生成的单个 Token</summary>
public class Token
{
    public TokenType Type { get; set; }
    public string Value { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }

    public override string ToString() => $"[{Type}] '{Value}' at {Line}:{Column}";
}

/// <summary>
/// .vns 脚本文件的词法分析器/分词器。
/// 将原始文本转换为 Token 流。
/// </summary>
public class VnsLexer
{
    private readonly string _source;
    private readonly string _filePath;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public VnsLexer(string source, string filePath = "")
    {
        _source = source;
        _filePath = filePath;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        Token token;
        int safety = 0;
        do
        {
            if (++safety > 100_000)
                throw new InvalidOperationException("Lexer safety limit exceeded — likely infinite loop");
            token = NextToken();
            tokens.Add(token);
        } while (token.Type != TokenType.Eof);

        return tokens;
    }

    private Token NextToken()
    {
        SkipWhitespaceExceptNewline();

        if (_pos >= _source.Length)
            return MakeToken(TokenType.Eof, "");

        char c = _source[_pos];

        // 换行符
        if (c == '\n' || c == '\r')
        {
            if (c == '\r' && Peek() == '\n') Advance();
            Advance();
            int line = _line - 1; // 行号已递增
            return MakeToken(TokenType.NewLine, "\\n", line + 1, 1);
        }

        // 注释
        if (c == ';' || (c == '/' && Peek() == '/'))
        {
            return ReadComment(c == '/' ? 2 : 1);
        }

        // @@
        if (c == '@' && Peek() == '@')
        {
            Advance(); Advance();
            return MakeToken(TokenType.DoubleAt, "@@");
        }

        // @
        if (c == '@')
        {
            Advance();
            return MakeToken(TokenType.AtSign, "@");
        }

        // ->
        if (c == '-' && Peek() == '>')
        {
            Advance(); Advance();
            return MakeToken(TokenType.Arrow, "->");
        }

        // #
        if (c == '#')
        {
            Advance();
            // 只有后跟恰好6位或8位十六进制数字时才视为十六进制颜色。
            // 3/4位颜色会与诸如 #abc、#ask_who 等标签冲突。
            if (IsHex(Peek()))
            {
                var saved = _pos;
                var hex = ReadWhile(IsHex);
                if ((hex.Length == 6 || hex.Length == 8) && !IsIdentPart(Peek()))
                    return MakeValueToken(TokenType.Identifier, "#" + hex);
                // 不是十六进制颜色 —— 回退并作为标签标记处理
                _pos = saved;
            }
            return MakeToken(TokenType.Hash, "#");
        }

        // :
        if (c == ':')
        {
            Advance();
            return MakeToken(TokenType.Colon, ":");
        }

        // -
        if (c == '-')
        {
            Advance();
            return MakeToken(TokenType.Dash, "-");
        }

        // ,
        if (c == ',')
        {
            Advance();
            return MakeToken(TokenType.Comma, ",");
        }

        // (
        if (c == '(')
        {
            Advance();
            return MakeToken(TokenType.LParen, "(");
        }

        // )
        if (c == ')')
        {
            Advance();
            return MakeToken(TokenType.RParen, ")");
        }

        // =
        if (c == '=')
        {
            Advance();
            if (Peek() == '=') { Advance(); return MakeToken(TokenType.Identifier, "=="); }
            return MakeToken(TokenType.Equals, "=");
        }

        // $
        if (c == '$')
        {
            Advance();
            return MakeToken(TokenType.Dollar, "$");
        }

        // .
        if (c == '.')
        {
            Advance();
            return MakeToken(TokenType.Dot, ".");
        }

        // &
        if (c == '&')
        {
            Advance();
            return MakeToken(TokenType.Ampersand, "&");
        }

        // "
        if (c == '"')
        {
            return ReadString();
        }

        // 数字
        if (char.IsDigit(c) || (c == '-' && char.IsDigit(Peek())))
        {
            return ReadNumber();
        }

        // 标识符
        if (IsIdentStart(c))
        {
            var ident = ReadWhile(IsIdentPart);
            return MakeValueToken(TokenType.Identifier, ident);
        }

        // 未知字符 —— 跳过
        Advance();
        return MakeToken(TokenType.Identifier, c.ToString());
    }

    // ---- 辅助方法 ----

    private void SkipWhitespaceExceptNewline()
    {
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (c == ' ' || c == '\t')
            {
                Advance();
            }
            else break;
        }
    }

    private Token ReadComment(int skip)
    {
        Advance(); if (skip == 2) Advance();
        var text = ReadWhile(ch => ch != '\n' && ch != '\r');
        return MakeToken(TokenType.Comment, text.Trim());
    }

    private Token ReadString()
    {
        Advance(); // 跳过开头的 "
        var sb = new System.Text.StringBuilder();
        while (_pos < _source.Length && _source[_pos] != '"' && _source[_pos] != '\n' && _source[_pos] != '\r')
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
            {
                Advance();
                sb.Append(_source[_pos]);
            }
            else
            {
                sb.Append(_source[_pos]);
            }
            Advance();
        }
        if (_pos < _source.Length && _source[_pos] == '"') Advance();
        return MakeValueToken(TokenType.String, sb.ToString());
    }

    private Token ReadNumber()
    {
        bool neg = false;
        if (_source[_pos] == '-') { neg = true; Advance(); }
        var num = ReadWhile(ch => char.IsDigit(ch) || ch == '.');
        return MakeValueToken(TokenType.Number, (neg ? "-" : "") + num);
    }

    private char Peek() => _pos + 1 < _source.Length ? _source[_pos + 1] : '\0';
    private void Advance() { if (_pos < _source.Length) { if (_source[_pos] == '\n') { _line++; _col = 1; } else _col++; _pos++; } }

    private string ReadWhile(Func<char, bool> pred)
    {
        var start = _pos;
        while (_pos < _source.Length && pred(_source[_pos])) Advance();
        return _source[start.._pos];
    }

    private static bool IsHex(char c) => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => IsIdentStart(c) || char.IsDigit(c);

    private Token MakeToken(TokenType type, string value, int? line = null, int? col = null)
    {
        return new Token { Type = type, Value = value, Line = line ?? _line, Column = col ?? _col };
    }

    private Token MakeValueToken(TokenType type, string value)
    {
        return new Token { Type = type, Value = value, Line = _line, Column = _col };
    }
}

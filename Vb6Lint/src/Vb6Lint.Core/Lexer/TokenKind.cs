namespace Vb6Lint.Core.Lexer;

/// <summary>
/// Classifies every token produced by the VB6 lexer.
/// Ordering is irrelevant — use switch expressions by name.
/// </summary>
public enum TokenKind
{
    // ── Structural ────────────────────────────────────────────────────────────
    EndOfFile,
    NewLine,          // logical line terminator (CR, LF, or CRLF)
    Colon,            // ':' statement separator on the same physical line
    LineContinuation, // ' _' + newline — consumed transparently by the lexer

    // ── Literals ──────────────────────────────────────────────────────────────
    IntegerLiteral,
    LongLiteral,
    DoubleLiteral,
    SingleLiteral,
    CurrencyLiteral,
    StringLiteral,
    DateLiteral,
    BooleanLiteral,   // True / False
    NothingLiteral,   // Nothing
    NullLiteral,      // Null
    EmptyLiteral,     // Empty

    // ── Identifier / generic name ─────────────────────────────────────────────
    Identifier,

    // ── Declaration keywords ──────────────────────────────────────────────────
    Dim,
    Public,
    Private,
    Friend,
    Global,
    Static,
    Const,
    Type,
    Enum,

    // ── Procedure keywords ────────────────────────────────────────────────────
    Sub,
    Function,
    Property,
    Get,
    Set,
    Let,
    End,
    Exit,
    Return,

    // ── Class / module keywords ───────────────────────────────────────────────
    Class,
    Implements,
    Event,
    RaiseEvent,
    WithEvents,

    // ── Flow control ──────────────────────────────────────────────────────────
    If,
    Then,
    Else,
    ElseIf,
    EndIf,      // synthesised from "End If"
    Select,
    Case,
    EndSelect,  // synthesised
    For,
    Each,
    In,
    To,
    Step,
    Next,
    While,
    Wend,
    Do,
    Loop,
    Until,
    GoTo,
    GoSub,
    Resume,
    On,
    Error,
    With,
    EndWith,    // synthesised

    // ── Option statements ─────────────────────────────────────────────────────
    Option,
    Explicit,
    Base,
    Compare,
    Text,
    Binary,
    Private_Keyword, // "Option Private Module"

    // ── Parameter modifiers ───────────────────────────────────────────────────
    ByVal,
    ByRef,
    Optional,
    ParamArray,

    // ── Type keywords ─────────────────────────────────────────────────────────
    As,
    New,
    Is,
    Like,
    TypeOf,
    Integer,
    Long,
    Single,
    Double,
    String,
    Boolean,
    Date,
    Variant,
    Object,
    Byte,
    Currency,
    LongLong,
    LongPtr,

    // ── Operators ─────────────────────────────────────────────────────────────
    Plus,            // +
    Minus,           // -
    Star,            // *
    Slash,           // /
    BackSlash,       // \  (integer division)
    Caret,           // ^
    Ampersand,       // &  (string concatenation)
    Equals,          // =
    NotEquals,       // <>
    LessThan,        // <
    GreaterThan,     // >
    LessOrEqual,     // <=
    GreaterOrEqual,  // >=
    And,
    Or,
    Not,
    Xor,
    Eqv,
    Imp,
    Mod,

    // ── Punctuation ───────────────────────────────────────────────────────────
    LeftParen,       // (
    RightParen,      // )
    Comma,           // ,
    Dot,             // .
    Bang,            // !  (default member shortcut)
    Hash,            // #  (date delimiter / line-number prefix)
    At,              // @  (currency type suffix)
    Underscore,      // _  (only when NOT a line-continuation)
    DollarSign,      // $  (string-returning function suffix)
    PercentSign,     // %
    AmpersandSuffix, // & type suffix

    // ── Misc keywords ────────────────────────────────────────────────────────
    Call,
    Declare,
    Lib,
    Alias,
    AddressOf,
    Me,
    Stop,
    End_Statement,  // bare "End" statement (ends a program)
    Rem,            // Rem comment
    Attribute,
    DefBool,
    DefByte,
    DefInt,
    DefLng,
    DefSng,
    DefDbl,
    DefCur,
    DefDate,
    DefStr,
    DefObj,
    DefVar,

    // ── Comment (carries the full comment text) ───────────────────────────────
    Comment,

    // ── Error recovery ────────────────────────────────────────────────────────
    Unknown,
}

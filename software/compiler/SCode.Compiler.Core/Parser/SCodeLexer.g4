lexer grammar SCodeLexer;

channels {
    COMMENTS_CHANNEL
}

options {
    language = CSharp;
}

// Comments & whitespaces
SINGLE_LINE_COMMENT         : '//' ~[\r\n]*  -> channel(COMMENTS_CHANNEL);
DELIMITED_COMMENT           : '/*' .*? '*/'  -> channel(COMMENTS_CHANNEL);
WHITESPACES                 : [ \t\r\n]+     -> channel(HIDDEN);

// SCode keywords
BOOL       : 'bool';
BREAK      : 'break';
CASE       : 'case';
CHAR       : 'char';
CONST      : 'const';
CONTINUE   : 'continue';
DECIMAL    : 'decimal';
DEFAULT    : 'default';
DO         : 'do';
ELSE       : 'else';
EXTERN     : 'extern';
FALSE      : 'false';
FOR        : 'for';
GOTO       : 'goto';
INCLUDE    : 'include';
IF         : 'if';
INT        : 'int';
UINT       : 'uint';
LONG       : 'long';
NEW        : 'new';
NULL       : 'null';
RETURN     : 'return';
SIZEOF     : 'sizeof';
STATIC     : 'static';
STRING     : 'string';
STRUCT     : 'struct';
SWITCH     : 'switch';
TRUE       : 'true';
VOID       : 'void';
WHILE      : 'while';

// Identifier
IDENTIFIER: [a-zA-Z_][a-zA-Z_0-9]*;

// Literals
LITERAL_CHAR : '\'' (~['\\\r\n] | SimpleEscapeSequence) '\'';
LITERAL_STRING: '"' (~["\\] | SimpleEscapeSequence)* '"';

LITERAL_INT: (LITERAL_INT_DEC|LITERAL_INT_HEX|LITERAL_INT_BIN);
LITERAL_INT_DEC: [0-9]+;
LITERAL_INT_HEX: '0x' [0-9a-fA-F]+;
LITERAL_INT_BIN: '0b' [01]+;
LITERAL_DECIMAL: [0-9]+ '.' [0-9]+;

// Operators And Punctuators
OPEN_BRACE               : '{';
CLOSE_BRACE              : '}';
OPEN_BRACKET             : '[';
CLOSE_BRACKET            : ']';
OPEN_PARENS              : '(';
CLOSE_PARENS             : ')';
DOT                      : '.';
COMMA                    : ',';
COLON                    : ':';
SEMICOLON                : ';';
PLUS                     : '+';
MINUS                    : '-';
STAR                     : '*';
DIV                      : '/';
PERCENT                  : '%';
AMP                      : '&';
BITWISE_OR               : '|';
CARET                    : '^';
BANG                     : '!';
TILDE                    : '~';
ASSIGNMENT               : '=';
LT                       : '<';
GT                       : '>';
INTERR                   : '?';
DOUBLE_COLON             : '::';
OP_COALESCING            : '??';
OP_INC                   : '++';
OP_DEC                   : '--';
OP_AND                   : '&&';
OP_OR                    : '||';
OP_PTR                   : '->';
OP_EQ                    : '==';
OP_NE                    : '!=';
OP_LE                    : '<=';
OP_GE                    : '>=';
OP_ADD_ASSIGNMENT        : '+=';
OP_SUB_ASSIGNMENT        : '-=';
OP_MULT_ASSIGNMENT       : '*=';
OP_DIV_ASSIGNMENT        : '/=';
OP_MOD_ASSIGNMENT        : '%=';
OP_AND_ASSIGNMENT        : '&=';
OP_OR_ASSIGNMENT         : '|=';
OP_XOR_ASSIGNMENT        : '^=';
OP_LEFT_SHIFT            : '<<';
OP_LEFT_SHIFT_ASSIGNMENT : '<<=';
OP_RIGHT_SHIFT           : '>>';
OP_RIGHT_SHIFT_ASSIGNMENT: '>>=';
OP_COALESCING_ASSIGNMENT : '??=';
SHARP                    : '#';


// Fragments
fragment SimpleEscapeSequence:
    '\\\''
    | '\\"'
    | '\\\\'
    | '\\0'
    | '\\a'
    | '\\b'
    | '\\f'
    | '\\n'
    | '\\r'
    | '\\t'
    | '\\v'
;
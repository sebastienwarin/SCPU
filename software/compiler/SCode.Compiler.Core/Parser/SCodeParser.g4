parser grammar SCodeParser;

options {  
  tokenVocab = SCodeLexer;
  language = CSharp;
}

// Entry point
program: statement* EOF;

// Types
customTypeName: identifier;
baseType: LONG | INT | DECIMAL | BOOL | CHAR | STRING;
type: (baseType|customTypeName) '*'*;

// Statements
statement
  : labeledStatement
  | declarationStatement
  | embeddedStatement
  ;
labeledStatement: identifier ':' statement;

// Declaration statements
declarationStatement
  : variableDeclaration ';'
  | includeDeclaration
  | assemblyConstantDeclaration
  | functionDeclaration
  | structDeclaration
  ;

// Include declaration
includeDeclaration: SHARP INCLUDE LITERAL_STRING;

// Assembly constant declaration
assemblyConstantDeclaration: SHARP CONST baseType identifier '=' conditionalExpression;

// Variable declarations
variableDeclaration: CONST? STATIC? type variableDeclarators;
variableDeclarators: variableDeclarator (',' variableDeclarator)*;
variableDeclarator: identifier (arraySpecifier)? ('=' variableInitializer)?;
arraySpecifier: '[' LITERAL_INT? (',' LITERAL_INT?)* ']';

// Variable initializer
variableInitializer
  : expression
  | arrayInitializer
  | structInitializer
  ;
arrayInitializer: OPEN_BRACE (variableInitializer (',' variableInitializer)* ','?)? CLOSE_BRACE;
structInitializer: OPEN_BRACE (structMembersInitializers ','?)? CLOSE_BRACE;
structMembersInitializers: structMemberInitializer (',' structMemberInitializer)*;
structMemberInitializer: identifier ':' variableInitializer;

// Function declaration
functionDeclaration: EXTERN functionSignature ';' | functionSignature block;
functionSignature: returnType identifier OPEN_PARENS parameters? CLOSE_PARENS;
returnType: type | VOID;
parameters: parameter (',' parameter)*;
parameter: type identifier;

// Struct declaration
structDeclaration: STRUCT identifier structBody ';'?;
structBody: OPEN_BRACE structMemberDeclaration* CLOSE_BRACE;
structMemberDeclaration: type identifier ';';

// Embedded statements
embeddedStatement
  : block
  | simpleEmbeddedStatement
  ;

simpleEmbeddedStatement
  : ';'             # emptyStatement
  | expression ';'  # expressionStatement

  // Selection statements
  | IF OPEN_PARENS expression CLOSE_PARENS ifBody (ELSE ifBody)?                      # ifStatement
  | SWITCH OPEN_PARENS expression CLOSE_PARENS OPEN_BRACE switchSection* CLOSE_BRACE  # switchStatement

  // Iteration statements
  | WHILE OPEN_PARENS expression CLOSE_PARENS embeddedStatement (ELSE embeddedStatement )?    # whileStatement
  | DO embeddedStatement WHILE OPEN_PARENS expression CLOSE_PARENS                            # doStatement
  | FOR OPEN_PARENS forInitializer?';' expression? ';' forIterator? CLOSE_PARENS block        # forStatement

  // Jump statements
  | BREAK ';'                       # breakStatement
  | CONTINUE ';'                    # continueStatement
  | GOTO identifier ';'             # gotoStatement
  | RETURN expression? ';'          # returnStatement
  ;

ifBody
  : block
  | simpleEmbeddedStatement
  ;

forInitializer: 'int'? identifier '=' expression;
forIterator: expression; // (',' expression)*

switchSection: switchLabel+ statementList;
switchLabel
  : CASE expression ':'
  | DEFAULT ':'
  ;

// Expressions
expression
  : assignment
  | nonAssignmentExpression
  ;

nonAssignmentExpression: conditionalExpression;

assignment: primaryExpression assignmentOperator expression;
assignmentOperator
  : '='
  | '+='
  | '-='
  | '*='
  | '/='
  | '%='
  | '&='
  | '|='
  | '^='
  | '<<='
  | '>>='
  ;
  
unaryExpression
  : castExpression
  | primaryExpression
  | '+' unaryExpression
  | '-' unaryExpression
  | BANG unaryExpression
  | '~' unaryExpression
  | '++' unaryExpression
  | '--' unaryExpression
  | '&' unaryExpression
  | '*' unaryExpression
  ;
castExpression: OPEN_PARENS type CLOSE_PARENS unaryExpression;

primaryExpression
  : pe = primaryExpressionStart preAccess=arrayAccessExpression* (
      (memberAccess | methodInvocation | '++' | '--') postAccess=arrayAccessExpression*
  )?
  ;

memberAccess: '.' identifier;
methodInvocation: OPEN_PARENS argumentList? CLOSE_PARENS;
argumentList: expression (',' expression)*;
arrayAccessExpression: '[' expression (',' expression)* ']';

primaryExpressionStart
  : literal                                 # literalExpression
  | identifier                              # identifierExpression
  | OPEN_PARENS expression CLOSE_PARENS     # parenthesisExpressions
  | SIZEOF OPEN_PARENS type CLOSE_PARENS    # sizeofExpression
  ;

// Ternary expression
conditionalExpression: orConditionalOrExpression ('?' expression ':' expression)?;

// Boolean expressions
orConditionalOrExpression: andConditionalExpression (OP_OR andConditionalExpression)*;
andConditionalExpression: orBitwiseExpression (OP_AND orBitwiseExpression)*;

// Bitwise expressions
orBitwiseExpression: xorBitwiseExpression ('|' xorBitwiseExpression)*;
xorBitwiseExpression: andBitwiseExpression ('^' andBitwiseExpression)*;
andBitwiseExpression: equalityExpression ('&' equalityExpression)*;

// Equality & relational expressions
equalityExpression: relationalExpression ((OP_EQ | OP_NE) relationalExpression)*;
relationalExpression: shiftExpression (('<' | '>' | '<=' | '>=') shiftExpression)*;

// Logic shift expressions
shiftExpression: additiveExpression (('<<' | '>>') additiveExpression)*;

// Additive & multiplicative expressions
additiveExpression: multiplicativeExpression (('+' | '-') multiplicativeExpression)*;
multiplicativeExpression: unaryExpression (('*' | '/' | '%') unaryExpression)*;

// Basic concepts
block: OPEN_BRACE statementList? CLOSE_BRACE;
statementList: statement+;

literal
  : literalBoolean
  | LITERAL_STRING
  | LITERAL_INT
  | LITERAL_DECIMAL
  | LITERAL_CHAR
  | NULL
  ;

literalBoolean
  : TRUE
  | FALSE
  ;

identifier: IDENTIFIER;

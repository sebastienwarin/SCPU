using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Ast.Expressions;
using SCode.Compiler.Ast.Expressions.Binary;
using SCode.Compiler.Ast.Expressions.Initializers;
using SCode.Compiler.Ast.Expressions.Unary;
using SCode.Compiler.Ast.Literals;
using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Ast.Statements.VariableDeclaration;
using System.Globalization;

namespace SCode.Compiler.Ast
{
    internal class SCodeVisitor : SCodeParserBaseVisitor<Node>
    {
        #region Basic concepts

        public override Program VisitProgram([NotNull] SCodeParser.ProgramContext context)
        {
            return new Program
            {
                Source = SourceRange.FromParserContext(context),
                Body = context.statement().Select(VisitStatement).RemoveNull().ToList()
            };
        }

        public override Block VisitBlock([NotNull] SCodeParser.BlockContext context)
        {
            return VisitStatementList(context.statementList());
        }

        public override Block VisitStatementList([NotNull] SCodeParser.StatementListContext context)
        {
            if (context == null) return null;
            return new Block
            {
                Source = SourceRange.FromParserContext(context),
                Body = context.statement().Select(VisitStatement).RemoveNull().ToList()
            };
        }

        #endregion

        #region Basic Statements

        public Statement VisitStatement(ParserRuleContext context)
        {
            return Visit<Statement>(context);
        }

        public override Statement VisitLabeledStatement([NotNull] SCodeParser.LabeledStatementContext context)
        {
            var statement = VisitStatement(context.statement());
            statement.Label = context.identifier().GetText();
            return statement;
        }

        #endregion

        #region Variable declaration

        public override VariableDeclarationStatement VisitVariableDeclaration([NotNull] SCodeParser.VariableDeclarationContext context)
        {
            return new VariableDeclarationStatement
            {
                Source = SourceRange.FromParserContext(context),
                IsConst = context.CONST() != null,
                IsStatic = context.STATIC() != null,
                Type = VisitType(context.type()),
                Variables = VisitVariableDeclarators(context.variableDeclarators())
            };
        }

        public List<VariableDeclarator> VisitVariableDeclarators([NotNull] SCodeParser.VariableDeclaratorsContext context)
        {
            return context.variableDeclarator()?.Select(VisitVariableDeclarator).RemoveNull().ToList();
        }

        public override VariableDeclarator VisitVariableDeclarator([NotNull] SCodeParser.VariableDeclaratorContext context)
        {
            return new VariableDeclarator
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier()),
                Initializer = VisitVariableInitializer(context.variableInitializer()),
                ArraySpecifier = VisitArraySpecifier(context.arraySpecifier())
            };
        }

        public override ArraySpecifier VisitArraySpecifier([NotNull] SCodeParser.ArraySpecifierContext context)
        {
            if (context == null) return null;
            return new ArraySpecifier
            {
                Source = SourceRange.FromParserContext(context),
                Count = context.COMMA().Length + 1,
                Sizes = context.LITERAL_INT().Select(v => int.Parse(v.GetText())).ToArray()
            };
        }

        #endregion

        #region Variables initializers

        public override InitializerExpression VisitVariableInitializer([NotNull] SCodeParser.VariableInitializerContext context)
        {
            if (context == null)
            {
                return null;
            }
            else if (context.expression() is { } expression)
            {
                return new ValueInitializerExpression
                {
                    Source = SourceRange.FromParserContext(context),
                    Value = VisitExpression(expression)
                };
            }
            else if (context.arrayInitializer() is { } arrayInitializer)
            {
                return new ArrayInitializerExpression
                {
                    Source = SourceRange.FromParserContext(context),
                    Values = arrayInitializer.variableInitializer().Select(VisitVariableInitializer).RemoveNull().ToList()
                };
            }
            else if (context.structInitializer() is { } structInitializer)
            {
                return new StructInitializerExpression
                {
                    Source = SourceRange.FromParserContext(context),
                    Initializers = VisitStructMembersInitializers(structInitializer.structMembersInitializers())
                };
            }
            else
            {
                throw new InvalidOperationException($"{context.GetType().Name} is not a supported VariableInitializer");
            }
        }

        public List<StructInitializerExpression.MemberInitializer> VisitStructMembersInitializers([NotNull] SCodeParser.StructMembersInitializersContext context)
        {
            return context?.structMemberInitializer()?.Select(VisitStructMemberInitializer).ToList() ?? [];
        }

        public override StructInitializerExpression.MemberInitializer VisitStructMemberInitializer([NotNull] SCodeParser.StructMemberInitializerContext context)
        {
            return new StructInitializerExpression.MemberInitializer
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier()),
                Initializer = VisitVariableInitializer(context.variableInitializer())
            };
        }

        #endregion

        #region Include declaration

        public override Node VisitIncludeDeclaration([NotNull] SCodeParser.IncludeDeclarationContext context)
        {
            return new IncludeStatement
            { 
                Source = SourceRange.FromParserContext(context),
                Filename = context.LITERAL_STRING().GetText().RemoveCharsContainer()
            };
        }

        #endregion

        #region Assembly Constant declaration

        public override Node VisitAssemblyConstantDeclaration([NotNull] SCodeParser.AssemblyConstantDeclarationContext context)
        {
            return new AssemblyConstantStatement
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier()),
                Type = new TypeDescriptor
                {
                    Source = SourceRange.FromParserContext(context),
                    Name = context.baseType()?.GetText(),
                    IsBaseType = true
                },
                Value = context.conditionalExpression().GetText()
            };
        }

        #endregion

        #region Function declaration

        public override FunctionDeclarationStatement VisitFunctionDeclaration([NotNull] SCodeParser.FunctionDeclarationContext context)
        {
            var isExtern = context.EXTERN() != null;
            var signature = context.functionSignature();
            return new FunctionDeclarationStatement
            {
                Source = SourceRange.FromParserContext(context),
                IsExtern = isExtern,
                ReturnType = VisitReturnType(signature.returnType()),
                Identifier = VisitIdentifier(signature.identifier()),
                Parameters = VisitParameters(signature.parameters()),
                Body = !isExtern ? VisitBlock(context.block()) : null
            };
        }

        public List<FunctionDeclarationStatement.Parameter> VisitParameters([NotNull] SCodeParser.ParametersContext context)
        {
            return context?.parameter()?.Select(VisitParameter).ToList() ?? [];
        }

        public override FunctionDeclarationStatement.Parameter VisitParameter([NotNull] SCodeParser.ParameterContext context)
        {
            return new FunctionDeclarationStatement.Parameter
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier()),
                Type = VisitType(context.type())
            };
        }

        #endregion

        #region Structs declaration

        public override StructDeclarationStatement VisitStructDeclaration([NotNull] SCodeParser.StructDeclarationContext context)
        {
            return new StructDeclarationStatement
            {
                Source = SourceRange.FromParserContext(context),
                Members = VisitStructBody(context.structBody()),
                Identifier = VisitIdentifier(context.identifier())
            };
        }

        public List<StructDeclarationStatement.Member> VisitStructBody([NotNull] SCodeParser.StructBodyContext context)
        {
            return context?.structMemberDeclaration()?.Select(VisitStructMemberDeclaration).ToList() ?? [];
        }

        public override StructDeclarationStatement.Member VisitStructMemberDeclaration([NotNull] SCodeParser.StructMemberDeclarationContext context)
        {
            return new StructDeclarationStatement.Member
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier()),
                Type = VisitType(context.type())
            };
        }

        #endregion

        #region If statement

        public override IfStatement VisitIfStatement([NotNull] SCodeParser.IfStatementContext context)
        {
            return new IfStatement
            {
                Source = SourceRange.FromParserContext(context),
                Condition = VisitExpression(context.expression()),
                Then = VisitIfBody(context.ifBody(0)),
                Else = VisitIfBody(context.ifBody(1))
            };
        }

        public override Block VisitIfBody([NotNull] SCodeParser.IfBodyContext context)
        {
            if (context == null)
            {
                return null;
            }
            else if (context.block() is { } block)
            {
                return VisitBlock(block);
            }
            else if (context.simpleEmbeddedStatement() is { } statement)
            {
                return new Block
                {
                    Source = SourceRange.FromParserContext(context),
                    Body = [VisitStatement(statement)]
                };
            }
            else
            {
                var source = SourceRange.FromParserContext(context);
                throw new InvalidOperationException($"{source}: {context.GetType().Name} is not a valid IfBody") { Source = source.ToString() };
            }
        }

        #endregion

        #region Switch statement

        public override SwitchStatement VisitSwitchStatement([NotNull] SCodeParser.SwitchStatementContext context)
        {
            return new SwitchStatement
            {
                Source = SourceRange.FromParserContext(context),
                Condition = VisitExpression(context.expression()),
                Sections = context.switchSection().Select(VisitSwitchSection).RemoveNull().ToList()
            };
        }

        public override SwitchStatement.SwitchSection VisitSwitchSection([NotNull] SCodeParser.SwitchSectionContext context)
        {
            return new SwitchStatement.SwitchSection
            {
                Source = SourceRange.FromParserContext(context),
                Cases = context.switchLabel().Select(VisitSwitchLabel).RemoveNull().ToList(),
                Body = VisitStatementList(context.statementList())
            };
        }

        public override Expression VisitSwitchLabel([NotNull] SCodeParser.SwitchLabelContext context)
        {
            if (context.expression() is { } expressionContext)
            {
                return VisitExpression(expressionContext);
            }
            else
            {
                // Default case
                return null;
            }
        }

        #endregion

        #region For statement

        public override ForStatement VisitForStatement([NotNull] SCodeParser.ForStatementContext context)
        {
            return new ForStatement
            {
                Source = SourceRange.FromParserContext(context),
                Initializer = VisitForInitializer(context.forInitializer()),
                Condition = VisitExpression(context.expression()),
                Iterator = VisitExpression(context.forIterator()),
                Body = VisitBlock(context.block()),
            };
        }

        public override Node VisitForInitializer([NotNull] SCodeParser.ForInitializerContext context)
        {
            if (context != null)
            {
                var source = SourceRange.FromParserContext(context);
                if (context.INT() is { })
                {
                    return new VariableDeclarationStatement
                    {
                        Source = source,
                        Type = new TypeDescriptor { IsBaseType = true, Name = "int", Source = source },
                        Variables =
                        [
                            new() {
                                Source = source,
                                Identifier = VisitIdentifier(context.identifier()),
                                Initializer = new ValueInitializerExpression
                                {
                                    Source = source,
                                    Value = VisitExpression(context.expression())
                                }
                            }
                        ]
                    };
                }
                else
                {
                    return new AssignmentExpression
                    {
                        Source = source,
                        Target = new IdentifierExpression
                        {
                            Source = source,
                            Identifier = VisitIdentifier(context.identifier())
                        },
                        Value = VisitExpression(context.expression())
                    };
                }
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region While & Do statements

        public override WhileStatement VisitWhileStatement([NotNull] SCodeParser.WhileStatementContext context)
        {
            return new WhileStatement
            {
                Source = SourceRange.FromParserContext(context),
                Condition = VisitExpression(context.expression()),
                Body = VisitEmbeddedStatementInternal(context.embeddedStatement(0)),
                Else = VisitEmbeddedStatementInternal(context.embeddedStatement(1)),
            };
        }

        public override DoWhileStatement VisitDoStatement([NotNull] SCodeParser.DoStatementContext context)
        {
            return new DoWhileStatement
            {
                Source = SourceRange.FromParserContext(context),
                Condition = VisitExpression(context.expression()),
                Body = VisitEmbeddedStatementInternal(context.embeddedStatement())
            };
        }

        private Block VisitEmbeddedStatementInternal([NotNull] SCodeParser.EmbeddedStatementContext context)
        {
            if (context == null)
            {
                return null;
            }
            else if (context.block() is { } block)
            {
                return VisitBlock(block);
            }
            else if (context.simpleEmbeddedStatement() is { } statement)
            {
                return new Block
                {
                    Source = SourceRange.FromParserContext(context),
                    Body = [VisitStatement(statement)]
                };
            }
            else
            {
                var source = SourceRange.FromParserContext(context);
                throw new InvalidOperationException($"{source}: {context.GetType().Name} is not a valid EmbeddedStatement") { Source = source.ToString() };
            }
        }

        #endregion

        #region Jump statements

        public override Node VisitReturnStatement([NotNull] SCodeParser.ReturnStatementContext context)
        {
            return new ReturnStatement
            {
                Source = SourceRange.FromParserContext(context),
                Value = VisitExpression(context.expression())
            };
        }

        public override Node VisitBreakStatement([NotNull] SCodeParser.BreakStatementContext context)
        {
            return new BreakStatement()
            {
                Source = SourceRange.FromParserContext(context),
            };
        }

        public override Node VisitContinueStatement([NotNull] SCodeParser.ContinueStatementContext context)
        {
            return new ContinueStatement()
            {
                Source = SourceRange.FromParserContext(context),
            };
        }

        public override Node VisitGotoStatement([NotNull] SCodeParser.GotoStatementContext context)
        {
            return new GotoStatement()
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier())
            };
        }

        #endregion

        #region Type description

        public override TypeDescriptor VisitType([NotNull] SCodeParser.TypeContext context)
        {
            return new TypeDescriptor
            {
                Source = SourceRange.FromParserContext(context),
                Name = context.baseType()?.GetText() ?? context.customTypeName().GetText(),
                PointerLevel = context.STAR().Length,
                IsBaseType = context.customTypeName() == null
            };
        }

        public override TypeDescriptor VisitReturnType([NotNull] SCodeParser.ReturnTypeContext context)
        {
            if (context.type() is { } typeContext)
            {
                return VisitType(typeContext);
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Expressions

        public override ExpressionStatement VisitExpressionStatement([NotNull] SCodeParser.ExpressionStatementContext context)
        {
            return new ExpressionStatement
            {
                Source = SourceRange.FromParserContext(context),
                Expression = VisitExpression(context.expression())
            };
        }

        public override Expression VisitParenthesisExpressions([NotNull] SCodeParser.ParenthesisExpressionsContext context)
        {
            return VisitExpression(context.expression());
        }

        public override ArrayAccessExpression VisitArrayAccessExpression([NotNull] SCodeParser.ArrayAccessExpressionContext context)
        {
            return new ArrayAccessExpression
            {
                Source = SourceRange.FromParserContext(context),
                Indices = context.expression().Select(VisitExpression).RemoveNull().ToList()
            };
        }

        public override CastExpression VisitCastExpression([NotNull] SCodeParser.CastExpressionContext context)
        {
            return new CastExpression
            {
                Source = SourceRange.FromParserContext(context),
                TargetedType = VisitType(context.type()),
                Expression = VisitExpression(context.unaryExpression())
            };
        }

        public override SizeOfExpression VisitSizeofExpression([NotNull] SCodeParser.SizeofExpressionContext context)
        {
            return new SizeOfExpression
            {
                Source = SourceRange.FromParserContext(context),
                Type = VisitType(context.type())
            };
        }

        public override Expression VisitPrimaryExpression([NotNull] SCodeParser.PrimaryExpressionContext context)
        {
            // Build the primary expression
            Expression? primaryExpression = null;
            if (context.methodInvocation() is { } methodInvocationContext)
            {
                var source = SourceRange.FromParserContext(methodInvocationContext);
                var identifier = Visit<IdentifierExpression>(context.pe) ?? throw new InvalidOperationException($"{source}: invalid identifier for MethodInvocationExpression") { Source = source.ToString() };
                primaryExpression = new FunctionInvocationExpression()
                {
                    Source = source,
                    Identifier = identifier!.Identifier,
                    Arguments = methodInvocationContext.argumentList()?.expression()?.Select(x => VisitExpression(x)).ToList() ?? []
                };
            }
            else if (context.OP_INC() is { })
            {
                primaryExpression = CreateUnaryExpression<IncDecExpression>(VisitExpression(context.primaryExpressionStart()), (opt) =>
                {
                    opt.Operator = IncDecOperator.Increment;
                    opt.Order = Order.Post;
                });
            }
            else if (context.OP_DEC() is { })
            {
                primaryExpression = CreateUnaryExpression<IncDecExpression>(VisitExpression(context.primaryExpressionStart()), (opt) =>
                {
                    opt.Operator = IncDecOperator.Decrement;
                    opt.Order = Order.Post;
                });
            }
            else if (context.memberAccess() is { } memberAccessContext)
            {
                var source = SourceRange.FromParserContext(memberAccessContext);
                primaryExpression = new MemberAccessExpression()
                {
                    Source = source,
                    Expression = VisitExpression(context.pe),
                    Member = VisitIdentifier(memberAccessContext.identifier())
                };
            }
            else
            {
                primaryExpression = Visit<Expression>(context.pe);
            }

            // Handle ArrayAccessExpression
            if (primaryExpression != null)
            {
                // Count ArrayAccess
                var preAccessExpressionCount = context.preAccess?.expression().Length ?? 0;
                var postAccessExpressionCount = context.postAccess?.expression().Length ?? 0;

                // Pre & Post ArrayAccess at same time
                if (preAccessExpressionCount > 0 && postAccessExpressionCount > 0)
                {
                    throw new InvalidOperationException($"{primaryExpression.Source}: ArrayAccessExpressions not allowed before and after a primary expression at same time")
                    {
                        Source = primaryExpression.Source.ToString()
                    };
                }

                // Check ArrayAccess after Post Inc/Dec
                if (postAccessExpressionCount > 0 && primaryExpression is IncDecExpression incDecExpression && incDecExpression.Order == Order.Post)
                {
                    throw new InvalidOperationException($"{primaryExpression.Source}: ArrayAccessExpression not allowed after post increment/decrement")
                    {
                        Source = primaryExpression.Source.ToString()
                    };
                }

                // Check ArrayAccess before method invocation
                if (preAccessExpressionCount > 0 && primaryExpression is FunctionInvocationExpression)
                {
                    throw new InvalidOperationException($"{primaryExpression.Source}: ArrayAccessExpression not allowed before method invocation")
                    {
                        Source = primaryExpression.Source.ToString()
                    };
                }

                // Build the ArrayAccessExpressions
                if (preAccessExpressionCount > 0 || postAccessExpressionCount > 0)
                {
                    var arrayAccessExpressions = context.arrayAccessExpression().Select(VisitArrayAccessExpression).ToList();

                    for (var i = 0; i < arrayAccessExpressions.Count; i++)
                    {
                        arrayAccessExpressions[i].Array = primaryExpression;
                        primaryExpression = arrayAccessExpressions[i];
                    }
                }
            }

            // Return the final expression
            return primaryExpression;
        }

        public Expression VisitExpression(ParserRuleContext context)
        {
            return Visit<Expression>(context);
        }

        #endregion

        #region Assignment expression

        public override AssignmentExpression VisitAssignment([NotNull] SCodeParser.AssignmentContext context)
        {
            var assignment = new AssignmentExpression
            {
                Source = SourceRange.FromParserContext(context),
                Target = VisitExpression(context.unaryExpression()),
                Value = VisitExpression(context.expression())
            };
            switch (VisitAssignmentOperator(context.assignmentOperator()))
            {
                case AssignmentOperator.AddAssign:
                    assignment.Value = SetAssignmentExpression<AdditiveExpression, AdditiveOperator>(AdditiveOperator.Add, assignment);
                    break;
                case AssignmentOperator.SubtractAssign:
                    assignment.Value = SetAssignmentExpression<AdditiveExpression, AdditiveOperator>(AdditiveOperator.Subtract, assignment);
                    break;
                case AssignmentOperator.AndAssign:
                    assignment.Value = SetAssignmentExpression<BitwiseExpression, BitwiseOperator>(BitwiseOperator.And, assignment);
                    break;
                case AssignmentOperator.OrAssign:
                    assignment.Value = SetAssignmentExpression<BitwiseExpression, BitwiseOperator>(BitwiseOperator.Or, assignment);
                    break;
                case AssignmentOperator.XorAssign:
                    assignment.Value = SetAssignmentExpression<BitwiseExpression, BitwiseOperator>(BitwiseOperator.Xor, assignment);
                    break;
                case AssignmentOperator.MultiplyAssign:
                    assignment.Value = SetAssignmentExpression<MultiplicativeExpression, MultiplicativeOperator>(MultiplicativeOperator.Multiply, assignment);
                    break;
                case AssignmentOperator.DivideAssign:
                    assignment.Value = SetAssignmentExpression<MultiplicativeExpression, MultiplicativeOperator>(MultiplicativeOperator.Divide, assignment);
                    break;
                case AssignmentOperator.ModuloAssign:
                    assignment.Value = SetAssignmentExpression<MultiplicativeExpression, MultiplicativeOperator>(MultiplicativeOperator.Modulus, assignment);
                    break;
                case AssignmentOperator.LeftShiftAssign:
                    assignment.Value = SetAssignmentExpression<ShiftExpression, ShiftOperator>(ShiftOperator.LeftShift, assignment);
                    break;
                case AssignmentOperator.RightShiftAssign:
                    assignment.Value = SetAssignmentExpression<ShiftExpression, ShiftOperator>(ShiftOperator.RightShift, assignment);
                    break;
            }
            return assignment;
        }

        private TBinaryExpression SetAssignmentExpression<TBinaryExpression, TOperator>(TOperator @operator, AssignmentExpression assignmentExpression)
            where TBinaryExpression : BinaryExpression<TOperator>, new()
            where TOperator : Enum
        {
            return new TBinaryExpression
            {
                Source = assignmentExpression.Source,
                LeftOperand = assignmentExpression.Target,
                Operator = @operator,
                RightOperand = assignmentExpression.Value
            };
        }

        private AssignmentOperator VisitAssignmentOperator([NotNull] SCodeParser.AssignmentOperatorContext context)
        {
            return (context?.GetText()) switch
            {
                "=" => AssignmentOperator.Assign,
                "+=" => AssignmentOperator.AddAssign,
                "-=" => AssignmentOperator.SubtractAssign,
                "*=" => AssignmentOperator.MultiplyAssign,
                "/=" => AssignmentOperator.DivideAssign,
                "%=" => AssignmentOperator.ModuloAssign,
                "&=" => AssignmentOperator.AndAssign,
                "|=" => AssignmentOperator.OrAssign,
                "^=" => AssignmentOperator.XorAssign,
                "<<=" => AssignmentOperator.LeftShiftAssign,
                ">>=" => AssignmentOperator.RightShiftAssign,
                _ => throw new InvalidOperationException()
            };
        }

        #endregion

        #region Unary expressions

        public override Node VisitUnaryExpression([NotNull] SCodeParser.UnaryExpressionContext context)
        {
            if (context.primaryExpression() is { } primarExpressionContext)
            {
                return Visit(primarExpressionContext);
            }
            else if (context.castExpression() is { } castExpressionContext)
            {
                return Visit(castExpressionContext);
            }
            else
            {
                var unaryExpression = VisitExpression(context.unaryExpression());
                if (context.MINUS() is { })
                {
                    return CreateUnaryExpression<MinusExpression>(unaryExpression);
                }
                else if (context.BANG() is { })
                {
                    return CreateUnaryExpression<LogicalNotExpression>(unaryExpression);
                }
                else if (context.TILDE() is { })
                {
                    return CreateUnaryExpression<BitwiseNotExpression>(unaryExpression);
                }
                else if (context.OP_INC() is { })
                {
                    return CreateUnaryExpression<IncDecExpression>(unaryExpression, (opt) =>
                    {
                        opt.Operator = IncDecOperator.Increment;
                        opt.Order = Order.Pre;
                    });
                }
                else if (context.OP_DEC() is { })
                {
                    return CreateUnaryExpression<IncDecExpression>(unaryExpression, (opt) =>
                    {
                        opt.Operator = IncDecOperator.Decrement;
                        opt.Order = Order.Pre;
                    });
                }
                else if (context.AMP() is { })
                {
                    return CreateUnaryExpression<AddressOfExpression>(unaryExpression);
                }
                else if (context.STAR() is { })
                {
                    return CreateUnaryExpression<DereferenceExpression>(unaryExpression);
                }
                else
                {
                    return unaryExpression;
                }
            }
        }

        private TUnaryExpression CreateUnaryExpression<TUnaryExpression>(Expression baseExpression, Action<TUnaryExpression> options = null)
            where TUnaryExpression : UnaryExpression, new()
        {
            var expression = new TUnaryExpression
            {
                Source = baseExpression.Source,
                Target = baseExpression
            };
            options?.Invoke(expression);
            return expression;
        }

        #endregion

        #region Binary & Ternary expressions

        public override Expression VisitConditionalExpression([NotNull] SCodeParser.ConditionalExpressionContext context)
        {
            var baseExpression = VisitExpression(context.orConditionalOrExpression());
            if (context.expression().Length == 2)
            {
                return new TernaryExpression
                {
                    Condition = baseExpression,
                    True = VisitExpression(context.expression(0)),
                    False = VisitExpression(context.expression(1)),
                };
            }
            else
            {
                return baseExpression;
            }
        }

        public override Expression VisitAdditiveExpression([NotNull] SCodeParser.AdditiveExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.AdditiveExpressionContext, AdditiveExpression, AdditiveOperator>
                (context, context.multiplicativeExpression(), (ctx) => ctx.MINUS().Length > 0 ? AdditiveOperator.Subtract : AdditiveOperator.Add);
        }

        public override Expression VisitMultiplicativeExpression([NotNull] SCodeParser.MultiplicativeExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.MultiplicativeExpressionContext, MultiplicativeExpression, MultiplicativeOperator>
                (context, context.unaryExpression(), (ctx) =>
                    ctx.STAR().Length > 0 ? MultiplicativeOperator.Multiply :
                    ctx.DIV().Length > 0 ? MultiplicativeOperator.Divide : MultiplicativeOperator.Modulus);
        }

        public override Expression VisitAndBitwiseExpression([NotNull] SCodeParser.AndBitwiseExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.AndBitwiseExpressionContext, BitwiseExpression, BitwiseOperator>
                (context, context.equalityExpression(), (ctx) => BitwiseOperator.And);
        }

        public override Expression VisitOrBitwiseExpression([NotNull] SCodeParser.OrBitwiseExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.OrBitwiseExpressionContext, BitwiseExpression, BitwiseOperator>
                (context, context.xorBitwiseExpression(), (ctx) => BitwiseOperator.Or);
        }

        public override Expression VisitXorBitwiseExpression([NotNull] SCodeParser.XorBitwiseExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.XorBitwiseExpressionContext, BitwiseExpression, BitwiseOperator>
                (context, context.andBitwiseExpression(), (ctx) => BitwiseOperator.Xor);
        }

        public override Expression VisitOrConditionalOrExpression([NotNull] SCodeParser.OrConditionalOrExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.OrConditionalOrExpressionContext, LogicalExpression, LogicalOperator>
                (context, context.andConditionalExpression(), (ctx) => LogicalOperator.Or);
        }

        public override Expression VisitAndConditionalExpression([NotNull] SCodeParser.AndConditionalExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.AndConditionalExpressionContext, LogicalExpression, LogicalOperator>
                (context, context.orBitwiseExpression(), (ctx) => LogicalOperator.And);
        }

        public override Expression VisitEqualityExpression([NotNull] SCodeParser.EqualityExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.EqualityExpressionContext, EqualityExpression, EqualityOperator>
                (context, context.relationalExpression(), (ctx) => ctx.OP_EQ().Length > 0 ? EqualityOperator.Equal : EqualityOperator.NotEqual);
        }

        public override Expression VisitRelationalExpression([NotNull] SCodeParser.RelationalExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.RelationalExpressionContext, RelationalExpression, RelationalOperator>
                (context, context.shiftExpression(), (ctx) =>
                    ctx.LT().Length > 0 ? RelationalOperator.LessThan :
                    ctx.OP_LE().Length > 0 ? RelationalOperator.LessThanOrEqual :
                    ctx.GT().Length > 0 ? RelationalOperator.GreaterThan : RelationalOperator.GreaterThanOrEqual);
        }

        public override Expression VisitShiftExpression([NotNull] SCodeParser.ShiftExpressionContext context)
        {
            return CreateBinaryExpression
                <SCodeParser.ShiftExpressionContext, ShiftExpression, ShiftOperator>
                (context, context.additiveExpression(), (ctx) => ctx.OP_LEFT_SHIFT().Length > 0 ? ShiftOperator.LeftShift : ShiftOperator.RightShift);
        }

        private Expression CreateBinaryExpression<TParserRuleContext, TBinaryExpression, TOperator>(TParserRuleContext contextSource, ParserRuleContext[] contexts, Func<TParserRuleContext, TOperator> operationSelector)
            where TParserRuleContext : ParserRuleContext
            where TBinaryExpression : BinaryExpression<TOperator>, new()
            where TOperator : Enum
        {
            return VisitBinaryExpression(contexts, (left, right) =>
            {
                return new TBinaryExpression
                {
                    Source = SourceRange.FromParserContext(contextSource),
                    LeftOperand = left,
                    Operator = operationSelector(contextSource),
                    RightOperand = right,
                };
            });
        }

        private Expression VisitBinaryExpression(ParserRuleContext[] contexts, Func<Expression, Expression, Expression> func)
        {
            return (contexts?.Length) switch
            {
                0 => null,
                1 => VisitExpression(contexts[0]),
                2 => func(VisitExpression(contexts[0]), VisitExpression(contexts[1])),
                _ => func(VisitExpression(contexts[0]), VisitBinaryExpression(contexts.Skip(1).ToArray(), func))
            };
        }

        #endregion

        #region Identifier

        public override Identifier VisitIdentifier([NotNull] SCodeParser.IdentifierContext context)
        {
            return new Identifier(context.IDENTIFIER().GetText())
            {
                Source = SourceRange.FromParserContext(context)
            };
        }

        public override IdentifierExpression VisitIdentifierExpression([NotNull] SCodeParser.IdentifierExpressionContext context)
        {
            return new IdentifierExpression
            {
                Source = SourceRange.FromParserContext(context),
                Identifier = VisitIdentifier(context.identifier())
            };
        }

        #endregion

        #region Literals

        public override LiteralExpression VisitLiteralExpression([NotNull] SCodeParser.LiteralExpressionContext context)
        {
            return new LiteralExpression
            {
                Source = SourceRange.FromParserContext(context),
                Literal = VisitLiteral(context.literal())
            };
        }

        public override Literal VisitLiteral([NotNull] SCodeParser.LiteralContext context)
        {
            if (context.LITERAL_INT() is { } i)
            {
                var fromBase = 10; // Default mode: decimal value
                var input = i.GetText();
                if (input.Length > 2 && input[0] == '0')
                {
                    switch (input[1])
                    {
                        case 'b': fromBase = 2; break;
                        case 'x': fromBase = 16; break;
                    }
                    input = input.Substring(2);
                }
                int int32number = Convert.ToInt32(input, fromBase);
                if (int32number >= 0 && int32number <= 0xFFFF)
                {
                    return new LiteralInt()
                    {
                        Source = SourceRange.FromParserContext(context),
                        Value = (short)int32number
                    };
                }
                else
                {
                    return new LiteralLong()
                    {
                        Source = SourceRange.FromParserContext(context),
                        Value = int32number
                    };
                }
            }
            else if (context.LITERAL_DECIMAL() is { } d)
            {
                return new LiteralDecimal()
                {
                    Source = SourceRange.FromParserContext(context),
                    Value = Decimal.Parse(d.GetText(), CultureInfo.InvariantCulture)
                };
            }
            else if (context.literalBoolean() is { } boolean)
            {
                return new LiteralBoolean()
                {
                    Source = SourceRange.FromParserContext(context),
                    Value = boolean.GetText() == "true"
                };
            }
            else if (context.LITERAL_CHAR() is { } s)
            {
                return new LiteralChar()
                {
                    Source = SourceRange.FromParserContext(context),
                    Value = s.GetText().RemoveCharsContainer().Unescape().FirstOrDefault()
                };
            }
            else if (context.LITERAL_STRING() is { } c)
            {
                return new LiteralString()
                {
                    Source = SourceRange.FromParserContext(context),
                    Value = c.GetText().RemoveCharsContainer().Unescape()
                };
            }
            else if (context.NULL() != null)
            {
                return new LiteralInt()
                {
                    Source = SourceRange.FromParserContext(context),
                    Value = 0
                };
            }
            else
            {
                throw new InvalidOperationException($"{context.GetType().Name} is not a valid literal");
            }
        }

        #endregion

        #region Helpers

        protected override Node AggregateResult(Node aggregate, Node nextResult)
        {
            return aggregate ?? nextResult;
        }

        public TNode Visit<TNode>(ParserRuleContext context, bool isRequired = false)
        {
            if (context != null)
            {
                var node = Visit(context);
                if (node is TNode typedNode)
                {
                    return typedNode;
                }
                else if (isRequired)
                {
                    var source = SourceRange.FromParserContext(context);
                    throw new InvalidOperationException($"{source}: {node.GetType().Name} is not {typeof(TNode).Name}") { Source = source.ToString() };
                }
            }
            return default;
        }

        #endregion
    }
}
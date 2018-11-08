using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HelloRoslyn
{
    public class ControlFlowGraph
    {
        private SyntaxNode entryPoint;
        private Dictionary<SyntaxNode, HashSet<SyntaxNode>> predecessors = new Dictionary<SyntaxNode, HashSet<SyntaxNode>>();
        private Dictionary<SyntaxNode, Dictionary<SyntaxNode, int>> successors = new Dictionary<SyntaxNode, Dictionary<SyntaxNode, int>>();

        public ControlFlowGraph(MethodDeclarationSyntax m, SemanticModel model)
        {
            entryPoint = m;
            var exitPoint = SyntaxFactory.ReturnStatement();
            successors[entryPoint] = new Dictionary<SyntaxNode, int>();
            predecessors[exitPoint] = new HashSet<SyntaxNode>();
            
            var incoming = new Dictionary<SyntaxNode, int>();
            incoming.Add(entryPoint, -1);

            var immediateReturn = new Dictionary<SyntaxNode, int>();

            linkThen(analyzeChildren(m, model, incoming, immediateReturn), exitPoint, -1);
            linkThen(immediateReturn, exitPoint, -1);

            successors.Add(exitPoint, new Dictionary<SyntaxNode, int>());
        }

        private Dictionary<SyntaxNode, int> analyzeChildren(SyntaxNode n, SemanticModel model, Dictionary<SyntaxNode, int> incoming, Dictionary<SyntaxNode, int> immediateReturn)
        {
            foreach (var s in n.ChildNodes().OfType<StatementSyntax>())
            {
                incoming = analyze(s, model, incoming, immediateReturn);
            }
            return incoming;
        }

        private Dictionary<SyntaxNode, int> analyze(StatementSyntax s, SemanticModel model, Dictionary<SyntaxNode, int> incoming, Dictionary<SyntaxNode, int> immediateReturn)
        {
            //linkThen(incoming, s, -1);

            if (s is BlockSyntax)
            {
                return analyzeChildren(s, model, incoming, immediateReturn);
            }
            else if (s is IfStatementSyntax)
            {
                var i = s as IfStatementSyntax;
                var failCase = analyzeCondition(i.Condition, model, incoming);
                var incoming2 = new Dictionary<SyntaxNode, int>(incoming);
                incoming = analyze(i.Statement, model, incoming, immediateReturn);
                if (i.Else != null)
                {
                    foreach (var pair in analyze(i.Else.Statement, model, failCase, immediateReturn))
                    {
                        incoming.Add(pair.Key, pair.Value);
                    }
                }
                else
                {
                    foreach (var pair in failCase)
                    {
                        if (!incoming.ContainsKey(pair.Key))
                        {
                            incoming.Add(pair.Key, pair.Value);
                        }
                        else
                        {
                            incoming[pair.Key] = -1;
                        }
                    }
                }
                return incoming;
            }
            else if (s is WhileStatementSyntax)
            {
                var w = s as WhileStatementSyntax;
                var failCase = analyzeCondition(w.Condition, model, incoming);
                incoming = analyze(w.Statement, model, incoming, immediateReturn);
                //linkThen(incoming, w.Condition, -1);
                analyzeCondition(w.Condition, model, incoming);
                return failCase;
            }
            else if (s is ForStatementSyntax)
            {
                var f = s as ForStatementSyntax;
                if (f.Declaration != null)
                {
                    linkThen(incoming, f.Declaration, -1);
                }
                else
                {
                    foreach (var i in f.Initializers)
                    {
                        linkThen(incoming, i, -1);
                    }
                }
                var falseCase = analyzeCondition(f.Condition, model, incoming);
                incoming = analyze(f.Statement, model, incoming, immediateReturn);
                foreach (var i in f.Incrementors)
                {
                    linkThen(incoming, i, -1);
                }
                linkThen(incoming, f.Condition, -1);
                return falseCase;
            }
            else if (s is ReturnStatementSyntax)
            {
                linkThen(incoming, s, -1);

                immediateReturn.Add(s, -1);
                incoming.Clear();
                return incoming;
            }
            else
            {
                linkThen(incoming, s, -1);

                return incoming;
            }
        }

        private Dictionary<SyntaxNode, int> analyzeCondition(ExpressionSyntax condition, SemanticModel model, Dictionary<SyntaxNode, int> incoming)
        {
            if (condition is BinaryExpressionSyntax)
            {
                var b = condition as BinaryExpressionSyntax;
                if (b.OperatorToken.ToString().Equals("&&"))
                {
                    var lFail = analyzeCondition(b.Left, model, incoming);
                    var rFail = analyzeCondition(b.Right, model, incoming);
                    foreach (var pair in rFail)
                    {
                        lFail.Add(pair.Key, pair.Value);
                    }
                    return lFail;
                }
                else if (b.OperatorToken.ToString().Equals("||"))
                {
                    var lFail = analyzeCondition(b.Left, model, incoming);
                    var rFail = analyzeCondition(b.Right, model, lFail);
                    foreach (var pair in lFail)
                    {
                        incoming.Add(pair.Key, pair.Value);
                    }
                    return rFail;
                }
            }

            if (condition is PrefixUnaryExpressionSyntax)
            {
                var p = condition as PrefixUnaryExpressionSyntax;
                if (p.OperatorToken.ToString().Equals("!"))
                {
                    var insideFail = analyzeCondition(p.Operand, model, incoming);
                    var outsideFail = new Dictionary<SyntaxNode, int>(incoming);
                    incoming.Clear();
                    foreach (var f in insideFail)
                    {
                        incoming.Add(f.Key, f.Value);
                    }
                    return outsideFail;
                }
            }

            linkThen(incoming, condition, 1);
            var falseCase = new Dictionary<SyntaxNode, int>();
            falseCase.Add(condition, 0);
            return falseCase;
        }
        
        /**
         * Link all nodes in incoming to point to n.  Then make incoming = {n}.
         */
        private void linkThen(Dictionary<SyntaxNode, int> incoming, SyntaxNode n, int value)
        {
            foreach (var i in incoming)
            {
                add(lookup(successors, i.Key), n, i.Value);
                add(lookup(predecessors, n), i.Key);
            }

            incoming.Clear();
            incoming.Add(n, value);
        }

        private void add(Dictionary<SyntaxNode, int> dictionary, SyntaxNode n, int value)
        {
            if (!dictionary.ContainsKey(n))
            {
                dictionary.Add(n, value);
            }
        }

        private void add(HashSet<SyntaxNode> hashSet, SyntaxNode key)
        {
            if (!hashSet.Contains(key))
            {
                hashSet.Add(key);
            }
        }

        private Dictionary<SyntaxNode, int> lookup(Dictionary<SyntaxNode, Dictionary<SyntaxNode, int>> dict, SyntaxNode node)
        {
            if (dict.ContainsKey(node))
            {
                return dict[node];
            }
            else
            {
                var h = new Dictionary<SyntaxNode, int>();
                dict.Add(node, h);
                return h;
            }
        }

        private HashSet<SyntaxNode> lookup(Dictionary<SyntaxNode, HashSet<SyntaxNode>> dict, SyntaxNode node)
        {
            if (dict.ContainsKey(node))
            {
                return dict[node];
            }
            else
            {
                var h = new HashSet<SyntaxNode>();
                dict.Add(node, h);
                return h;
            }
        }

        public SyntaxNode GetEntryPoint()
        {
            return entryPoint;
        }
        public ISet<SyntaxNode> GetSuccessors(SyntaxNode node)
        {
            return new HashSet<SyntaxNode>(successors[node].Keys);
        }
        public Dictionary<SyntaxNode, int> GetSuccessorsB(SyntaxNode node)
        {
            return successors[node];
        }
        public ISet<SyntaxNode> GetPredecessors(SyntaxNode node)
        {
            return predecessors[node];
        }
    }
}
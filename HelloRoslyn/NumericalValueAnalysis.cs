using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace HelloRoslyn
{
    public class NumericalValueAnalysis
    {
        private const bool debug = true;

        public NumericalValueAnalysis(ControlFlowGraph graph)
        {
            analyze(graph, graph.GetEntryPoint(), new Dictionary<string, NumericalValue<int>>(), new Dictionary<SyntaxNode, Dictionary<string, NumericalValue<int>>>());
        }

        private void analyze(ControlFlowGraph graph, SyntaxNode node, Dictionary<string, NumericalValue<int>> variables, Dictionary<SyntaxNode, Dictionary<string, NumericalValue<int>>> history)
        {
            #region Union usagesAfter with reachingDefinitions
            if (history.ContainsKey(node))
            {
                bool anyChanged = false;
                foreach (var pair in history[node])
                {
                    if (variables.ContainsKey(pair.Key))
                    {
                        var i = variables[pair.Key];
                        string old = i.ToString();
                        //Console.WriteLine("{0} vs {1} and {2}", old, i, pair.Value);
                        i.UnionWith(pair.Value);
                        if (!old.Equals(i.ToString()))
                        {
                            anyChanged = true;
                        }
                    }
                    else
                    {
                        variables.Add(pair.Key, pair.Value.Clone());
                        anyChanged = true;
                    }
                }
                if (history[node].Count == 0 && variables.Count != 0)
                {
                    anyChanged = true;
                }
                if (!anyChanged)
                {
                    return;
                }
                else
                {
                    var clone = new Dictionary<string, NumericalValue<int>>();
                    foreach (var v in variables)
                    {
                        clone.Add(v.Key, v.Value.Clone());
                    }
                    history[node] = clone;
                }
            }
            else
            {
                var clone = new Dictionary<string, NumericalValue<int>>();
                foreach (var v in variables)
                {
                    clone.Add(v.Key, v.Value.Clone());
                }
                history.Add(node, clone);
            }
            #endregion

            NumericalValue<int> rangeTrue = null;
            NumericalValue<int> rangeFalse = null;
            string rangeVar = null;

            #region Process node
            if (node is LocalDeclarationStatementSyntax || node is VariableDeclarationSyntax)
            {
                VariableDeclarationSyntax s;
                if (node is LocalDeclarationStatementSyntax)
                {
                    s = (node as LocalDeclarationStatementSyntax).Declaration;
                }
                else
                {
                    s = node as VariableDeclarationSyntax;
                }

                foreach (var v in s.Variables)
                {
                    if (v.Initializer != null)
                    {
                        if (v.Initializer.Value is LiteralExpressionSyntax)
                        {
                            var valS = (v.Initializer.Value as LiteralExpressionSyntax).Token.ValueText;
                            int valI;
                            if (int.TryParse(valS, out valI))
                            {
                                var valNV = new NumericalValue<int>(valI);
                                if (variables.ContainsKey(v.Identifier.ToString()))
                                {
                                    variables[v.Identifier.ToString()].AssignValue(valNV);
                                }
                                else
                                {
                                    variables.Add(v.Identifier.ToString(), valNV);
                                }
                            }
                        }
                        else if (v.Initializer.Value is IdentifierNameSyntax)
                        {
                            var valNV = variables[v.Initializer.Value.ToString()];
                            if (variables.ContainsKey(v.Identifier.ToString()))
                            {
                                variables[v.Identifier.ToString()].AssignValue(valNV);
                            }
                            else
                            {
                                variables.Add(v.Identifier.ToString(), valNV);
                            }
                        }
                        else
                        {
                            var valNV = new NumericalValue<int>(int.MinValue, Inclusivity.INCLUSIVE, int.MaxValue, Inclusivity.INCLUSIVE);
                            if (variables.ContainsKey(v.Identifier.ToString()))
                            {
                                variables[v.Identifier.ToString()].AssignValue(valNV);
                            }
                            else
                            {
                                variables.Add(v.Identifier.ToString(), valNV);
                            }
                        }
                    }
                }
            }
            else if (node is ExpressionStatementSyntax && (node as ExpressionStatementSyntax).Expression is AssignmentExpressionSyntax)
            {
                var s = (node as ExpressionStatementSyntax).Expression as AssignmentExpressionSyntax;
                if (s.Right is LiteralExpressionSyntax)
                {
                    var valS = (s.Right as LiteralExpressionSyntax).Token.ValueText;
                    int valI;
                    if (int.TryParse(valS, out valI))
                    {
                        var valNV = new NumericalValue<int>(valI);
                        if (variables.ContainsKey(s.Left.ToString()))
                        {
                            variables[s.Left.ToString()].AssignValue(valNV);
                        }
                        else
                        {
                            variables.Add(s.Left.ToString(), valNV);
                        }
                    }
                }
                else if (s.Right is IdentifierNameSyntax)
                {
                    var valNV = variables[s.Right.ToString()];
                    if (variables.ContainsKey(s.Left.ToString()))
                    {
                        variables[s.Left.ToString()].AssignValue(valNV);
                    }
                    else
                    {
                        variables.Add(s.Left.ToString(), valNV);
                    }
                }
                else
                {
                    var valNV = new NumericalValue<int>(int.MinValue, Inclusivity.INCLUSIVE, int.MaxValue, Inclusivity.INCLUSIVE);
                    if (variables.ContainsKey(s.Left.ToString()))
                    {
                        variables[s.Left.ToString()].AssignValue(valNV);
                    }
                    else
                    {
                        variables.Add(s.Left.ToString(), valNV);
                    }
                }
            }
            else if (node is BinaryExpressionSyntax)
            {
                var n = node as BinaryExpressionSyntax;
                if (n.Right is LiteralExpressionSyntax)
                {
                    int value;
                    if (int.TryParse((n.Right as LiteralExpressionSyntax).Token.ValueText, out value))
                    {
                        if (n.OperatorToken.ToString().Equals("<"))
                        {
                            rangeTrue = new NumericalValue<int>(int.MinValue, Inclusivity.INCLUSIVE, value, Inclusivity.EXCLUSIVE);
                            rangeFalse = new NumericalValue<int>(value, Inclusivity.INCLUSIVE, int.MaxValue, Inclusivity.INCLUSIVE);
                            rangeVar = n.Left.ToString();
                        }
                        if (n.OperatorToken.ToString().Equals("<="))
                        {
                            rangeTrue = new NumericalValue<int>(int.MinValue, Inclusivity.INCLUSIVE, value, Inclusivity.INCLUSIVE);
                            rangeFalse = new NumericalValue<int>(value, Inclusivity.EXCLUSIVE, int.MaxValue, Inclusivity.INCLUSIVE);
                            rangeVar = n.Left.ToString();
                        }
                        if (n.OperatorToken.ToString().Equals(">"))
                        {
                            rangeTrue = new NumericalValue<int>(value, Inclusivity.EXCLUSIVE, int.MaxValue, Inclusivity.INCLUSIVE);
                            rangeFalse = new NumericalValue<int>(int.MinValue, Inclusivity.INCLUSIVE, value, Inclusivity.INCLUSIVE);
                            rangeVar = n.Left.ToString();
                        }
                        if (n.OperatorToken.ToString().Equals(">="))
                        {
                            rangeTrue = new NumericalValue<int>(value, Inclusivity.INCLUSIVE, int.MaxValue, Inclusivity.INCLUSIVE);
                            rangeFalse = new NumericalValue<int>(int.MinValue, Inclusivity.INCLUSIVE, value, Inclusivity.EXCLUSIVE);
                            rangeVar = n.Left.ToString();
                        }
                    }
                }
            }

            if (debug)
            {
                Console.WriteLine("({0}): {1} {2}", node.GetLocation().GetLineSpan().StartLinePosition, node, node.GetType());
                foreach (var pair in variables)
                {
                    Console.WriteLine("  {0} -> {1}", pair.Key, pair.Value);
                }
            }
            #endregion

            #region Recurse
            foreach (var succ in graph.GetSuccessorsB(node))
            {
                var variablesPrime = new Dictionary<string, NumericalValue<int>>();
                foreach (var v in variables)
                {
                    variablesPrime.Add(v.Key, v.Value.Clone());
                }
                if (succ.Value == 0)
                {
                    variablesPrime[rangeVar].IntersectWith(rangeFalse);
                }
                else if (succ.Value == 1)
                {
                    variablesPrime[rangeVar].IntersectWith(rangeTrue);
                }
                analyze(graph, succ.Key, variablesPrime, history);
            }
            #endregion
        }
    }
}
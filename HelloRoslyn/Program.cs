using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelloRoslyn
{
    class Program
    {
        static void Main(string[] args)
        {
            /*{
                var n = new NumericalValue<int>();
                n.UnionWith(new NumericalValue<int>(0, Inclusivity.INCLUSIVE, 3, Inclusivity.INCLUSIVE));
                AssertEquals(n.ToString(), "[0, 3]");
                n.UnionWith(new NumericalValue<int>(0, Inclusivity.INCLUSIVE, 3, Inclusivity.INCLUSIVE));
                AssertEquals(n.ToString(), "[0, 3]");
                n.UnionWith(new NumericalValue<int>(1, Inclusivity.INCLUSIVE, 4, Inclusivity.EXCLUSIVE));
                AssertEquals(n.ToString(), "[0, 4)");
                n.UnionWith(new NumericalValue<int>(7, Inclusivity.INCLUSIVE, 10, Inclusivity.INCLUSIVE));
                AssertEquals(n.ToString(), "[0, 4) U [7, 10]");
                n.UnionWith(new NumericalValue<int>(6, Inclusivity.INCLUSIVE, 7, Inclusivity.EXCLUSIVE));
                AssertEquals(n.ToString(), "[0, 4) U [6, 10]");
                n.UnionWith(new NumericalValue<int>(3, Inclusivity.INCLUSIVE, 7, Inclusivity.EXCLUSIVE));
                AssertEquals(n.ToString(), "[0, 10]");
            }
            {
                var n = new NumericalValue<int>();
                n.UnionWith(new NumericalValue<int>(0, Inclusivity.INCLUSIVE, 4, Inclusivity.EXCLUSIVE));
                n.UnionWith(new NumericalValue<int>(6, Inclusivity.INCLUSIVE, 10, Inclusivity.INCLUSIVE));
                n.UnionWith(new NumericalValue<int>(-2, Inclusivity.INCLUSIVE, -2, Inclusivity.INCLUSIVE));
                n.UnionWith(new NumericalValue<int>(14, Inclusivity.EXCLUSIVE, 15, Inclusivity.EXCLUSIVE));
                AssertEquals(n.ToString(), "[-2, -2] U [0, 4) U [6, 10] U (14, 15)");
                n.IntersectWith(3, Inclusivity.INCLUSIVE, 6, Inclusivity.INCLUSIVE);
                AssertEquals(n.ToString(), "[3, 4) U [6, 6]");
            }*/

            SyntaxTree tree = CSharpSyntaxTree.ParseText(
            @"using System;
            using System.Collections;
            using System.Linq;
            using System.Text;
 
            namespace HelloWorld
            {
                class ProgramUnderTest
                {

static void Main(string[] args) {}
int f() { return 4; }

private void Test() {
    int j = 3;
    for (int i = 0; i < 10; i = i + 1) {
        if (i > j) { j = i + 1; }
    }
    return;
/*
    int i = 0;
    i = 3;
    int j = f();
    if (j + 3 > 5) {
        i = j;
    } else {
        i = -1;
    }
    return;*/
}
                }
            }");

            var compilation = CompileCode(tree);
            var model = compilation.GetSemanticModel(tree);
            foreach (var c in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                foreach (var m in c.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (m.Identifier.ToString().Equals("Test"))
                    {
                        ControlFlowGraph controlFlowGraph = new ControlFlowGraph(m, model);
                        Console.WriteLine("digraph {0} {{", "Test");
                        HashSet<SyntaxNode> visitedNodes = new HashSet<SyntaxNode>();
                        printGraphViz(controlFlowGraph, controlFlowGraph.GetEntryPoint(), visitedNodes);
                        Console.WriteLine();
                        foreach (var node in visitedNodes)
                        {
                            if (node.GetLocation().ToString().Contains("[0..7)"))
                            {
                                Console.WriteLine("\"{0}\" [color=\"#55AAFF\"];", node.GetLocation().ToString());
                            }
                            Console.WriteLine("\"{0}\" [label=\"{1}\"];", node.GetLocation().ToString(), interpolateLabel(node.ToFullString()));
                        }
                        Console.WriteLine("}");

                        Console.Write("\n\n\n");

                        /*NumericalValueAnalysis numericalValueAnalysis = new NumericalValueAnalysis(controlFlowGraph);*/
                        StringBuilder builder = new StringBuilder();
                        builder.Append("{ \"nodes\": [\n");
                        outputNumericalValueOutput(controlFlowGraph, controlFlowGraph.GetEntryPoint(), new HashSet<SyntaxNode>(), builder);
                        builder.Append("\n] }");

                        Console.WriteLine("Builder: '\n{0}\n'", builder);
                    }
                }
            }


            Console.WriteLine();
            Console.Write("Press enter to continue...");
            Console.ReadLine();
        }

        private static void outputNumericalValueOutput(ControlFlowGraph graph, SyntaxNode node, HashSet<SyntaxNode> history, StringBuilder builder)
        {
            if (!history.Contains(node))
            {
                history.Add(node);
                builder.AppendFormat("  {{ \"key\": \"{0}\",\n    \"value\": ", location(node));
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
                    builder.Append("{ \"type\": \"variable_declaration\", \"declarations\": [");

                    bool firstVar = true;
                    foreach (var v in s.Variables)
                    {
                        if (firstVar)
                        {
                            firstVar = false;
                        }
                        else
                        {
                            builder.Append(",");
                        }
                        builder.AppendFormat(" {{ \"identifier\": \"{0}\", \"initializer\": ", v.Identifier);
                        if (v.Initializer == null)
                        {
                            builder.Append("null");
                        }
                        else
                        {
                            outputNumericalValueOutput(v.Initializer.Value, builder);
                        }
                        builder.Append(" }");
                    }
                    builder.Append(" ] }");
                }
                else if (node is AssignmentExpressionSyntax || (node is ExpressionStatementSyntax && (node as ExpressionStatementSyntax).Expression is AssignmentExpressionSyntax))
                {
                    AssignmentExpressionSyntax s;
                    if (node is AssignmentExpressionSyntax)
                    {
                        s = node as AssignmentExpressionSyntax;
                    }
                    else
                    {
                        s = (node as ExpressionStatementSyntax).Expression as AssignmentExpressionSyntax;
                    }
                    builder.AppendFormat("{{ \"type\": \"variable_assignment\", \"left\": \"{0}\", \"right\": ", s.Left);
                    outputNumericalValueOutput(s.Right, builder);
                    builder.Append(" }");
                }
                else if (node is BinaryExpressionSyntax)
                {
                    builder.Append("{ \"type\": \"comparison\", \"left\": ");
                    var s = node as BinaryExpressionSyntax;
                    outputNumericalValueOutput(s.Left, builder);
                    builder.AppendFormat(", \"op\": \"{0}\", \"right\": ", s.OperatorToken);
                    outputNumericalValueOutput(s.Right, builder);
                    builder.Append(" }");
                }
                else
                {
                    Console.WriteLine("node is {0} {1}", node, node.GetType());
                    builder.Append("{ \"type\": \"other\" }");
                }
                builder.Append(",\n    \"successors\": [");
                bool firstSucc = true;
                foreach (var succ in graph.GetSuccessorsB(node))
                {
                    if (firstSucc) { firstSucc = false; }
                    else { builder.Append(", "); }
                    builder.AppendFormat("{{ \"key\": \"{0}\", \"value\": {1} }}", location(succ.Key), succ.Value);
                }
                builder.Append("] }");
                foreach (var succ in graph.GetSuccessors(node))
                {
                    if (!history.Contains(succ))
                    {
                        builder.Append(",\n");
                    }
                    outputNumericalValueOutput(graph, succ, history, builder);
                }
            }
        }

        private static string location(SyntaxNode key)
        {
            var loc = key.GetLocation().GetMappedLineSpan();
            return string.Format("({0}) - ({1})", loc.StartLinePosition, loc.EndLinePosition);
        }

        private static void outputNumericalValueOutput(ExpressionSyntax value, StringBuilder builder)
        {
            if (value is BinaryExpressionSyntax)
            {
                var b = value as BinaryExpressionSyntax;
                builder.Append("{ \"left\": ");
                outputNumericalValueOutput(b.Left, builder);
                builder.AppendFormat(", \"op\": \"{0}\", \"right\": ", b.OperatorToken);
                outputNumericalValueOutput(b.Right, builder);
                builder.Append(" }");
            }
            else if (value is LiteralExpressionSyntax)
            {
                builder.Append(value.ToString());
            }
            else if (value is IdentifierNameSyntax)
            {
                builder.AppendFormat("\"{0}\"", value.ToString());
            }
            else
            {
                builder.Append("null");
            }
        }

        private static void AssertEquals(object v1, object v2)
        {
            if (!v1.Equals(v2))
            {
                throw new Exception(String.Format("Assertion error: {0} != {1}", v1, v2));
            }
        }

        private static string interpolateLabel(string v)
        {
            StringBuilder builder = new StringBuilder();
            int afterNewLine = 2;
            foreach (char c in v)
            {
                if (afterNewLine != 0 && Char.IsWhiteSpace(c) || c == '\r')
                {
                }
                else if (c == '\n')
                {
                    if (afterNewLine == 0)
                    {
                        afterNewLine = 1;
                    }
                }
                else if (afterNewLine != 0)
                {
                    if (afterNewLine == 1)
                    {
                        builder.Append(' ');
                    }
                    afterNewLine = 0;
                    builder.Append(c);
                }
                else {
                    afterNewLine = 0;
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        private static void printGraphViz(ControlFlowGraph controlFlowGraph, SyntaxNode node, HashSet<SyntaxNode> visitedNodes)
        {
            if (!visitedNodes.Contains(node))
            {
                visitedNodes.Add(node);
                var succs = controlFlowGraph.GetSuccessorsB(node);
                foreach (var succ in succs)
                {
                    int v = succ.Value;
                    var s = succ;
                    /*while (true)
                    {
                        var su = controlFlowGraph.GetSuccessorsB(s.Key);
                        if (su.Count != 1)
                        {
                            break;
                        }
                        s = su.Single();
                    }*/
                    if (v == 1)
                    {
                        Console.WriteLine("\"{0}\" -> \"{1}\" [color=\"#00ff00\"];", node.GetLocation().ToString(), s.Key.GetLocation().ToString());
                    }
                    if (v == 0)
                    {
                        Console.WriteLine("\"{0}\" -> \"{1}\" [color=\"#ff0000\"];", node.GetLocation().ToString(), s.Key.GetLocation().ToString());
                    }
                    if (v == -1)
                    {
                        Console.WriteLine("\"{0}\" -> \"{1}\";", node.GetLocation().ToString(), s.Key.GetLocation().ToString());
                    }
                    printGraphViz(controlFlowGraph, s.Key, visitedNodes);
                }
            }
        }

        private static CSharpCompilation CompileCode(SyntaxTree tree)
        {
            Console.WriteLine("=====COMPILATION=====");
            // Q: What does CSharpCompilation.Create do?
            var Mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var linq = MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location);
            var compilation = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: new[] { tree }, references: new[] { Mscorlib, linq });

            // Print all the ways this snippet is horribly coded.
            // FIXME: how do we fix the Linq error?
            Console.WriteLine("Compile errors: ");
            Console.WriteLine(compilation.GetDiagnostics().Select(s => s.ToString()).Aggregate((s, s2) => s.ToString() + "\n" + s2.ToString()));
            Console.WriteLine();
            
            // FIXME: how do I abort on only the most serious errors?
            if (compilation.GetDiagnostics().Any(s => s.Severity == DiagnosticSeverity.Error))
            {
                throw new Exception("Compilation failed.");
            }

            return compilation;
        }
    }
}

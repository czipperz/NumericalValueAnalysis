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
    class EqualsFinder : CSharpSyntaxWalker
    {
        private SemanticModel model;

        public EqualsFinder(SemanticModel model)
        {
            this.model = model;
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            base.VisitBinaryExpression(node);
            if (node.OperatorToken.ToString().Equals("==") || node.OperatorToken.ToString().Equals("!="))
            {
                Console.WriteLine("EQUALS" + node.GetLocation().GetMappedLineSpan() + " : " + node);
                if (node.Left is ObjectCreationExpressionSyntax || node.Right is ObjectCreationExpressionSyntax)
                {
                    Console.WriteLine("Statically decided to be " + node.OperatorToken.ToString().Equals("!="));
                }
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);
            
            SyntaxNode[] nodes = node.Expression.ChildNodes().ToArray();
            
            if (nodes[1].ToString().Equals("Equals"))
            {
                var o = nodes[0];
                ITypeSymbol ot = model.GetTypeInfo(o).Type;
                var arg = node.ArgumentList.Arguments.Single();
                ITypeSymbol argt = model.GetTypeInfo(arg.Expression).Type;
                Console.WriteLine(node.GetLocation().GetMappedLineSpan() + " : " + nodes[0] + " . Equals (" + arg.ToString() + ")");
                Console.WriteLine(node.GetLocation().GetMappedLineSpan() + " : " + ot + " . Equals (" + argt + ")");
                
                    if (!parent(ot, argt) && !parent(argt, ot))
                {
                    Console.WriteLine("Error here");
                }
                else
                {
                    Console.WriteLine("OK here");
                }

                Console.WriteLine();
            }
        }

        /** Test if b extends a */
        private bool parent(ITypeSymbol a, ITypeSymbol b)
        {
            for (var bBase = b; bBase != null; bBase = bBase.BaseType)
            {
                if (bBase.Equals(a))
                {
                    return true;
                }
            }
            foreach (var i in b.AllInterfaces)
            {
                if (i.Equals(a))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

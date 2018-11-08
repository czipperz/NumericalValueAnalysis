using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelloRoslyn
{
    class ToStringFinder : CSharpSyntaxWalker
    {
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);
            // Q: why is this if-statement a questionable way to find calls to ToString?
            // FIXME: come up with a better if statement.
            if (node.Expression.ToString().Contains("ToString"))
            {
                // Q: what is a better way to "remember" calls to ToString besides printing to the console?
                Console.WriteLine("Found ToString at "+node.GetLocation().GetMappedLineSpan());
            }
        }

        // Q: how can we easily add to this walker to visit other types of expressions?
        
        // Q: when should we add code here vs making a new walker?
    }
}

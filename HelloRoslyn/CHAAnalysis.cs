using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HelloRoslyn
{
    class CHAAnalysis
    {
        private Dictionary<string, HashSet<INamedTypeSymbol>> parentToChildren =
            new Dictionary<string, HashSet<INamedTypeSymbol>>();
        private Dictionary<Location, HashSet<ITypeSymbol>> typeResolutions =
            new Dictionary<Location, HashSet<ITypeSymbol>>();
        private Dictionary<string, HashSet<ITypeSymbol>> previousConstructedTypesIn =
            new Dictionary<string, HashSet<ITypeSymbol>>();
        private SemanticModel model;

        public CHAAnalysis(SemanticModel model)
        {
            this.model = model;
        }

        public void AnalyzeMain(IEnumerable<ClassDeclarationSyntax> classDeclarations)
        {
            AnalyzeFunction(classDeclarations, new HashSet<ITypeSymbol>(), null, "Main");
        }
        
        private void AnalyzeFunction(IEnumerable<ClassDeclarationSyntax> classDeclarations, HashSet<ITypeSymbol> constructedTypesIn, ITypeSymbol targetClass, string targetMethod) {
            var constructedTypesOut = constructedTypesIn;

            foreach (var classDeclaration in classDeclarations)
            {
                if (targetClass != null && !targetClass.Equals(model.GetDeclaredSymbol(classDeclaration))) { continue; }

                foreach (var field in classDeclaration.Members.OfType<FieldDeclarationSyntax>())
                {
                    Console.WriteLine();
                    var eq = field.DescendantNodes().OfType<EqualsValueClauseSyntax>();
                    if (eq.Count() == 0)
                    {
                        typeResolutions.Add(field.GetLocation(), new HashSet<ITypeSymbol>());
                    }
                    else
                    {
                        //x = new A()
                        var create = eq.First().ChildNodes().OfType<ObjectCreationExpressionSyntax>();
                        if (create.Count() == 0)
                        {
                            typeResolutions.Add(field.GetLocation(), new HashSet<ITypeSymbol>());
                        }
                        else
                        {
                            var hs = new HashSet<ITypeSymbol>();
                            hs.Add(model.GetTypeInfo(create.First()).Type);
                            if (typeResolutions.ContainsKey(field.GetLocation()))
                            {
                                typeResolutions[field.GetLocation()].UnionWith(hs);
                            }
                            else
                            {
                                typeResolutions.Add(field.GetLocation(), hs);
                            }
                        }
                    }
                }

                foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!method.Identifier.ToString().Equals(targetMethod)) { continue; }
                    
                    HashSet<ITypeSymbol> constructedTypes = new HashSet<ITypeSymbol>(constructedTypesIn);

                    foreach (var param in method.ParameterList.Parameters)
                    {
                        var type = model.GetTypeInfo(param.Type).Type;
                        constructedTypes.UnionWith(constructedTypesIn.Where(t => isParent(type, t)));
                    }

                    if (targetClass != null)
                    {
                        string str = stringify(targetClass) + "." + targetMethod;
                        Console.WriteLine("Lookup {0}", str);
                        if (previousConstructedTypesIn.ContainsKey(str))
                        {
                            var p = previousConstructedTypesIn[str];
                            var len = p.Count;
                            p.UnionWith(constructedTypes);
                            if (len != p.Count)
                            {
                                constructedTypes = p;
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            previousConstructedTypesIn.Add(str, new HashSet<ITypeSymbol>(constructedTypes));
                        }
                    }

                    Console.WriteLine();

                    var expressions = method.Body.DescendantNodesAndSelf();
                    foreach (var constructs in expressions.OfType<ObjectCreationExpressionSyntax>())
                    {
                        constructedTypes.Add(model.GetTypeInfo(constructs).Type);
                        Console.WriteLine("Construct {0}", model.GetTypeInfo(constructs).Type);
                    }

                    for (bool changed = true; changed;)
                    {
                        changed = false;
                        foreach (var name in expressions.OfType<IdentifierNameSyntax>())
                        {
                            var type = model.GetTypeInfo(name).Type;
                            var sym = model.GetSymbolInfo(name).Symbol;
                            if (sym.Kind == SymbolKind.Field)
                            {
                                var loc = sym.Locations.First();
                                if (name.Parent is AssignmentExpressionSyntax && (name.Parent as AssignmentExpressionSyntax).Left.Equals(name))
                                {
                                    if (!typeResolutions.ContainsKey(loc))
                                    {
                                        typeResolutions.Add(loc, new HashSet<ITypeSymbol>());
                                    }
                                    var len = typeResolutions[loc].Count;
                                    typeResolutions[loc].UnionWith(constructedTypes.Where(t => isParent(type, t)));
                                    changed |= (len != typeResolutions[loc].Count);
                                }
                                else
                                {
                                    if (typeResolutions.ContainsKey(loc))
                                    {
                                        var len = typeResolutions[loc].Count;
                                        constructedTypes.UnionWith(typeResolutions[loc]);
                                        changed |= (len != typeResolutions[loc].Count);
                                    }
                                }
                                Console.WriteLine("{0}: {1} is a field", loc.GetMappedLineSpan(), name);
                            }
                        }
                    }

                    foreach (var invoke in expressions.OfType<InvocationExpressionSyntax>())
                    {
                        ITypeSymbol type = null;
                        string methodName = null;
                        if (invoke.Expression is MemberAccessExpressionSyntax)
                        {
                            var dot = invoke.Expression as MemberAccessExpressionSyntax;
                            type = model.GetTypeInfo(dot.Expression).Type;
                            methodName = dot.Name.ToString();
                        }
                        else
                        {
                            type = model.GetDeclaredSymbol(classDeclaration);
                            methodName = invoke.Expression.ToString();
                        }
                        if (type == null)
                        {
                            Console.WriteLine("Error in getting type of {0}", invoke.Expression);
                            continue;
                        }
                        var potentialTypes = new HashSet<ITypeSymbol>(constructedTypes.Where(constructedType => isParent(type, constructedType)));
                        Console.WriteLine("Invoke {0} could be {1}", invoke, potentialTypes.Select(t => t.ToString()).Aggregate((a, b) => a + ", " + b));
                        var location = invoke.GetLocation();
                        if (typeResolutions.ContainsKey(location))
                        {
                            typeResolutions[location].UnionWith(potentialTypes);
                        }
                        else
                        {
                            typeResolutions.Add(location, potentialTypes);
                        }
                        foreach (var t in potentialTypes)
                        {
                            Console.WriteLine("Type {0}?", t);
                            AnalyzeFunction(classDeclarations, constructedTypes, t, methodName);
                        }
                    }

                    var returnType = model.GetTypeInfo(method.ReturnType).Type;
                    constructedTypesOut.UnionWith(constructedTypes.Where(t => isParent(returnType, t)));
                }
            }
        }

        /** Test if a is the parent of b */
        private bool isParent(ITypeSymbol a, ITypeSymbol b)
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

        public void AddParents(INamespaceSymbol namespaceSymbol)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                AddParents(type);
            }
            foreach (var ns in namespaceSymbol.GetNamespaceMembers())
            {
                AddParents(ns);
            }
        }

        private void AddParents(INamedTypeSymbol type)
        {
            if (type.BaseType != null)
            {
                AddEdge(type.BaseType, type);
            }
            
            foreach (var i in type.Interfaces)
            {
                AddEdge(i, type);
            }

            foreach (var nested in type.GetTypeMembers())
            {
                AddParents(nested);
            }
        }

        private string stringify(ITypeSymbol type)
        {
            return type.ContainingNamespace.ToString() + "." + type.Name;
        }

        private void AddEdge(INamedTypeSymbol baseSymbol, INamedTypeSymbol type)
        {
            string baseName = stringify(baseSymbol);

            if (!parentToChildren.ContainsKey(baseName))
            {
                parentToChildren.Add(baseName, new HashSet<INamedTypeSymbol>());
            }

            parentToChildren[baseName].Add(type);
        }

        public ISet<INamedTypeSymbol> GetCHADescendants(string parentClass)
        {
            var set = new HashSet<INamedTypeSymbol>();
            var workingSet = parentToChildren[parentClass];
            var newWorkingSet = new HashSet<INamedTypeSymbol>();
            for (var setSize = set.Count - 1; setSize != set.Count; setSize = set.Count) {
                foreach (var child in workingSet)
                {
                    var childS = stringify(child);
                    if (parentToChildren.ContainsKey(childS))
                    {
                        newWorkingSet.UnionWith(parentToChildren[childS]);
                    }
                }
                set.UnionWith(workingSet);
                workingSet = newWorkingSet;
                newWorkingSet = new HashSet<INamedTypeSymbol>();
            }
            return set;
        }

        public ISet<INamedTypeSymbol> GetCHADescendants(INamedTypeSymbol parentClass)
        {
            return GetCHADescendants(stringify(parentClass));
        }

        public ISet<INamedTypeSymbol> GetCHADescendants(ITypeSymbol parentClass)
        {
            return GetCHADescendants(stringify(parentClass));
        }

        public ISet<IMethodSymbol> ResolveDispatch(InvocationExpressionSyntax node)
        {
            //return ResolveDispatchCHA(node);
            return ResolveDispatchRTA(node);
        }

        private ISet<IMethodSymbol> ResolveDispatchRTA(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax) {
                var dot = node.Expression as MemberAccessExpressionSyntax;
                return new HashSet<IMethodSymbol>(typeResolutions[node.GetLocation()].Select(type =>
                {
                    foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (method.Name.Equals(dot.Name))
                        {
                            return method;
                        }
                    }
                    return null;
                }));
            }

            throw new NotSupportedException();
        }

        private ISet<IMethodSymbol> ResolveDispatchCHA(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax)
            {
                var dot = node.Expression as MemberAccessExpressionSyntax;
                ITypeSymbol typeInfo = model.GetTypeInfo(dot.Expression).Type;
                return ResolveDispatchCHA(stringify(typeInfo), dot.Name.ToString());
            }

            throw new NotSupportedException();
        }

        public ISet<IMethodSymbol> ResolveDispatchCHA(string c, string m)
        {
            var hs = new HashSet<IMethodSymbol>();
            foreach (var type in GetCHADescendants(c))
            {
                foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if (method.Name.Equals(m))
                    {
                        hs.Add(method);
                    }
                }
            }
            return hs;
        }
    }
}

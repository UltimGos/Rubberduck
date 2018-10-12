﻿using Antlr4.Runtime.Tree;
using Rubberduck.Inspections.CodePathAnalysis.Nodes;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Symbols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rubberduck.Inspections.CodePathAnalysis
{
    public class Walker
    {
        public INode GenerateTree(IParseTree tree, Declaration declaration)
        {
            INode node = default;
            switch (tree)
            {
                case VBAParser.ForNextStmtContext _:
                case VBAParser.ForEachStmtContext _:
                case VBAParser.WhileWendStmtContext _:
                case VBAParser.DoLoopStmtContext _:
                    node = new LoopNode();
                    break;
                case VBAParser.IfStmtContext _:
                case VBAParser.ElseBlockContext _:
                case VBAParser.ElseIfBlockContext _:
                case VBAParser.SingleLineIfStmtContext _:
                case VBAParser.SingleLineElseClauseContext _:
                case VBAParser.CaseClauseContext _:
                case VBAParser.CaseElseClauseContext _:
                    node = new BranchNode();
                    break;
                case VBAParser.BlockContext _:
                    node = new BlockNode();
                    break;
            }

            if (declaration.Context == tree)
            {
                node = new DeclarationNode
                {
                    Declaration = declaration
                };
            }

            var reference = declaration.References.SingleOrDefault(w => w.Context == tree);
            if (reference != null)
            {
                if (reference.IsAssignment)
                {
                    node = new AssignmentNode
                    {
                        Reference = reference
                    };
                }
                else
                {
                    node = new ReferenceNode
                    {
                        Reference = reference
                    };
                }
            }

            if (node == null)
            {
                node = new GenericNode();
            }

            var children = new List<INode>();
            for (var i = 0; i < tree.ChildCount; i++)
            {
                var nextChild = GenerateTree(tree.GetChild(i), declaration);
                nextChild.SortOrder = i;
                nextChild.Parent = node;

                if (nextChild.Children.Any() || nextChild.GetType() != typeof(GenericNode))
                {
                    children.Add(nextChild);
                }
            }

            node.Children = children.ToImmutableList();

            return node;
        }
    }
}

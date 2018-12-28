﻿using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using NLog;
using Rubberduck.Parsing.Annotations;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Rewriter;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA.Extensions;

namespace Rubberduck.Parsing.VBA
{
    public class AnnotationUpdater : IAnnotationUpdater
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public void AddAnnotation(IRewriteSession rewriteSession, QualifiedContext context, AnnotationType annotationType, IReadOnlyList<string> values = null)
        {
            var annotationValues = values ?? new List<string>();

            if (context == null)
            {
                _logger.Warn("Tried to add an annotation to a context that is null.");
                _logger.Trace($"Tried to add annotation {annotationType} with values {AnnotationValuesText(annotationValues)} to a context that is null.");
                return;
            }

            var annotationText = AnnotationText(annotationType, annotationValues);

            string codeToAdd;
            IModuleRewriter rewriter;
            if (context.Context.start.Line == 1)
            {
                codeToAdd = $"{annotationText}{Environment.NewLine}";
                rewriter = rewriteSession.CheckOutModuleRewriter(context.ModuleName);
                rewriter.InsertBefore(0, codeToAdd);
                return;
            }

            var previousEndOfLine = PreviousEndOfLine(context.Context);
            if (context.Context.start.Line > previousEndOfLine.stop.Line + 1)
            {
                _logger.Warn("Tried to add an annotation to a context not on the first physical line of a logical line.");
                _logger.Trace($"Tried to add annotation {annotationType} with values {AnnotationValuesText(annotationValues)} to a the context with text '{context.Context.GetText()}' at {context.Context.GetSelection()} in module {context.ModuleName}.");
                return;
            }
            
            codeToAdd = previousEndOfLine.TryGetFollowingContext(out VBAParser.WhiteSpaceContext whitespaceAtStartOfLine) 
                            ? $"{whitespaceAtStartOfLine.GetText()}{annotationText}{Environment.NewLine}" 
                            : $"{annotationText}{Environment.NewLine}";
            rewriter = rewriteSession.CheckOutModuleRewriter(context.ModuleName);
            rewriter.InsertAfter(previousEndOfLine.stop.TokenIndex, codeToAdd);
        }

        private static string AnnotationText(AnnotationType annotationType, IReadOnlyList<string> values)
        {
            return $"'{AnnotationBase.ANNOTATION_MARKER}{AnnotationBaseText(annotationType, values)}";
        }

        private static string AnnotationBaseText(AnnotationType annotationType, IReadOnlyList<string> values)
        {
            return $"{annotationType}{(values.Any() ? $" {AnnotationValuesText(values)}" : string.Empty)}";
        }

        private static string AnnotationValuesText(IEnumerable<string> annotationValues)
        {
            return string.Join(", ", annotationValues);
        }

        private static VBAParser.EndOfLineContext PreviousEndOfLine(ParserRuleContext context)
        {
            var moduleContext = context.GetAncestor<VBAParser.ModuleContext>();
            var endOfLineListener = new EndOfLineListener();
            ParseTreeWalker.Default.Walk(endOfLineListener, moduleContext);
            var previousEol = endOfLineListener.Contexts
                .OrderBy(eol => eol.Start.TokenIndex)
                .LastOrDefault(eol => eol.stop.TokenIndex < context.start.TokenIndex);
            return previousEol;
        }

        public void AddAnnotation(IRewriteSession rewriteSession, Declaration declaration, AnnotationType annotationType, IReadOnlyList<string> values = null)
        {
            var annotationValues = values ?? new List<string>();

            if (declaration == null)
            {
                _logger.Warn("Tried to add an annotation to a declaration that is null.");
                _logger.Trace($"Tried to add annotation {annotationType} with values {AnnotationValuesText(annotationValues)} to a declaration that is null.");
                return;
            }

            if (declaration.DeclarationType.HasFlag(DeclarationType.Module))
            {
                AddModuleAnnotation(rewriteSession, declaration, annotationType, annotationValues);
            }
            else if (declaration.DeclarationType.HasFlag(DeclarationType.Variable))
            {
                AddVariableAnnotation(rewriteSession, declaration, annotationType, annotationValues);
            }
            else
            {
                AddMemberAnnotation(rewriteSession, declaration, annotationType, annotationValues);
            }
        }

        private void AddModuleAnnotation(IRewriteSession rewriteSession, Declaration declaration, AnnotationType annotationType, IReadOnlyList<string> values)
        {
            if (!annotationType.HasFlag(AnnotationType.ModuleAnnotation))
            {
                _logger.Warn("Tried to add an annotation without the module annotation flag to a module.");
                _logger.Trace($"Tried to add the annotation {annotationType} with values {AnnotationValuesText(values)} to the module {declaration.QualifiedModuleName}.");
                return;
            }

            var codeToAdd = $"{AnnotationText(annotationType, values)}{Environment.NewLine}";

            var rewriter = rewriteSession.CheckOutModuleRewriter(declaration.QualifiedModuleName);
            rewriter.InsertBefore(0, codeToAdd);
        }

        private void AddVariableAnnotation(IRewriteSession rewriteSession, Declaration declaration, AnnotationType annotationType, IReadOnlyList<string> values)
        {
            if (!annotationType.HasFlag(AnnotationType.VariableAnnotation))
            {
                _logger.Warn("Tried to add an annotation without the variable annotation flag to a variable declaration.");
                _logger.Trace($"Tried to add the annotation {annotationType} with values {AnnotationValuesText(values)} to the variable declaration for {declaration.QualifiedName}.");
                return;
            }

            AddAnnotation(rewriteSession, new QualifiedContext(declaration.QualifiedName, declaration.Context), annotationType, values);
        }

        private void AddMemberAnnotation(IRewriteSession rewriteSession, Declaration declaration, AnnotationType annotationType, IReadOnlyList<string> values)
        {
            if (!annotationType.HasFlag(AnnotationType.MemberAnnotation))
            {
                _logger.Warn("Tried to add an annotation without the member annotation flag to a member declaration.");
                _logger.Trace($"Tried to add the annotation {annotationType} with values {AnnotationValuesText(values)} to the member declaration for {declaration.QualifiedName}.");
                return;
            }

            AddAnnotation(rewriteSession, new QualifiedContext(declaration.QualifiedName, declaration.Context), annotationType, values);
        }


        public void AddAnnotation(IRewriteSession rewriteSession, IdentifierReference reference, AnnotationType annotationType,
            IReadOnlyList<string> values = null)
        {
            var annotationValues = values ?? new List<string>();

            if (reference == null)
            {
                _logger.Warn("Tried to add an annotation to an identifier reference that is null.");
                _logger.Trace($"Tried to add annotation {annotationType} with values {AnnotationValuesText(annotationValues)} to an identifier reference that is null.");
                return;
            }

            if (!annotationType.HasFlag(AnnotationType.IdentifierAnnotation))
            {
                _logger.Warn("Tried to add an annotation without the identifier reference annotation flag to an identifier reference.");
                _logger.Trace($"Tried to add annotation {annotationType} with values {AnnotationValuesText(annotationValues)} to the identifier reference to {reference.Declaration.QualifiedName} at {reference.Selection} in module {reference.QualifiedModuleName}.");
                return;
            }

            AddAnnotation(rewriteSession, new QualifiedContext(reference.QualifiedModuleName, reference.Context), annotationType, annotationValues);
        }

        public void RemoveAnnotation(IRewriteSession rewriteSession, IAnnotation annotation)
        {
            if (annotation == null)
            {
                _logger.Warn("Tried to remove an annotation that is null.");
                return;
            }

            var annotationContext = annotation.Context;
            var annotationList = (VBAParser.AnnotationListContext)annotationContext.Parent;

            var rewriter = rewriteSession.CheckOutModuleRewriter(annotation.QualifiedSelection.QualifiedName);

            var annotations = annotationList.annotation();
            if (annotations.Length == 1)
            {
                RemoveSingleAnnotation(rewriter, annotationContext, annotationList);
            }

            RemoveAnnotationMarker(rewriter, annotationContext);
            rewriter.Remove(annotationContext);
        }

        private static void RemoveSingleAnnotation(IModuleRewriter rewriter, VBAParser.AnnotationContext annotationContext, VBAParser.AnnotationListContext annotationListContext)
        {
            var commentSeparator = annotationListContext.COLON();
            if(commentSeparator == null)
            {
                RemoveEntireLine(rewriter, annotationContext);
            }
            else
            {
                RemoveAnnotationMarker(rewriter, annotationContext);
                rewriter.Remove(annotationContext);
                rewriter.Remove(commentSeparator);
            }
        }

        private static void RemoveEntireLine(IModuleRewriter rewriter, ParserRuleContext contextInCommentOrAnnotation)
        {
            var previousEndOfLineContext = PreviousEndOfLine(contextInCommentOrAnnotation);
            var containingCommentOrAnnotationContext = contextInCommentOrAnnotation.GetAncestor<VBAParser.CommentOrAnnotationContext>();

            if (previousEndOfLineContext == null)
            {
                //We are on the first logical line.
                rewriter.RemoveRange(0, containingCommentOrAnnotationContext.stop.TokenIndex);
            }
            else if (containingCommentOrAnnotationContext.Eof() != null)
            {
                //We are on the last logical line. So swallow the NEWLINE from the previous end of line.
                rewriter.RemoveRange(previousEndOfLineContext.stop.TokenIndex, containingCommentOrAnnotationContext.stop.TokenIndex);
            }
            else
            {
                rewriter.RemoveRange(previousEndOfLineContext.stop.TokenIndex + 1, containingCommentOrAnnotationContext.stop.TokenIndex);
            }
        }

        private static void RemoveAnnotationMarker(IModuleRewriter rewriter, VBAParser.AnnotationContext annotationContext)
        {
            var endOfAnnotationMarker = annotationContext.start.TokenIndex - 1;
            var startOfAnnotationMarker = endOfAnnotationMarker - AnnotationBase.ANNOTATION_MARKER.Length + 1;
            rewriter.RemoveRange(startOfAnnotationMarker, endOfAnnotationMarker);
        }

        public void RemoveAnnotations(IRewriteSession rewriteSession, IEnumerable<IAnnotation> annotations)
        {
            if (annotations == null)
            {
                return;
            }

            var annotationsByAnnotationList = annotations.Distinct()
                .GroupBy(annotation => new QualifiedContext(annotation.QualifiedSelection.QualifiedName, (ParserRuleContext)annotation.Context.Parent))
                .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());

            if (!annotationsByAnnotationList.Keys.Any())
            {
                return;
            }

            foreach (var qualifiedAnnotationList in annotationsByAnnotationList.Keys)
            {
                var annotationList = (VBAParser.AnnotationListContext) qualifiedAnnotationList.Context;
                if (annotationList.commentBody() == null && annotationList.annotation().Length == annotationsByAnnotationList[qualifiedAnnotationList].Count)
                {
                    //We want to remove all annotations in the list. So, we remove the entire line.
                    //This does not really work if there are multiple consecutive lines at the end of the file that need to be removed,
                    //but I think we can live with leaving an empty line in this edge-case.
                    var rewriter = rewriteSession.CheckOutModuleRewriter(qualifiedAnnotationList.ModuleName);
                    RemoveEntireLine(rewriter, annotationList);
                }
                else
                {
                    foreach (var annotation in annotationsByAnnotationList[qualifiedAnnotationList])
                    {
                        RemoveAnnotation(rewriteSession, annotation);
                    }
                }
            }
        }

        public void UpdateAnnotation(IRewriteSession rewriteSession, IAnnotation annotation, AnnotationType newAnnotationType, IReadOnlyList<string> newValues = null)
        {
            var newAnnotationValues = newValues ?? new List<string>();

            if (annotation == null)
            {
                _logger.Warn("Tried to replace an annotation that is null.");
                _logger.Trace($"Tried to replace an annotation that is null with an annotation {newAnnotationType} with values {AnnotationValuesText(newAnnotationValues)}.");
                return;
            }

            //If there are no common flags, the annotations cannot apply to the same target.
            if ((annotation.AnnotationType & newAnnotationType) == 0)
            {
                _logger.Warn("Tried to replace an annotation with an annotation without common flags.");
                _logger.Trace($"Tried to replace an annotation {annotation.AnnotationType} with values {AnnotationValuesText(newValues)} at {annotation.QualifiedSelection.Selection} in module {annotation.QualifiedSelection.QualifiedName} with an annotation {newAnnotationType} with values {AnnotationValuesText(newAnnotationValues)}, which does not have any common flags.");
                return;
            }
            
            var context = annotation.Context;
            var whitespaceAtEnd = context.whiteSpace()?.GetText() ?? string.Empty;
            var codeReplacement = $"{AnnotationBaseText(newAnnotationType, newAnnotationValues)}{whitespaceAtEnd}";

            var rewriter = rewriteSession.CheckOutModuleRewriter(annotation.QualifiedSelection.QualifiedName);
            rewriter.Replace(annotation.Context, codeReplacement);
        }

        private class EndOfLineListener : VBAParserBaseListener
        {
            private readonly IList<VBAParser.EndOfLineContext> _contexts = new List<VBAParser.EndOfLineContext>();
            public IEnumerable<VBAParser.EndOfLineContext> Contexts => _contexts;

            public override void ExitEndOfLine([NotNull] VBAParser.EndOfLineContext context)
            {
                _contexts.Add(context);
            }
        }
    }
}
using Rubberduck.Common;
using Rubberduck.Inspections.Abstract;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Inspections.Abstract;
using Rubberduck.Parsing.Inspections.Resources;

namespace Rubberduck.Inspections.Results
{
    public class MissingAnnotationArgumentInspectionResult : InspectionResultBase
    {
        public MissingAnnotationArgumentInspectionResult(IInspection inspection, QualifiedContext<VBAParser.AnnotationContext> qualifiedContext)
            : base(inspection, qualifiedContext.ModuleName, qualifiedContext.Context) {}

        public override string Description
        {
            get { return string.Format(InspectionsUI.MissingAnnotationArgumentInspectionResultFormat, ((VBAParser.AnnotationContext)Context).annotationName().GetText()).Capitalize(); }
        }
    }
}
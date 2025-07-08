using Microsoft.Extensions.Localization;

namespace centrny.Resources
{
    /// <summary>
    /// Provides strongly-typed access to shared localized resources
    /// </summary>
    public class SharedResources
    {
        private readonly IStringLocalizer<SharedResources> _localizer;

        public SharedResources(IStringLocalizer<SharedResources> localizer)
        {
            _localizer = localizer;
        }

        public string Welcome => _localizer["Welcome"];
        public string Home => _localizer["Home"];
        public string Login => _localizer["Login"];
        public string Logout => _localizer["Logout"];
        public string Submit => _localizer["Submit"];
        public string Cancel => _localizer["Cancel"];
        public string Save => _localizer["Save"];
        public string Delete => _localizer["Delete"];
        public string Edit => _localizer["Edit"];
        public string Add => _localizer["Add"];
        public string Search => _localizer["Search"];
        public string Clear => _localizer["Clear"];
        public string Refresh => _localizer["Refresh"];
        public string Today => _localizer["Today"];
        public string Loading => _localizer["Loading"];
        public string Language => _localizer["Language"];
        
        // Student Related
        public string StudentRegistration => _localizer["StudentRegistration"];
        public string CompleteRegistration => _localizer["CompleteRegistration"];
        public string StudentProfile => _localizer["StudentProfile"];
        public string StudentName => _localizer["StudentName"];
        public string StudentCode => _localizer["StudentCode"];
        public string StudentPhone => _localizer["StudentPhone"];
        public string ParentPhone => _localizer["ParentPhone"];
        public string Address => _localizer["Address"];
        
        // Academic
        public string Subject => _localizer["Subject"];
        public string Teacher => _localizer["Teacher"];
        public string Class => _localizer["Class"];
        public string Schedule => _localizer["Schedule"];
        public string Chapter => _localizer["Chapter"];
        public string Question => _localizer["Question"];
        public string Exam => _localizer["Exam"];
        public string Year => _localizer["Year"];
        public string EducationYear => _localizer["EducationYear"];
        
        // Daily Class
        public string DailyTimeline => _localizer["DailyTimeline"];
        public string GenerateWeeklyClasses => _localizer["GenerateWeeklyClasses"];
        public string AddChapter => _localizer["AddChapter"];
        public string ChapterName => _localizer["ChapterName"];
        
        // Security Messages
        public string SecureInformation => _localizer["SecureInformation"];
        
        // Root/Center
        public string RootCode => _localizer["RootCode"];
        public string RootName => _localizer["RootName"];
        public string RootPhone => _localizer["RootPhone"];
        public string RootOwner => _localizer["RootOwner"];
        public string IsCenter => _localizer["IsCenter"];
        
        // Common Form Validation
        public string Required => _localizer["Required"];
        public string FieldRequired => _localizer["FieldRequired"];
        
        // Navigation
        public string PreviousDay => _localizer["PreviousDay"];
        public string NextDay => _localizer["NextDay"];

        // Dynamic access for keys not defined above
        public string this[string key] => _localizer[key];
    }
}
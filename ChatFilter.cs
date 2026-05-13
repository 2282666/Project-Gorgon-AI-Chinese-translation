using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace GorgonChinesePatch
{
    public class ChatFilter
    {
        private ManualLogSource _log;
        private HashSet<string> _chatUIPatterns;
        private HashSet<string> _skipUIPatterns;
        private List<Regex> _chatTextPatterns;
        private List<Regex> _skipTextPatterns;
        private List<Regex> _uiPathPatterns;

        public ChatFilter(ManualLogSource log)
        {
            _log = log;
            _chatUIPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _skipUIPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _chatTextPatterns = new List<Regex>();
            _skipTextPatterns = new List<Regex>();
            _uiPathPatterns = new List<Regex>();
            InitializePatterns();
        }

        private void InitializePatterns()
        {
            _chatUIPatterns.Add("ChatPanel");
            _chatUIPatterns.Add("ChatWindow");
            _chatUIPatterns.Add("ChatBox");
            _chatUIPatterns.Add("ChatInput");
            _chatUIPatterns.Add("ChatMessage");
            _chatUIPatterns.Add("ChatLog");
            _chatUIPatterns.Add("SocialPanel");
            _chatUIPatterns.Add("Chat");

            _skipUIPatterns.Add("UpdateLog");
            _skipUIPatterns.Add("PatchNotes");
            _skipUIPatterns.Add("ChangeLog");
            _skipUIPatterns.Add("VersionLog");
            _skipUIPatterns.Add("NewsPanel");
            _skipUIPatterns.Add("Announcement");

            _chatTextPatterns.Add(new Regex(@"^\[.*?\]\s*\w+:", RegexOptions.Compiled));
            _chatTextPatterns.Add(new Regex(@"^\w+\s*says:", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _chatTextPatterns.Add(new Regex(@"^\w+\s*whispers:", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _chatTextPatterns.Add(new Regex(@"^\w+\s*shouts:", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _chatTextPatterns.Add(new Regex(@"^\w+\s*asks:", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _chatTextPatterns.Add(new Regex(@"^\w+\s*replies:", RegexOptions.Compiled | RegexOptions.IgnoreCase));

            _skipTextPatterns.Add(new Regex(@"^(v|version)\s*\d+\.\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _skipTextPatterns.Add(new Regex(@"^\d{4}-\d{2}-\d{2}", RegexOptions.Compiled));
            _skipTextPatterns.Add(new Regex(@"^(fixed|added|changed|removed|updated|improved)\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _skipTextPatterns.Add(new Regex(@"^[-*]\s*(fixed|added|changed|removed|updated|improved)", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _skipTextPatterns.Add(new Regex(@"^\d+$", RegexOptions.Compiled));
            _skipTextPatterns.Add(new Regex(@"^\d+\s*%", RegexOptions.Compiled));
            _skipTextPatterns.Add(new Regex(@"^\d+\s*fps$", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _skipTextPatterns.Add(new Regex(@"^\d+/\d+$", RegexOptions.Compiled));
            _skipTextPatterns.Add(new Regex(@"^\d+\.\d+$", RegexOptions.Compiled));
            _skipTextPatterns.Add(new Regex(@"^\d+ms$", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _skipTextPatterns.Add(new Regex(@"^<sprite=\d+>\s*\d+$", RegexOptions.Compiled));
            _skipTextPatterns.Add(new Regex(@"fps", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _skipTextPatterns.Add(new Regex(@"framerate", RegexOptions.Compiled | RegexOptions.IgnoreCase));

            _uiPathPatterns.Add(new Regex(@"Chat", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Social", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Message.*List", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Player.*Name", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Update.*Log", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Patch.*Notes", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Change.*Log", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Framerate", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"FPS", RegexOptions.Compiled | RegexOptions.IgnoreCase));
            _uiPathPatterns.Add(new Regex(@"Performance", RegexOptions.Compiled | RegexOptions.IgnoreCase));
        }

        public bool IsChatUI(string uiPath)
        {
            if (string.IsNullOrEmpty(uiPath))
                return false;

            foreach (var pattern in _chatUIPatterns)
            {
                if (uiPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            foreach (var regex in _uiPathPatterns)
            {
                if (regex.IsMatch(uiPath))
                    return true;
            }

            return false;
        }

        public bool IsSkipUI(string uiPath)
        {
            if (string.IsNullOrEmpty(uiPath))
                return false;

            foreach (var pattern in _skipUIPatterns)
            {
                if (uiPath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool IsChatMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var regex in _chatTextPatterns)
            {
                if (regex.IsMatch(text))
                    return true;
            }

            return false;
        }

        public bool IsSkipText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var regex in _skipTextPatterns)
            {
                if (regex.IsMatch(text))
                    return true;
            }

            return false;
        }

        public bool ShouldTranslate(string uiPath, string text)
        {
            if (IsChatUI(uiPath))
                return false;

            if (IsSkipUI(uiPath))
                return false;

            if (IsChatMessage(text))
                return false;

            if (IsSkipText(text))
                return false;

            return true;
        }
    }
}

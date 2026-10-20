using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string path = "docs/Task_Details.md";
        string text = File.ReadAllText(path);
        
        // Fix TK-131 heading broken char
        text = text.Replace("### TK-131  Merge", "### TK-131  Merge");
        text = text.Replace("### TK-131 A Merge", "### TK-131  Merge");
        
        // Fix group lines for TK-131 to TK-135
        for (int i = 131; i <= 135; i++)
        {
            var regex = new Regex($"### TK-{i}.*?\n(.*?)- \\*\\*Group:\\*\\* F A.*? Phase 3: POS", RegexOptions.Singleline);
            text = regex.Replace(text, $"### TK-{i}$1- **Group:** J  Business types, add-ons and one worker project");
        }
        
        File.WriteAllText(path, text);
    }
}
